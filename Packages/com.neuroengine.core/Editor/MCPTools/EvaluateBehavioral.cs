#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using NeuroEngine.Services;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool for Tier 3 (Behavioral) evaluation.
    /// Runs automated playtests with input simulation and game state verification.
    /// Requires Play Mode to execute.
    /// </summary>
    [McpForUnityTool("evaluate_behavioral", Description = "Runs Tier 3 (Behavioral) evaluation. Actions: 'playtest', 'flow', 'interaction'. Requires Play Mode. Simulates player input and verifies game behavior through automated testing.")]
    public static class EvaluateBehavioral
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant() ?? "playtest";

            // Check Play Mode first
            if (!EditorApplication.isPlaying)
            {
                return new ErrorResponse("Behavioral evaluation requires Play Mode. Use manage_editor(action='play') first.", new
                {
                    required = "Play Mode",
                    hint = "Call manage_editor with action='play' to enter Play Mode before running behavioral tests"
                });
            }

            var grader = EditorServiceLocator.Get<IBehavioralGrader>();
            if (grader == null)
            {
                // Try creating directly
                grader = new BehavioralGraderService();
                EditorServiceLocator.Register<IBehavioralGrader>(grader);
            }

            try
            {
                switch (action)
                {
                    case "playtest":
                    case "run":
                        return HandlePlaytest(@params, grader);

                    case "flow":
                    case "verify_flow":
                        return HandleFlow(@params, grader);

                    case "interaction":
                    case "test_interaction":
                        return HandleInteraction(@params, grader);

                    case "check_condition":
                    case "condition":
                        return HandleCheckCondition(@params);

                    default:
                        return new ErrorResponse($"Unknown action '{action}'. Use: playtest, flow, interaction, check_condition");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Behavioral evaluation failed: {e.Message}", new
                {
                    exception_type = e.GetType().Name,
                    stack_trace = e.StackTrace
                });
            }
        }

        private static object HandlePlaytest(JObject @params, IBehavioralGrader grader)
        {
            var config = BuildPlaytestConfig(@params);
            var result = grader.RunPlaytestAsync(config).GetAwaiter().GetResult();
            return FormatGraderResult(result, "playtest");
        }

        private static object HandleFlow(JObject @params, IBehavioralGrader grader)
        {
            var config = BuildFlowConfig(@params);
            if (config.Steps == null || config.Steps.Count == 0)
            {
                return new ErrorResponse("Flow verification requires 'steps' parameter. Provide an array of flow steps.", new
                {
                    example = new
                    {
                        flow_name = "tutorial_flow",
                        steps = new[]
                        {
                            new { description = "Click play button", action = "click:PlayButton", expected_result = "exists:GameScene" }
                        }
                    }
                });
            }

            var result = grader.VerifyFlowAsync(config).GetAwaiter().GetResult();
            return FormatGraderResult(result, "flow");
        }

        private static object HandleInteraction(JObject @params, IBehavioralGrader grader)
        {
            var config = BuildInteractionConfig(@params);
            if (string.IsNullOrEmpty(config.TargetObject))
            {
                return new ErrorResponse("Interaction test requires 'target' parameter specifying the GameObject name.");
            }

            var result = grader.TestInteractionAsync(config).GetAwaiter().GetResult();
            return FormatGraderResult(result, "interaction");
        }

        private static object HandleCheckCondition(JObject @params)
        {
            string condition = @params["condition"]?.ToString();
            if (string.IsNullOrEmpty(condition))
            {
                return new ErrorResponse("Required parameter 'condition' is missing.", new
                {
                    condition_formats = new[]
                    {
                        "exists:GameObjectName",
                        "active:GameObjectName",
                        "not_exists:GameObjectName",
                        "component:GameObjectName:ComponentType",
                        "value:ObjectName.Component.Field:expectedValue",
                        "tag:TagName",
                        "count:TagOrComponent:>5"
                    }
                });
            }

            // Use the grader's condition evaluation
            var grader = new BehavioralGraderService();

            // We need to access the private method, so we'll replicate the logic here
            bool conditionMet = EvaluateConditionPublic(condition);

            return new SuccessResponse(conditionMet ? "Condition met" : "Condition not met", new
            {
                condition = condition,
                result = conditionMet,
                timestamp = Time.time,
                frame = Time.frameCount
            });
        }

        /// <summary>
        /// Public condition evaluation for direct MCP use.
        /// </summary>
        private static bool EvaluateConditionPublic(string condition)
        {
            if (string.IsNullOrEmpty(condition))
                return true;

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
                    return EvaluateCountConditionPublic(target, value);

                default:
                    var go = GameObject.Find(condition);
                    return go != null && go.activeInHierarchy;
            }
        }

        private static bool EvaluateCountConditionPublic(string target, string expectedCount)
        {
            if (string.IsNullOrEmpty(target)) return false;

            int actualCount = 0;
            try
            {
                actualCount = GameObject.FindGameObjectsWithTag(target).Length;
            }
            catch
            {
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.Name == target && typeof(Component).IsAssignableFrom(t));

                if (type != null)
                {
                    actualCount = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None).Length;
                }
            }

            if (string.IsNullOrEmpty(expectedCount)) return actualCount > 0;

            if (expectedCount.StartsWith(">"))
            {
                if (int.TryParse(expectedCount.Substring(1), out var expected))
                    return actualCount > expected;
            }
            else if (expectedCount.StartsWith("<"))
            {
                if (int.TryParse(expectedCount.Substring(1), out var expected))
                    return actualCount < expected;
            }
            else if (expectedCount.StartsWith(">="))
            {
                if (int.TryParse(expectedCount.Substring(2), out var expected))
                    return actualCount >= expected;
            }
            else if (expectedCount.StartsWith("<="))
            {
                if (int.TryParse(expectedCount.Substring(2), out var expected))
                    return actualCount <= expected;
            }
            else if (int.TryParse(expectedCount, out var expected))
            {
                return actualCount == expected;
            }

            return false;
        }

        private static PlaytestConfig BuildPlaytestConfig(JObject @params)
        {
            var config = new PlaytestConfig
            {
                ScenePath = @params["scene_path"]?.ToString(),
                TimeoutSeconds = @params["timeout"]?.Value<int>() ?? 30,
                RecordVideo = @params["record_video"]?.Value<bool>() ?? false
            };

            // Parse input sequences
            var inputsJson = @params["inputs"] ?? @params["input_scripts"];
            if (inputsJson != null)
            {
                config.InputScripts = new List<InputSequence>();
                foreach (var seqJson in inputsJson)
                {
                    var sequence = new InputSequence
                    {
                        Name = seqJson["name"]?.ToString() ?? "sequence",
                        DelayBetweenActions = seqJson["delay"]?.Value<float>() ?? 0.1f,
                        Actions = new List<InputAction>()
                    };

                    var actionsJson = seqJson["actions"];
                    if (actionsJson != null)
                    {
                        foreach (var actionJson in actionsJson)
                        {
                            sequence.Actions.Add(new InputAction
                            {
                                Type = actionJson["type"]?.ToString() ?? "key",
                                Action = actionJson["action"]?.ToString() ?? "press",
                                Target = actionJson["target"]?.ToString() ?? actionJson["key"]?.ToString(),
                                Duration = actionJson["duration"]?.Value<float>() ?? 0f
                            });
                        }
                    }

                    config.InputScripts.Add(sequence);
                }
            }

            // Parse success conditions
            var successJson = @params["success_conditions"] ?? @params["success"];
            if (successJson != null)
            {
                config.SuccessConditions = successJson.ToObject<List<string>>();
            }

            // Parse failure conditions
            var failureJson = @params["failure_conditions"] ?? @params["failure"];
            if (failureJson != null)
            {
                config.FailureConditions = failureJson.ToObject<List<string>>();
            }

            return config;
        }

        private static GameFlowConfig BuildFlowConfig(JObject @params)
        {
            var config = new GameFlowConfig
            {
                FlowName = @params["flow_name"]?.ToString() ?? @params["name"]?.ToString() ?? "flow",
                StartScene = @params["start_scene"]?.ToString() ?? @params["scene"]?.ToString(),
                TimeoutSeconds = @params["timeout"]?.Value<int>() ?? 60,
                Steps = new List<FlowStep>()
            };

            var stepsJson = @params["steps"];
            if (stepsJson != null)
            {
                foreach (var stepJson in stepsJson)
                {
                    config.Steps.Add(new FlowStep
                    {
                        Description = stepJson["description"]?.ToString() ?? stepJson["desc"]?.ToString(),
                        Action = stepJson["action"]?.ToString(),
                        ExpectedResult = stepJson["expected_result"]?.ToString() ?? stepJson["expected"]?.ToString(),
                        MaxWaitSeconds = stepJson["max_wait"]?.Value<int>() ?? stepJson["wait"]?.Value<int>() ?? 10
                    });
                }
            }

            return config;
        }

        private static InteractionTestConfig BuildInteractionConfig(JObject @params)
        {
            return new InteractionTestConfig
            {
                TargetObject = @params["target"]?.ToString() ?? @params["object"]?.ToString(),
                InteractionType = @params["interaction"]?.ToString() ?? @params["type"]?.ToString() ?? "click",
                ExpectedOutcome = @params["expected"]?.ToString() ?? @params["expected_outcome"]?.ToString(),
                TimeoutSeconds = @params["timeout"]?.Value<int>() ?? 10
            };
        }

        private static object FormatGraderResult(GraderResult result, string testType)
        {
            var response = new
            {
                grader_id = result.GraderId,
                tier = "behavioral",
                test_type = testType,
                status = result.Status.ToString().ToLowerInvariant(),
                score = result.Score,
                is_blocking = result.IsBlocking,
                duration_ms = result.DurationMs,
                summary = result.Summary,
                issue_count = result.Issues?.Count ?? 0,
                issues = result.Issues?.ConvertAll(i => new
                {
                    severity = i.Severity.ToString().ToLowerInvariant(),
                    code = i.Code,
                    message = i.Message,
                    metadata = i.Metadata
                }),
                metadata = result.Metadata
            };

            if (result.Status == GradeStatus.Pass)
            {
                return new SuccessResponse($"Behavioral evaluation: {result.Status}", response);
            }
            else
            {
                return new SuccessResponse($"Behavioral evaluation: {result.Status} - {result.Summary}", response);
            }
        }
    }
}
#endif
