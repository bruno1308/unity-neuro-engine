using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NeuroEngine.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Tier 2: State grading - JSON snapshots, data integrity.
    /// Wraps existing Layer 2 services (ISceneStateCapture, IValidationRules).
    /// </summary>
    public class StateGraderService : IStateGrader
    {
        private readonly ISceneStateCapture _sceneCapture;
        private readonly IValidationRules _validationRules;

        private const string GRADER_ID_EXPECTATIONS = "state.expectations";
        private const string GRADER_ID_BASELINE = "state.baseline";
        private const string GRADER_ID_VALIDATION = "state.validation-rules";

        public StateGraderService(
            ISceneStateCapture sceneCapture,
            IValidationRules validationRules)
        {
            _sceneCapture = sceneCapture;
            _validationRules = validationRules;
        }

        /// <summary>
        /// Parameterless constructor for editor/standalone use.
        /// </summary>
        public StateGraderService()
        {
            _sceneCapture = new SceneStateCaptureService();
            _validationRules = new ValidationRulesEngine();
        }

        public async Task<SceneSnapshot> CaptureSnapshotAsync(string scenePath = null)
        {
            return await Task.Run(() => _sceneCapture.CaptureScene(scenePath));
        }

        public GraderResult ValidateExpectations(List<StateExpectation> expectations, string scenePath = null)
        {
            var sw = Stopwatch.StartNew();
            var issues = new List<GradingIssue>();

            if (expectations == null || expectations.Count == 0)
            {
                sw.Stop();
                return GraderResult.Skipped(GRADER_ID_EXPECTATIONS, EvaluationTier.State,
                    "No expectations provided") with { DurationMs = sw.ElapsedMilliseconds };
            }

            try
            {
                var snapshot = _sceneCapture.CaptureScene(scenePath);
                var passedCount = 0;

                foreach (var expectation in expectations)
                {
                    var result = ValidateSingleExpectation(snapshot, expectation);
                    if (result.passed)
                    {
                        passedCount++;
                    }
                    else
                    {
                        issues.Add(new GradingIssue
                        {
                            Severity = IssueSeverity.Error,
                            Code = "STATE_EXPECTATION_FAILED",
                            Message = $"Expectation failed: {expectation.Description ?? expectation.PropertyPath}",
                            ObjectPath = expectation.GameObjectPath,
                            Metadata = new Dictionary<string, object>
                            {
                                { "expected", expectation.ExpectedValue },
                                { "actual", result.actualValue },
                                { "mode", expectation.Mode.ToString() }
                            }
                        });
                    }
                }

                sw.Stop();
                var score = (float)passedCount / expectations.Count;

                if (issues.Count == 0)
                {
                    return new GraderResult
                    {
                        GraderId = GRADER_ID_EXPECTATIONS,
                        Tier = EvaluationTier.State,
                        Status = GradeStatus.Pass,
                        Score = 1.0f,
                        Weight = 1.0f,
                        DurationMs = sw.ElapsedMilliseconds,
                        Summary = $"All {expectations.Count} expectations passed"
                    };
                }

                return new GraderResult
                {
                    GraderId = GRADER_ID_EXPECTATIONS,
                    Tier = EvaluationTier.State,
                    Status = GradeStatus.Fail,
                    Score = score,
                    Weight = 1.0f,
                    DurationMs = sw.ElapsedMilliseconds,
                    Summary = $"{passedCount}/{expectations.Count} expectations passed",
                    Issues = issues
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return GraderResult.Error(GRADER_ID_EXPECTATIONS, EvaluationTier.State,
                    $"Failed to validate expectations: {ex.Message}") with { DurationMs = sw.ElapsedMilliseconds };
            }
        }

        public GraderResult CompareToBaseline(SceneSnapshot baseline, string scenePath = null)
        {
            var sw = Stopwatch.StartNew();

            if (baseline == null)
            {
                sw.Stop();
                return GraderResult.Skipped(GRADER_ID_BASELINE, EvaluationTier.State,
                    "No baseline snapshot provided") with { DurationMs = sw.ElapsedMilliseconds };
            }

            try
            {
                var current = _sceneCapture.CaptureScene(scenePath);
                var issues = new List<GradingIssue>();
                var differences = 0;
                var criticalDifferences = 0;

                // Compare GameObjects
                var baselineObjects = baseline.GameObjects.ToDictionary(g => g.Path, g => g);
                var currentObjects = current.GameObjects.ToDictionary(g => g.Path, g => g);

                // Check for removed objects
                foreach (var path in baselineObjects.Keys.Except(currentObjects.Keys))
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Code = "STATE_OBJECT_REMOVED",
                        Message = $"GameObject removed from scene",
                        ObjectPath = path
                    });
                    differences++;
                }

                // Check for added objects
                foreach (var path in currentObjects.Keys.Except(baselineObjects.Keys))
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Info,
                        Code = "STATE_OBJECT_ADDED",
                        Message = $"New GameObject added to scene",
                        ObjectPath = path
                    });
                    differences++;
                }

                // Compare existing objects
                foreach (var path in baselineObjects.Keys.Intersect(currentObjects.Keys))
                {
                    var baselineObj = baselineObjects[path];
                    var currentObj = currentObjects[path];

                    // Check active state
                    if (baselineObj.IsActive != currentObj.IsActive)
                    {
                        issues.Add(new GradingIssue
                        {
                            Severity = IssueSeverity.Warning,
                            Code = "STATE_ACTIVE_CHANGED",
                            Message = $"Active state changed: {baselineObj.IsActive} -> {currentObj.IsActive}",
                            ObjectPath = path
                        });
                        differences++;
                    }

                    // Check component count
                    if (baselineObj.Components.Count != currentObj.Components.Count)
                    {
                        issues.Add(new GradingIssue
                        {
                            Severity = IssueSeverity.Warning,
                            Code = "STATE_COMPONENTS_CHANGED",
                            Message = $"Component count changed: {baselineObj.Components.Count} -> {currentObj.Components.Count}",
                            ObjectPath = path
                        });
                        differences++;
                        criticalDifferences++;
                    }
                }

                sw.Stop();

                if (differences == 0)
                {
                    return new GraderResult
                    {
                        GraderId = GRADER_ID_BASELINE,
                        Tier = EvaluationTier.State,
                        Status = GradeStatus.Pass,
                        Score = 1.0f,
                        Weight = 0.8f,
                        DurationMs = sw.ElapsedMilliseconds,
                        Summary = "Scene matches baseline"
                    };
                }

                var status = criticalDifferences > 0 ? GradeStatus.Fail : GradeStatus.Warning;
                var totalObjects = Math.Max(baselineObjects.Count, currentObjects.Count);
                var score = 1f - ((float)criticalDifferences / Math.Max(totalObjects, 1));

                return new GraderResult
                {
                    GraderId = GRADER_ID_BASELINE,
                    Tier = EvaluationTier.State,
                    Status = status,
                    Score = Math.Max(0, score),
                    Weight = 0.8f,
                    DurationMs = sw.ElapsedMilliseconds,
                    Summary = $"Found {differences} differences from baseline ({criticalDifferences} critical)",
                    Issues = issues,
                    Metadata = new Dictionary<string, object>
                    {
                        { "total_differences", differences },
                        { "critical_differences", criticalDifferences },
                        { "baseline_objects", baselineObjects.Count },
                        { "current_objects", currentObjects.Count }
                    }
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return GraderResult.Error(GRADER_ID_BASELINE, EvaluationTier.State,
                    $"Failed to compare to baseline: {ex.Message}") with { DurationMs = sw.ElapsedMilliseconds };
            }
        }

        public GraderResult RunValidationRules(string scenePath = null)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var validationResult = _validationRules.ValidateScene(scenePath);
                var issues = new List<GradingIssue>();

                foreach (var violation in validationResult.Violations)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = MapSeverity(violation.Severity),
                        Code = violation.RuleId,
                        Message = violation.Message,
                        ObjectPath = violation.ObjectPath,
                        SuggestedFix = violation.SuggestedFix
                    });
                }

                sw.Stop();

                if (issues.Count == 0)
                {
                    return new GraderResult
                    {
                        GraderId = GRADER_ID_VALIDATION,
                        Tier = EvaluationTier.State,
                        Status = GradeStatus.Pass,
                        Score = 1.0f,
                        Weight = 1.0f,
                        DurationMs = sw.ElapsedMilliseconds,
                        Summary = $"All {validationResult.RulesChecked} validation rules passed",
                        Metadata = new Dictionary<string, object>
                        {
                            { "rules_checked", validationResult.RulesChecked },
                            { "objects_validated", validationResult.ObjectsValidated }
                        }
                    };
                }

                var errorCount = issues.Count(i => i.Severity == IssueSeverity.Error || i.Severity == IssueSeverity.Critical);
                var status = errorCount > 0 ? GradeStatus.Fail : GradeStatus.Warning;
                var score = 1f - ((float)errorCount / Math.Max(validationResult.RulesChecked, 1));

                return new GraderResult
                {
                    GraderId = GRADER_ID_VALIDATION,
                    Tier = EvaluationTier.State,
                    Status = status,
                    Score = Math.Max(0, score),
                    Weight = 1.0f,
                    DurationMs = sw.ElapsedMilliseconds,
                    Summary = $"Found {issues.Count} validation violations ({errorCount} errors)",
                    Issues = issues,
                    Metadata = new Dictionary<string, object>
                    {
                        { "rules_checked", validationResult.RulesChecked },
                        { "objects_validated", validationResult.ObjectsValidated }
                    }
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return GraderResult.Error(GRADER_ID_VALIDATION, EvaluationTier.State,
                    $"Failed to run validation rules: {ex.Message}") with { DurationMs = sw.ElapsedMilliseconds };
            }
        }

        private (bool passed, object actualValue) ValidateSingleExpectation(SceneSnapshot snapshot, StateExpectation expectation)
        {
            // Find the GameObject
            var gameObject = snapshot.GameObjects.FirstOrDefault(g =>
                g.Path == expectation.GameObjectPath ||
                g.Name == expectation.GameObjectPath);

            if (gameObject == null)
            {
                return (expectation.Mode == StateComparisonMode.IsNull, null);
            }

            // Find the component
            ComponentSnapshot component = null;
            if (!string.IsNullOrEmpty(expectation.ComponentType))
            {
                component = gameObject.Components.FirstOrDefault(c =>
                    c.Type == expectation.ComponentType ||
                    c.Type.EndsWith("." + expectation.ComponentType));
            }

            // Get the property value
            object actualValue = null;
            if (component != null && !string.IsNullOrEmpty(expectation.PropertyPath))
            {
                actualValue = GetPropertyValue(component, expectation.PropertyPath);
            }
            else if (string.IsNullOrEmpty(expectation.ComponentType))
            {
                // Direct GameObject property
                actualValue = GetGameObjectProperty(gameObject, expectation.PropertyPath);
            }

            // Compare based on mode
            return (CompareValues(actualValue, expectation.ExpectedValue, expectation.Mode), actualValue);
        }

        private object GetPropertyValue(ComponentSnapshot component, string propertyPath)
        {
            if (component.SerializedFields == null)
                return null;

            // Handle nested paths like "settings.speed"
            var parts = propertyPath.Split('.');
            object current = component.SerializedFields;

            foreach (var part in parts)
            {
                if (current is Dictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(part, out current))
                        return null;
                }
                else if (current is JObject jObj)
                {
                    current = jObj[part];
                    if (current == null)
                        return null;
                }
                else
                {
                    return null;
                }
            }

            return current;
        }

        private object GetGameObjectProperty(GameObjectSnapshot gameObject, string propertyPath)
        {
            return propertyPath.ToLowerInvariant() switch
            {
                "isactive" or "active" => gameObject.IsActive,
                "layer" => gameObject.Layer,
                "tag" => gameObject.Tag,
                "name" => gameObject.Name,
                "position" or "position.x" or "position.y" or "position.z" => gameObject.Position,
                "rotation" => gameObject.Rotation,
                "scale" or "localscale" => gameObject.Scale,
                _ => null
            };
        }

        private bool CompareValues(object actual, object expected, StateComparisonMode mode)
        {
            return mode switch
            {
                StateComparisonMode.IsNull => actual == null,
                StateComparisonMode.NotNull => actual != null,
                StateComparisonMode.Exact => Equals(actual?.ToString(), expected?.ToString()),
                StateComparisonMode.Contains => actual?.ToString()?.Contains(expected?.ToString() ?? "") ?? false,
                StateComparisonMode.Regex => expected != null && actual != null &&
                    Regex.IsMatch(actual.ToString(), expected.ToString()),
                StateComparisonMode.Range => CompareRange(actual, expected),
                _ => false
            };
        }

        private bool CompareRange(object actual, object expected)
        {
            // Expected format: "min,max" or [min, max]
            if (actual == null || expected == null)
                return false;

            try
            {
                var actualNum = Convert.ToDouble(actual);
                var expectedStr = expected.ToString();

                if (expectedStr.Contains(","))
                {
                    var parts = expectedStr.Split(',');
                    var min = double.Parse(parts[0].Trim('[', ' '));
                    var max = double.Parse(parts[1].Trim(']', ' '));
                    return actualNum >= min && actualNum <= max;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private IssueSeverity MapSeverity(string severity)
        {
            return severity?.ToLowerInvariant() switch
            {
                "critical" => IssueSeverity.Critical,
                "error" => IssueSeverity.Error,
                "warning" => IssueSeverity.Warning,
                "info" => IssueSeverity.Info,
                _ => IssueSeverity.Warning
            };
        }
    }
}
