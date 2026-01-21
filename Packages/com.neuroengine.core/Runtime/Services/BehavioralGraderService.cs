using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NeuroEngine.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Result of a single playtest assertion.
    /// </summary>
    public class PlaytestAssertion
    {
        public string Name;
        public bool Passed;
        public string Message;
        public float TimestampSeconds;
        public Dictionary<string, object> Context;

        public PlaytestAssertion()
        {
            Context = new Dictionary<string, object>();
        }

        public static PlaytestAssertion Pass(string name, string message = null, float timestamp = 0f)
        {
            return new PlaytestAssertion
            {
                Name = name,
                Passed = true,
                Message = message ?? "Assertion passed",
                TimestampSeconds = timestamp
            };
        }

        public static PlaytestAssertion Fail(string name, string message, float timestamp = 0f)
        {
            return new PlaytestAssertion
            {
                Name = name,
                Passed = false,
                Message = message,
                TimestampSeconds = timestamp
            };
        }
    }

    /// <summary>
    /// Complete result from a playtest run.
    /// </summary>
    public class PlaytestResult
    {
        public string TestName;
        public bool Success;
        public float DurationSeconds;
        public int TotalAssertions;
        public int PassedAssertions;
        public int FailedAssertions;
        public List<PlaytestAssertion> Assertions;
        public List<string> Logs;
        public string FailureReason;
        public Dictionary<string, object> Metadata;

        public PlaytestResult()
        {
            Assertions = new List<PlaytestAssertion>();
            Logs = new List<string>();
            Metadata = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Tier 3: Behavioral grading - automated playtests.
    /// Requires PlayMode, simulates player actions, verifies game flows.
    ///
    /// Uses InputSimulationService for input injection and integrates
    /// with the PlaytestBridge pattern for game state inspection.
    /// </summary>
    public class BehavioralGraderService : IBehavioralGrader
    {
        private readonly IInputSimulation _inputSimulation;
        private readonly ISceneStateCapture _sceneCapture;
        private readonly IHooksWriter _hooksWriter;

        private const string GRADER_ID_PLAYTEST = "behavioral.playtest";
        private const string GRADER_ID_FLOW = "behavioral.flow";
        private const string GRADER_ID_INTERACTION = "behavioral.interaction";

        // For tracking assertions during playtest
        private readonly List<PlaytestAssertion> _currentAssertions = new List<PlaytestAssertion>();
        private readonly List<string> _currentLogs = new List<string>();
        private float _playtestStartTime;

        public BehavioralGraderService(
            IInputSimulation inputSimulation,
            ISceneStateCapture sceneCapture,
            IHooksWriter hooksWriter)
        {
            _inputSimulation = inputSimulation;
            _sceneCapture = sceneCapture;
            _hooksWriter = hooksWriter;
        }

        /// <summary>
        /// Parameterless constructor for editor/standalone use.
        /// </summary>
        public BehavioralGraderService()
        {
            _inputSimulation = new InputSimulationService();
            _sceneCapture = new SceneStateCaptureService();
            // HooksWriter requires IEnvConfig - will be null in standalone mode
            _hooksWriter = null;
        }

        /// <summary>
        /// Run an automated playtest sequence.
        /// Executes input sequences and checks success/failure conditions.
        /// </summary>
        public async Task<GraderResult> RunPlaytestAsync(PlaytestConfig config)
        {
            var sw = Stopwatch.StartNew();

            if (config == null)
            {
                return GraderResult.Error(GRADER_ID_PLAYTEST, EvaluationTier.Behavioral,
                    "PlaytestConfig is null");
            }

#if UNITY_EDITOR
            // Check if we're in PlayMode
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                return GraderResult.Error(GRADER_ID_PLAYTEST, EvaluationTier.Behavioral,
                    "Behavioral tests require Play Mode. Use manage_editor(action='play') first.");
            }
#endif

            try
            {
                // Reset state for this playtest
                _currentAssertions.Clear();
                _currentLogs.Clear();
                _playtestStartTime = Time.realtimeSinceStartup;

                // Log start
                LogPlaytest($"Starting playtest: {config.InputScripts?.Count ?? 0} input sequences, timeout: {config.TimeoutSeconds}s");

                // Execute input sequences
                if (config.InputScripts != null && config.InputScripts.Count > 0)
                {
                    foreach (var sequence in config.InputScripts)
                    {
                        var sequenceResult = await ExecuteInputSequenceAsync(sequence, config.TimeoutSeconds);
                        if (!sequenceResult.success)
                        {
                            // Sequence failed - check if this is a failure condition
                            LogPlaytest($"Input sequence '{sequence.Name}' failed: {sequenceResult.error}");
                        }
                    }
                }

                // Check success conditions
                bool allSuccessConditionsMet = true;
                var failedConditions = new List<string>();

                if (config.SuccessConditions != null)
                {
                    foreach (var condition in config.SuccessConditions)
                    {
                        var conditionMet = EvaluateCondition(condition);
                        if (!conditionMet)
                        {
                            allSuccessConditionsMet = false;
                            failedConditions.Add(condition);
                            RecordAssertion(PlaytestAssertion.Fail($"success:{condition}",
                                $"Success condition not met: {condition}",
                                Time.realtimeSinceStartup - _playtestStartTime));
                        }
                        else
                        {
                            RecordAssertion(PlaytestAssertion.Pass($"success:{condition}",
                                $"Success condition met: {condition}",
                                Time.realtimeSinceStartup - _playtestStartTime));
                        }
                    }
                }

                // Check failure conditions (should NOT be met)
                bool anyFailureConditionMet = false;
                var triggeredFailures = new List<string>();

                if (config.FailureConditions != null)
                {
                    foreach (var condition in config.FailureConditions)
                    {
                        var conditionMet = EvaluateCondition(condition);
                        if (conditionMet)
                        {
                            anyFailureConditionMet = true;
                            triggeredFailures.Add(condition);
                            RecordAssertion(PlaytestAssertion.Fail($"failure:{condition}",
                                $"Failure condition triggered: {condition}",
                                Time.realtimeSinceStartup - _playtestStartTime));
                        }
                        else
                        {
                            RecordAssertion(PlaytestAssertion.Pass($"failure:{condition}",
                                $"Failure condition not triggered: {condition}",
                                Time.realtimeSinceStartup - _playtestStartTime));
                        }
                    }
                }

                sw.Stop();

                // Build result
                var playtestResult = new PlaytestResult
                {
                    TestName = config.ScenePath ?? "playtest",
                    Success = allSuccessConditionsMet && !anyFailureConditionMet,
                    DurationSeconds = sw.ElapsedMilliseconds / 1000f,
                    TotalAssertions = _currentAssertions.Count,
                    PassedAssertions = _currentAssertions.Count(a => a.Passed),
                    FailedAssertions = _currentAssertions.Count(a => !a.Passed),
                    Assertions = new List<PlaytestAssertion>(_currentAssertions),
                    Logs = new List<string>(_currentLogs)
                };

                if (!playtestResult.Success)
                {
                    if (failedConditions.Count > 0)
                        playtestResult.FailureReason = $"Success conditions not met: {string.Join(", ", failedConditions)}";
                    else if (triggeredFailures.Count > 0)
                        playtestResult.FailureReason = $"Failure conditions triggered: {string.Join(", ", triggeredFailures)}";
                }

                // Persist result if hooks writer available
                await PersistPlaytestResultAsync(playtestResult);

                // Convert to GraderResult
                return BuildGraderResult(GRADER_ID_PLAYTEST, playtestResult, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_PLAYTEST, EvaluationTier.Behavioral,
                    $"Playtest failed with exception: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        /// <summary>
        /// Verify a specific game flow (e.g., "can complete tutorial").
        /// Executes flow steps sequentially, waiting for expected results.
        /// </summary>
        public async Task<GraderResult> VerifyFlowAsync(GameFlowConfig flowConfig)
        {
            var sw = Stopwatch.StartNew();

            if (flowConfig == null)
            {
                return GraderResult.Error(GRADER_ID_FLOW, EvaluationTier.Behavioral,
                    "GameFlowConfig is null");
            }

#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                return GraderResult.Error(GRADER_ID_FLOW, EvaluationTier.Behavioral,
                    "Flow verification requires Play Mode.");
            }
#endif

            try
            {
                _currentAssertions.Clear();
                _currentLogs.Clear();
                _playtestStartTime = Time.realtimeSinceStartup;

                LogPlaytest($"Verifying flow: {flowConfig.FlowName}");

                int completedSteps = 0;
                string failedStep = null;
                string failureReason = null;

                if (flowConfig.Steps != null)
                {
                    foreach (var step in flowConfig.Steps)
                    {
                        LogPlaytest($"Step {completedSteps + 1}: {step.Description}");

                        // Execute the action
                        var actionResult = await ExecuteFlowActionAsync(step.Action);
                        if (!actionResult.success)
                        {
                            failedStep = step.Description;
                            failureReason = $"Action failed: {actionResult.error}";
                            RecordAssertion(PlaytestAssertion.Fail($"step:{completedSteps + 1}:action",
                                failureReason, Time.realtimeSinceStartup - _playtestStartTime));
                            break;
                        }

                        // Wait for expected result
                        var waitResult = await WaitForConditionAsync(step.ExpectedResult, step.MaxWaitSeconds);
                        if (!waitResult.success)
                        {
                            failedStep = step.Description;
                            failureReason = $"Expected result not achieved: {step.ExpectedResult}";
                            RecordAssertion(PlaytestAssertion.Fail($"step:{completedSteps + 1}:expect",
                                failureReason, Time.realtimeSinceStartup - _playtestStartTime));
                            break;
                        }

                        RecordAssertion(PlaytestAssertion.Pass($"step:{completedSteps + 1}",
                            $"Step completed: {step.Description}",
                            Time.realtimeSinceStartup - _playtestStartTime));

                        completedSteps++;
                    }
                }

                sw.Stop();

                var success = completedSteps == (flowConfig.Steps?.Count ?? 0);
                var playtestResult = new PlaytestResult
                {
                    TestName = flowConfig.FlowName,
                    Success = success,
                    DurationSeconds = sw.ElapsedMilliseconds / 1000f,
                    TotalAssertions = _currentAssertions.Count,
                    PassedAssertions = _currentAssertions.Count(a => a.Passed),
                    FailedAssertions = _currentAssertions.Count(a => !a.Passed),
                    Assertions = new List<PlaytestAssertion>(_currentAssertions),
                    Logs = new List<string>(_currentLogs),
                    FailureReason = failureReason,
                    Metadata = new Dictionary<string, object>
                    {
                        { "completed_steps", completedSteps },
                        { "total_steps", flowConfig.Steps?.Count ?? 0 },
                        { "failed_step", failedStep }
                    }
                };

                await PersistPlaytestResultAsync(playtestResult);
                return BuildGraderResult(GRADER_ID_FLOW, playtestResult, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_FLOW, EvaluationTier.Behavioral,
                    $"Flow verification failed: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        /// <summary>
        /// Test if a specific interaction works as expected.
        /// Simpler than full flow - just checks one interaction.
        /// </summary>
        public async Task<GraderResult> TestInteractionAsync(InteractionTestConfig config)
        {
            var sw = Stopwatch.StartNew();

            if (config == null)
            {
                return GraderResult.Error(GRADER_ID_INTERACTION, EvaluationTier.Behavioral,
                    "InteractionTestConfig is null");
            }

#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                return GraderResult.Error(GRADER_ID_INTERACTION, EvaluationTier.Behavioral,
                    "Interaction tests require Play Mode.");
            }
#endif

            try
            {
                _currentAssertions.Clear();
                _currentLogs.Clear();
                _playtestStartTime = Time.realtimeSinceStartup;

                LogPlaytest($"Testing interaction: {config.InteractionType} on {config.TargetObject}");

                // Find the target object
                var targetGO = GameObject.Find(config.TargetObject);
                if (targetGO == null)
                {
                    sw.Stop();
                    return new GraderResult
                    {
                        GraderId = GRADER_ID_INTERACTION,
                        Tier = EvaluationTier.Behavioral,
                        Status = GradeStatus.Fail,
                        Score = 0f,
                        DurationMs = sw.ElapsedMilliseconds,
                        Summary = $"Target object '{config.TargetObject}' not found",
                        Issues = new List<GradingIssue>
                        {
                            new GradingIssue(IssueSeverity.Error, $"GameObject '{config.TargetObject}' not found in scene")
                        }
                    };
                }

                // Execute the interaction
                var interactionResult = await ExecuteInteractionAsync(targetGO, config.InteractionType);
                if (!interactionResult.success)
                {
                    RecordAssertion(PlaytestAssertion.Fail("interaction",
                        $"Interaction failed: {interactionResult.data}",
                        Time.realtimeSinceStartup - _playtestStartTime));
                }
                else
                {
                    RecordAssertion(PlaytestAssertion.Pass("interaction",
                        $"Interaction executed: {config.InteractionType}",
                        Time.realtimeSinceStartup - _playtestStartTime));
                }

                // Wait for expected outcome
                if (!string.IsNullOrEmpty(config.ExpectedOutcome))
                {
                    var outcomeResult = await WaitForConditionAsync(config.ExpectedOutcome, config.TimeoutSeconds);
                    if (!outcomeResult.success)
                    {
                        RecordAssertion(PlaytestAssertion.Fail("outcome",
                            $"Expected outcome not achieved: {config.ExpectedOutcome}",
                            Time.realtimeSinceStartup - _playtestStartTime));
                    }
                    else
                    {
                        RecordAssertion(PlaytestAssertion.Pass("outcome",
                            $"Expected outcome achieved: {config.ExpectedOutcome}",
                            Time.realtimeSinceStartup - _playtestStartTime));
                    }
                }

                sw.Stop();

                var success = _currentAssertions.All(a => a.Passed);
                var playtestResult = new PlaytestResult
                {
                    TestName = $"interaction:{config.TargetObject}:{config.InteractionType}",
                    Success = success,
                    DurationSeconds = sw.ElapsedMilliseconds / 1000f,
                    TotalAssertions = _currentAssertions.Count,
                    PassedAssertions = _currentAssertions.Count(a => a.Passed),
                    FailedAssertions = _currentAssertions.Count(a => !a.Passed),
                    Assertions = new List<PlaytestAssertion>(_currentAssertions),
                    Logs = new List<string>(_currentLogs),
                    Metadata = new Dictionary<string, object>
                    {
                        { "target_object", config.TargetObject },
                        { "interaction_type", config.InteractionType },
                        { "expected_outcome", config.ExpectedOutcome }
                    }
                };

                await PersistPlaytestResultAsync(playtestResult);
                return BuildGraderResult(GRADER_ID_INTERACTION, playtestResult, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_INTERACTION, EvaluationTier.Behavioral,
                    $"Interaction test failed: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        #region Private Helpers

        private async Task<(bool success, string error)> ExecuteInputSequenceAsync(InputSequence sequence, int timeoutSeconds)
        {
            if (sequence?.Actions == null || sequence.Actions.Count == 0)
            {
                return (true, null); // Empty sequence is success
            }

            LogPlaytest($"Executing input sequence: {sequence.Name} ({sequence.Actions.Count} actions)");

            try
            {
                foreach (var action in sequence.Actions)
                {
                    // Execute the input action
                    ExecuteInputAction(action);

                    // Wait between actions
                    if (sequence.DelayBetweenActions > 0)
                    {
                        await Task.Delay((int)(sequence.DelayBetweenActions * 1000));
                    }

                    // Additional wait for action duration
                    if (action.Duration > 0)
                    {
                        await Task.Delay((int)(action.Duration * 1000));

                        // Release if it was a hold action
                        if (action.Action == "hold" || action.Action == "down")
                        {
                            ReleaseInputAction(action);
                        }
                    }
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private void ExecuteInputAction(InputAction action)
        {
            if (action == null) return;

            switch (action.Type?.ToLowerInvariant())
            {
                case "key":
                case "keyboard":
                    ExecuteKeyAction(action);
                    break;

                case "mouse":
                    ExecuteMouseAction(action);
                    break;

                case "gamepad":
                    // Gamepad simulation not yet implemented
                    LogPlaytest($"Gamepad simulation not implemented: {action.Target}");
                    break;

                default:
                    LogPlaytest($"Unknown input type: {action.Type}");
                    break;
            }
        }

        private void ExecuteKeyAction(InputAction action)
        {
            if (string.IsNullOrEmpty(action.Target)) return;

            if (!Enum.TryParse<KeyCode>(action.Target, true, out var keyCode))
            {
                // Try common aliases
                keyCode = action.Target.ToLowerInvariant() switch
                {
                    "space" => KeyCode.Space,
                    "enter" => KeyCode.Return,
                    "escape" or "esc" => KeyCode.Escape,
                    "up" => KeyCode.UpArrow,
                    "down" => KeyCode.DownArrow,
                    "left" => KeyCode.LeftArrow,
                    "right" => KeyCode.RightArrow,
                    _ => KeyCode.None
                };
            }

            if (keyCode == KeyCode.None)
            {
                LogPlaytest($"Unknown key: {action.Target}");
                return;
            }

            switch (action.Action?.ToLowerInvariant())
            {
                case "press":
                    _inputSimulation.PressKey(keyCode);
                    break;
                case "down":
                case "hold":
                    _inputSimulation.KeyDown(keyCode);
                    break;
                case "up":
                case "release":
                    _inputSimulation.KeyUp(keyCode);
                    break;
                default:
                    _inputSimulation.PressKey(keyCode);
                    break;
            }
        }

        private void ExecuteMouseAction(InputAction action)
        {
            // Parse target as coordinates or named position
            var screenPos = ParseMouseTarget(action.Target);
            int button = 0;

            switch (action.Action?.ToLowerInvariant())
            {
                case "click":
                case "press":
                    _inputSimulation.MouseClick(screenPos, button);
                    break;
                case "rightclick":
                case "right_click":
                    _inputSimulation.MouseClick(screenPos, 1);
                    break;
                case "move":
                    _inputSimulation.MouseMove(screenPos);
                    break;
                case "scroll":
                    float scrollAmount = 1f;
                    if (float.TryParse(action.Target, out var parsed))
                        scrollAmount = parsed;
                    _inputSimulation.MouseScroll(scrollAmount);
                    break;
                default:
                    _inputSimulation.MouseClick(screenPos, button);
                    break;
            }
        }

        private Vector2 ParseMouseTarget(string target)
        {
            if (string.IsNullOrEmpty(target))
                return new Vector2(Screen.width / 2f, Screen.height / 2f);

            // Try parsing as "x,y"
            var parts = target.Split(',');
            if (parts.Length == 2 &&
                float.TryParse(parts[0].Trim(), out var x) &&
                float.TryParse(parts[1].Trim(), out var y))
            {
                return new Vector2(x, y);
            }

            // Named positions
            return target.ToLowerInvariant() switch
            {
                "center" => new Vector2(Screen.width / 2f, Screen.height / 2f),
                "top" => new Vector2(Screen.width / 2f, Screen.height * 0.9f),
                "bottom" => new Vector2(Screen.width / 2f, Screen.height * 0.1f),
                "left" => new Vector2(Screen.width * 0.1f, Screen.height / 2f),
                "right" => new Vector2(Screen.width * 0.9f, Screen.height / 2f),
                _ => new Vector2(Screen.width / 2f, Screen.height / 2f)
            };
        }

        private void ReleaseInputAction(InputAction action)
        {
            if (action?.Type?.ToLowerInvariant() != "key") return;

            if (Enum.TryParse<KeyCode>(action.Target, true, out var keyCode))
            {
                _inputSimulation.KeyUp(keyCode);
            }
        }

        private async Task<(bool success, string error)> ExecuteFlowActionAsync(string action)
        {
            if (string.IsNullOrEmpty(action))
                return (true, null);

            // Parse action format: "type:target:params"
            // Examples: "click:PlayButton", "key:Space", "wait:2"
            var parts = action.Split(':');
            var actionType = parts[0].ToLowerInvariant();
            var target = parts.Length > 1 ? parts[1] : null;

            try
            {
                switch (actionType)
                {
                    case "click":
                        if (!string.IsNullOrEmpty(target))
                        {
                            var go = GameObject.Find(target);
                            if (go != null)
                            {
                                // Click on the object's screen position
                                var screenPos = Camera.main?.WorldToScreenPoint(go.transform.position) ?? Vector3.zero;
                                _inputSimulation.MouseClick(new Vector2(screenPos.x, screenPos.y), 0);
                            }
                        }
                        break;

                    case "key":
                    case "press":
                        if (Enum.TryParse<KeyCode>(target, true, out var keyCode))
                        {
                            _inputSimulation.PressKey(keyCode);
                        }
                        break;

                    case "wait":
                        if (float.TryParse(target, out var waitTime))
                        {
                            await Task.Delay((int)(waitTime * 1000));
                        }
                        break;

                    case "move":
                        if (!string.IsNullOrEmpty(target))
                        {
                            var pos = ParseMouseTarget(target);
                            _inputSimulation.MouseMove(pos);
                        }
                        break;

                    default:
                        LogPlaytest($"Unknown flow action type: {actionType}");
                        break;
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<(bool success, string data)> ExecuteInteractionAsync(GameObject target, string interactionType)
        {
            if (target == null)
                return (false, "Target is null");

            try
            {
                switch (interactionType?.ToLowerInvariant())
                {
                    case "click":
                        // Click on the object
                        var screenPos = Camera.main?.WorldToScreenPoint(target.transform.position) ?? Vector3.zero;
                        _inputSimulation.MouseClick(new Vector2(screenPos.x, screenPos.y), 0);
                        break;

                    case "hover":
                        var hoverPos = Camera.main?.WorldToScreenPoint(target.transform.position) ?? Vector3.zero;
                        _inputSimulation.MouseMove(new Vector2(hoverPos.x, hoverPos.y));
                        break;

                    case "approach":
                        // This would require player movement - log for now
                        LogPlaytest($"Approach interaction requested for {target.name}");
                        break;

                    default:
                        LogPlaytest($"Unknown interaction type: {interactionType}");
                        break;
                }

                // Small delay to let Unity process the input
                await Task.Delay(100);

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<(bool success, string data)> WaitForConditionAsync(string condition, int timeoutSeconds)
        {
            if (string.IsNullOrEmpty(condition))
                return (true, null);

            var startTime = Time.realtimeSinceStartup;
            var timeout = timeoutSeconds > 0 ? timeoutSeconds : 10;

            while (Time.realtimeSinceStartup - startTime < timeout)
            {
                if (EvaluateCondition(condition))
                {
                    return (true, null);
                }

                await Task.Delay(100); // Poll every 100ms
            }

            return (false, $"Timeout after {timeout}s waiting for: {condition}");
        }

        private bool EvaluateCondition(string condition)
        {
            if (string.IsNullOrEmpty(condition))
                return true;

            // Parse condition format: "type:target:value"
            // Examples: "exists:GameOverPanel", "active:Player", "value:Score.text:>100"
            var parts = condition.Split(':');
            var conditionType = parts[0].ToLowerInvariant();
            var target = parts.Length > 1 ? parts[1] : null;
            var value = parts.Length > 2 ? parts[2] : null;

            switch (conditionType)
            {
                case "exists":
                    return GameObject.Find(target) != null;

                case "not_exists":
                case "notexists":
                    return GameObject.Find(target) == null;

                case "active":
                    var activeGo = GameObject.Find(target);
                    return activeGo != null && activeGo.activeInHierarchy;

                case "inactive":
                    var inactiveGo = GameObject.Find(target);
                    return inactiveGo == null || !inactiveGo.activeInHierarchy;

                case "component":
                    if (string.IsNullOrEmpty(value)) return false;
                    var compGo = GameObject.Find(target);
                    return compGo != null && compGo.GetComponent(value) != null;

                case "value":
                    return EvaluateValueCondition(target, value);

                case "tag":
                    try
                    {
                        return GameObject.FindGameObjectsWithTag(target).Length > 0;
                    }
                    catch
                    {
                        return false;
                    }

                case "count":
                    return EvaluateCountCondition(target, value);

                default:
                    // Default: check if object exists and is active
                    var go = GameObject.Find(condition);
                    return go != null && go.activeInHierarchy;
            }
        }

        private bool EvaluateValueCondition(string path, string expectedValue)
        {
            if (string.IsNullOrEmpty(path)) return false;

            // Path format: "ObjectName.ComponentType.FieldName" or "ObjectName.property"
            var pathParts = path.Split('.');
            if (pathParts.Length < 2) return false;

            var go = GameObject.Find(pathParts[0]);
            if (go == null) return false;

            try
            {
                object currentValue = null;

                if (pathParts.Length >= 3)
                {
                    // ObjectName.ComponentType.FieldName
                    var component = go.GetComponent(pathParts[1]);
                    if (component == null) return false;

                    var type = component.GetType();
                    var field = type.GetField(pathParts[2], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var prop = type.GetProperty(pathParts[2], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (field != null)
                        currentValue = field.GetValue(component);
                    else if (prop != null && prop.CanRead)
                        currentValue = prop.GetValue(component);
                }
                else
                {
                    // ObjectName.property (check Transform properties)
                    currentValue = pathParts[1].ToLowerInvariant() switch
                    {
                        "active" => go.activeSelf,
                        "name" => go.name,
                        "tag" => go.tag,
                        "layer" => go.layer,
                        "x" => go.transform.position.x,
                        "y" => go.transform.position.y,
                        "z" => go.transform.position.z,
                        _ => null
                    };
                }

                if (currentValue == null && string.IsNullOrEmpty(expectedValue))
                    return true;

                return CompareValues(currentValue, expectedValue);
            }
            catch
            {
                return false;
            }
        }

        private bool CompareValues(object actual, string expected)
        {
            if (actual == null) return string.IsNullOrEmpty(expected);
            if (string.IsNullOrEmpty(expected)) return false;

            var actualStr = actual.ToString();

            // Check for comparison operators
            if (expected.StartsWith(">"))
            {
                var numStr = expected.Substring(1);
                if (double.TryParse(actualStr, out var actualNum) && double.TryParse(numStr, out var expectedNum))
                    return actualNum > expectedNum;
            }
            else if (expected.StartsWith("<"))
            {
                var numStr = expected.Substring(1);
                if (double.TryParse(actualStr, out var actualNum) && double.TryParse(numStr, out var expectedNum))
                    return actualNum < expectedNum;
            }
            else if (expected.StartsWith(">="))
            {
                var numStr = expected.Substring(2);
                if (double.TryParse(actualStr, out var actualNum) && double.TryParse(numStr, out var expectedNum))
                    return actualNum >= expectedNum;
            }
            else if (expected.StartsWith("<="))
            {
                var numStr = expected.Substring(2);
                if (double.TryParse(actualStr, out var actualNum) && double.TryParse(numStr, out var expectedNum))
                    return actualNum <= expectedNum;
            }
            else if (expected.StartsWith("!="))
            {
                return actualStr != expected.Substring(2);
            }

            // Default: exact match
            return actualStr.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }

        private bool EvaluateCountCondition(string target, string expectedCount)
        {
            if (string.IsNullOrEmpty(target)) return false;

            int actualCount = 0;

            // Check if target is a tag
            try
            {
                actualCount = GameObject.FindGameObjectsWithTag(target).Length;
            }
            catch
            {
                // Not a valid tag, try as component type
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.Name == target && typeof(Component).IsAssignableFrom(t));

                if (type != null)
                {
                    actualCount = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None).Length;
                }
            }

            return CompareValues(actualCount, expectedCount);
        }

        private void RecordAssertion(PlaytestAssertion assertion)
        {
            _currentAssertions.Add(assertion);
            LogPlaytest($"[{(assertion.Passed ? "PASS" : "FAIL")}] {assertion.Name}: {assertion.Message}");
        }

        private void LogPlaytest(string message)
        {
            var timestamp = Time.realtimeSinceStartup - _playtestStartTime;
            var logEntry = $"[{timestamp:F2}s] {message}";
            _currentLogs.Add(logEntry);
            Debug.Log($"[BehavioralGrader] {logEntry}");
        }

        private async Task PersistPlaytestResultAsync(PlaytestResult result)
        {
            if (_hooksWriter == null) return;

            try
            {
                var filename = $"playtest_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{result.TestName.Replace("/", "_").Replace(":", "_")}.json";
                await _hooksWriter.WriteAsync("validation", filename, result);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BehavioralGrader] Failed to persist playtest result: {ex.Message}");
            }
        }

        private GraderResult BuildGraderResult(string graderId, PlaytestResult playtestResult, long durationMs)
        {
            var issues = new List<GradingIssue>();

            foreach (var assertion in playtestResult.Assertions.Where(a => !a.Passed))
            {
                issues.Add(new GradingIssue
                {
                    Severity = IssueSeverity.Error,
                    Code = "PLAYTEST_ASSERTION_FAILED",
                    Message = assertion.Message,
                    Metadata = new Dictionary<string, object>
                    {
                        { "assertion_name", assertion.Name },
                        { "timestamp", assertion.TimestampSeconds }
                    }
                });
            }

            var score = playtestResult.TotalAssertions > 0
                ? (float)playtestResult.PassedAssertions / playtestResult.TotalAssertions
                : (playtestResult.Success ? 1.0f : 0.0f);

            return new GraderResult
            {
                GraderId = graderId,
                Tier = EvaluationTier.Behavioral,
                Status = playtestResult.Success ? GradeStatus.Pass : GradeStatus.Fail,
                Score = score,
                Weight = 1.0f,
                DurationMs = durationMs,
                Summary = playtestResult.Success
                    ? $"Playtest passed: {playtestResult.PassedAssertions}/{playtestResult.TotalAssertions} assertions"
                    : $"Playtest failed: {playtestResult.FailureReason ?? $"{playtestResult.FailedAssertions} assertions failed"}",
                Issues = issues,
                Metadata = new Dictionary<string, object>
                {
                    { "test_name", playtestResult.TestName },
                    { "total_assertions", playtestResult.TotalAssertions },
                    { "passed_assertions", playtestResult.PassedAssertions },
                    { "failed_assertions", playtestResult.FailedAssertions },
                    { "duration_seconds", playtestResult.DurationSeconds },
                    { "logs", playtestResult.Logs }
                }
            };
        }

        #endregion
    }
}
