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
    /// Flattened view of a GameObject with computed path.
    /// Used internally for snapshot traversal.
    /// </summary>
    internal class FlattenedGameObject
    {
        public string Path;
        public GameObjectSnapshot Snapshot;
    }

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
            // Note: scenePath parameter is unused - ISceneStateCapture.CaptureScene()
            // captures the currently active scene
            return await Task.Run(() => _sceneCapture.CaptureScene());
        }

        /// <summary>
        /// Flatten the hierarchical RootObjects into a list with computed paths.
        /// </summary>
        private List<FlattenedGameObject> FlattenHierarchy(SceneSnapshot snapshot)
        {
            var result = new List<FlattenedGameObject>();
            if (snapshot?.RootObjects == null) return result;

            foreach (var root in snapshot.RootObjects)
            {
                FlattenRecursive(root, "", result);
            }
            return result;
        }

        private void FlattenRecursive(GameObjectSnapshot obj, string parentPath, List<FlattenedGameObject> result)
        {
            if (obj == null) return;

            var path = string.IsNullOrEmpty(parentPath) ? obj.Name : $"{parentPath}/{obj.Name}";
            result.Add(new FlattenedGameObject { Path = path, Snapshot = obj });

            if (obj.Children != null)
            {
                foreach (var child in obj.Children)
                {
                    FlattenRecursive(child, path, result);
                }
            }
        }

        public GraderResult ValidateExpectations(List<StateExpectation> expectations, string scenePath = null)
        {
            var sw = Stopwatch.StartNew();
            var issues = new List<GradingIssue>();

            if (expectations == null || expectations.Count == 0)
            {
                sw.Stop();
                var skippedResult = GraderResult.Skipped(GRADER_ID_EXPECTATIONS, EvaluationTier.State,
                    "No expectations provided");
                skippedResult.DurationMs = sw.ElapsedMilliseconds;
                return skippedResult;
            }

            try
            {
                // Note: scenePath parameter is unused - CaptureScene() captures the active scene
                var snapshot = _sceneCapture.CaptureScene();
                var flattenedObjects = FlattenHierarchy(snapshot);
                var passedCount = 0;

                foreach (var expectation in expectations)
                {
                    var result = ValidateSingleExpectation(flattenedObjects, expectation);
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
                var errorResult = GraderResult.Error(GRADER_ID_EXPECTATIONS, EvaluationTier.State,
                    $"Failed to validate expectations: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        public GraderResult CompareToBaseline(SceneSnapshot baseline, string scenePath = null)
        {
            var sw = Stopwatch.StartNew();

            if (baseline == null)
            {
                sw.Stop();
                var skippedResult = GraderResult.Skipped(GRADER_ID_BASELINE, EvaluationTier.State,
                    "No baseline snapshot provided");
                skippedResult.DurationMs = sw.ElapsedMilliseconds;
                return skippedResult;
            }

            try
            {
                // Note: scenePath parameter is unused - CaptureScene() captures the active scene
                var current = _sceneCapture.CaptureScene();
                var issues = new List<GradingIssue>();
                var differences = 0;
                var criticalDifferences = 0;

                // Flatten both hierarchies for comparison
                var baselineFlattened = FlattenHierarchy(baseline);
                var currentFlattened = FlattenHierarchy(current);

                var baselineObjects = baselineFlattened.ToDictionary(g => g.Path, g => g.Snapshot);
                var currentObjects = currentFlattened.ToDictionary(g => g.Path, g => g.Snapshot);

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

                    // Check active state (GameObjectSnapshot uses 'Active' not 'IsActive')
                    if (baselineObj.Active != currentObj.Active)
                    {
                        issues.Add(new GradingIssue
                        {
                            Severity = IssueSeverity.Warning,
                            Code = "STATE_ACTIVE_CHANGED",
                            Message = $"Active state changed: {baselineObj.Active} -> {currentObj.Active}",
                            ObjectPath = path
                        });
                        differences++;
                    }

                    // Check component count (Components is string[], use Length)
                    var baselineComponentCount = baselineObj.Components?.Length ?? 0;
                    var currentComponentCount = currentObj.Components?.Length ?? 0;
                    if (baselineComponentCount != currentComponentCount)
                    {
                        issues.Add(new GradingIssue
                        {
                            Severity = IssueSeverity.Warning,
                            Code = "STATE_COMPONENTS_CHANGED",
                            Message = $"Component count changed: {baselineComponentCount} -> {currentComponentCount}",
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
                var errorResult = GraderResult.Error(GRADER_ID_BASELINE, EvaluationTier.State,
                    $"Failed to compare to baseline: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        public GraderResult RunValidationRules(string scenePath = null)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                // Note: scenePath parameter is unused - ValidateScene() validates the active scene
                var validationReport = _validationRules.ValidateScene();
                var issues = new List<GradingIssue>();

                // ValidationReport has Results, not Violations
                foreach (var result in validationReport.Results)
                {
                    // Only add failed results as issues
                    if (!result.Passed)
                    {
                        issues.Add(new GradingIssue
                        {
                            Severity = MapSeverity(result.Severity),
                            Code = result.RuleId,
                            Message = result.Message,
                            ObjectPath = result.ObjectPath,
                            SuggestedFix = result.AutoFixSuggestion
                        });
                    }
                }

                sw.Stop();

                // Calculate rules checked from total results
                var rulesChecked = validationReport.Results.Count;

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
                        Summary = $"All {rulesChecked} validation rules passed",
                        Metadata = new Dictionary<string, object>
                        {
                            { "rules_checked", rulesChecked },
                            { "error_count", validationReport.ErrorCount },
                            { "warning_count", validationReport.WarningCount }
                        }
                    };
                }

                var errorCount = issues.Count(i => i.Severity == IssueSeverity.Error || i.Severity == IssueSeverity.Critical);
                var status = errorCount > 0 ? GradeStatus.Fail : GradeStatus.Warning;
                var score = 1f - ((float)errorCount / Math.Max(rulesChecked, 1));

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
                        { "rules_checked", rulesChecked },
                        { "error_count", validationReport.ErrorCount },
                        { "warning_count", validationReport.WarningCount }
                    }
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_VALIDATION, EvaluationTier.State,
                    $"Failed to run validation rules: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        private (bool passed, object actualValue) ValidateSingleExpectation(List<FlattenedGameObject> flattenedObjects, StateExpectation expectation)
        {
            // Find the GameObject by path or name
            var flattened = flattenedObjects.FirstOrDefault(g =>
                g.Path == expectation.GameObjectPath ||
                g.Snapshot.Name == expectation.GameObjectPath);

            if (flattened == null)
            {
                return (expectation.Mode == StateComparisonMode.IsNull, null);
            }

            var gameObject = flattened.Snapshot;

            // Find the component (ComponentData contains ComponentSnapshot[], not Components)
            ComponentSnapshot component = null;
            if (!string.IsNullOrEmpty(expectation.ComponentType) && gameObject.ComponentData != null)
            {
                component = gameObject.ComponentData.FirstOrDefault(c =>
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
            // ComponentSnapshot has 'Fields' not 'SerializedFields'
            if (component.Fields == null)
                return null;

            // Handle nested paths like "settings.speed"
            var parts = propertyPath.Split('.');
            object current = component.Fields;

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
            // GameObjectSnapshot uses 'Active' not 'IsActive'
            // Position, Rotation, Scale are float[] arrays, not Vector3
            var lowerPath = propertyPath.ToLowerInvariant();
            return lowerPath switch
            {
                "isactive" or "active" => gameObject.Active,
                "layer" => gameObject.Layer,
                "tag" => gameObject.Tag,
                "name" => gameObject.Name,
                "position" => gameObject.Position,
                "position.x" => gameObject.Position != null && gameObject.Position.Length > 0 ? gameObject.Position[0] : (object)null,
                "position.y" => gameObject.Position != null && gameObject.Position.Length > 1 ? gameObject.Position[1] : (object)null,
                "position.z" => gameObject.Position != null && gameObject.Position.Length > 2 ? gameObject.Position[2] : (object)null,
                "rotation" => gameObject.Rotation,
                "rotation.x" => gameObject.Rotation != null && gameObject.Rotation.Length > 0 ? gameObject.Rotation[0] : (object)null,
                "rotation.y" => gameObject.Rotation != null && gameObject.Rotation.Length > 1 ? gameObject.Rotation[1] : (object)null,
                "rotation.z" => gameObject.Rotation != null && gameObject.Rotation.Length > 2 ? gameObject.Rotation[2] : (object)null,
                "scale" or "localscale" => gameObject.Scale,
                "scale.x" or "localscale.x" => gameObject.Scale != null && gameObject.Scale.Length > 0 ? gameObject.Scale[0] : (object)null,
                "scale.y" or "localscale.y" => gameObject.Scale != null && gameObject.Scale.Length > 1 ? gameObject.Scale[1] : (object)null,
                "scale.z" or "localscale.z" => gameObject.Scale != null && gameObject.Scale.Length > 2 ? gameObject.Scale[2] : (object)null,
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

        private IssueSeverity MapSeverity(ValidationSeverity severity)
        {
            return severity switch
            {
                ValidationSeverity.Error => IssueSeverity.Error,
                ValidationSeverity.Warning => IssueSeverity.Warning,
                ValidationSeverity.Info => IssueSeverity.Info,
                _ => IssueSeverity.Warning
            };
        }
    }
}
