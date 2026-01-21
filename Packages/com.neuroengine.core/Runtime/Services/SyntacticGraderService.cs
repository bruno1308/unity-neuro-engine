using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NeuroEngine.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Tier 1: Syntactic grading - compilation, null refs, missing refs.
    /// Wraps existing Layer 2 services (IMissingReferenceDetector, IValidationRules).
    /// </summary>
    public class SyntacticGraderService : ISyntacticGrader
    {
        private readonly IMissingReferenceDetector _missingRefDetector;
        private readonly IValidationRules _validationRules;
        private readonly ISceneStateCapture _sceneCapture;

        private const string GRADER_ID_COMPILATION = "syntactic.compilation";
        private const string GRADER_ID_NULL_REFS = "syntactic.null-refs";
        private const string GRADER_ID_MISSING_REFS = "syntactic.missing-refs";
        private const string GRADER_ID_ALL = "syntactic.all";

        public SyntacticGraderService(
            IMissingReferenceDetector missingRefDetector,
            IValidationRules validationRules,
            ISceneStateCapture sceneCapture)
        {
            _missingRefDetector = missingRefDetector;
            _validationRules = validationRules;
            _sceneCapture = sceneCapture;
        }

        /// <summary>
        /// Parameterless constructor for editor/standalone use.
        /// </summary>
        public SyntacticGraderService()
        {
            _missingRefDetector = new MissingReferenceDetector();
            _validationRules = new ValidationRulesEngine();
            _sceneCapture = new SceneStateCaptureService();
        }

        public GraderResult CheckCompilation()
        {
            var sw = Stopwatch.StartNew();
            var issues = new List<GradingIssue>();

#if UNITY_EDITOR
            try
            {
                // Check for compilation errors using EditorUtility flag
                var hasErrors = UnityEditor.EditorUtility.scriptCompilationFailed;

                if (hasErrors)
                {
                    // EditorUtility.scriptCompilationFailed only tells us there ARE errors
                    // To get details, we'd need to parse the console or use CompilationPipeline events
                    // For now, we report the failure without detailed messages
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Error,
                        Code = "COMPILE_ERROR",
                        Message = "Script compilation failed. Check Unity console for details.",
                        FilePath = null,
                        Line = 0
                    });

                    sw.Stop();
                    return new GraderResult
                    {
                        GraderId = GRADER_ID_COMPILATION,
                        Tier = EvaluationTier.Syntactic,
                        Status = GradeStatus.Fail,
                        Score = 0f,
                        Weight = 1.0f,
                        DurationMs = sw.ElapsedMilliseconds,
                        Summary = "Compilation failed - check Unity console for details",
                        Issues = issues
                    };
                }

                sw.Stop();
                var passResult = GraderResult.Pass(GRADER_ID_COMPILATION, EvaluationTier.Syntactic,
                    "Compilation successful");
                passResult.DurationMs = sw.ElapsedMilliseconds;
                return passResult;
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_COMPILATION, EvaluationTier.Syntactic,
                    $"Failed to check compilation: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
#else
            sw.Stop();
            var skippedResult = GraderResult.Skipped(GRADER_ID_COMPILATION, EvaluationTier.Syntactic,
                "Compilation check only available in editor");
            skippedResult.DurationMs = sw.ElapsedMilliseconds;
            return skippedResult;
#endif
        }

        public GraderResult DetectNullReferences(string scenePath = null)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                // CaptureScene() takes no params or SceneCaptureOptions, not a string path
                var snapshot = _sceneCapture.CaptureScene();
                var issues = new List<GradingIssue>();
                int totalObjectCount = 0;

                // Recursively check all GameObjects for null serialized fields
                // RootObjects is hierarchical, need to traverse
                if (snapshot.RootObjects != null)
                {
                    foreach (var rootGo in snapshot.RootObjects)
                    {
                        CheckGameObjectForNullRefs(rootGo, "", issues, ref totalObjectCount);
                    }
                }

                sw.Stop();

                if (issues.Count == 0)
                {
                    var passResult = GraderResult.Pass(GRADER_ID_NULL_REFS, EvaluationTier.Syntactic,
                        "No null references found");
                    passResult.DurationMs = sw.ElapsedMilliseconds;
                    return passResult;
                }

                var errorCount = issues.Count(i => i.Severity == IssueSeverity.Error);
                var status = errorCount > 0 ? GradeStatus.Fail : GradeStatus.Warning;
                var score = 1f - (issues.Count / (float)Math.Max(totalObjectCount * 5, 1));

                return new GraderResult
                {
                    GraderId = GRADER_ID_NULL_REFS,
                    Tier = EvaluationTier.Syntactic,
                    Status = status,
                    Score = Math.Max(0, score),
                    Weight = 0.8f,
                    DurationMs = sw.ElapsedMilliseconds,
                    Summary = $"Found {issues.Count} null references",
                    Issues = issues
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_NULL_REFS, EvaluationTier.Syntactic,
                    $"Failed to detect null references: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        /// <summary>
        /// Recursively checks a GameObjectSnapshot and its children for null references.
        /// Builds the path during traversal since GameObjectSnapshot doesn't have a Path property.
        /// </summary>
        private void CheckGameObjectForNullRefs(GameObjectSnapshot go, string parentPath, List<GradingIssue> issues, ref int objectCount)
        {
            objectCount++;
            var currentPath = string.IsNullOrEmpty(parentPath) ? go.Name : $"{parentPath}/{go.Name}";

            // ComponentData contains the full component snapshots with Fields
            // Components is just string[] of component names
            if (go.ComponentData != null)
            {
                foreach (var component in go.ComponentData)
                {
                    // Fields is Dictionary<string, object>, not SerializedFields
                    if (component.Fields == null) continue;

                    foreach (var field in component.Fields)
                    {
                        // Check if field is a reference type that's null
                        if (field.Value != null && field.Value is string strValue)
                        {
                            if (strValue == "null" || strValue == "None" || strValue == "Missing")
                            {
                                issues.Add(new GradingIssue
                                {
                                    Severity = IssueSeverity.Warning,
                                    Code = "NULL_REF",
                                    Message = $"Null reference in {component.Type}.{field.Key}",
                                    ObjectPath = currentPath,
                                    SuggestedFix = $"Assign a value to {field.Key}"
                                });
                            }
                        }
                    }
                }
            }

            // Recursively check children
            if (go.Children != null)
            {
                foreach (var child in go.Children)
                {
                    CheckGameObjectForNullRefs(child, currentPath, issues, ref objectCount);
                }
            }
        }

        public GraderResult DetectMissingReferences(string scenePath = null)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                // ScanScene() takes no params, not DetectInScene(scenePath)
                var report = _missingRefDetector.ScanScene();
                var issues = new List<GradingIssue>();

                // References is List<MissingReference>, not MissingReferences
                foreach (var missing in report.References)
                {
                    // MissingReference has ObjectPath, ComponentType, FieldName, ExpectedType, Severity
                    // Not HierarchyPath or LastKnownGUID
                    issues.Add(new GradingIssue
                    {
                        Severity = missing.Severity == "error" ? IssueSeverity.Error : IssueSeverity.Warning,
                        Code = "MISSING_REF",
                        Message = $"Missing reference: {missing.FieldName} on {missing.ComponentType}",
                        ObjectPath = missing.ObjectPath,
                        SuggestedFix = $"Assign a {missing.ExpectedType} to {missing.FieldName}",
                        Metadata = new Dictionary<string, object>
                        {
                            { "field_name", missing.FieldName },
                            { "component_type", missing.ComponentType },
                            { "expected_type", missing.ExpectedType }
                        }
                    });
                }

                sw.Stop();

                if (issues.Count == 0)
                {
                    var passResult = GraderResult.Pass(GRADER_ID_MISSING_REFS, EvaluationTier.Syntactic,
                        "No missing references found");
                    passResult.DurationMs = sw.ElapsedMilliseconds;
                    passResult.Metadata = new Dictionary<string, object>
                    {
                        { "fields_scanned", report.TotalFieldsScanned },
                        { "null_count", report.NullCount }
                    };
                    return passResult;
                }

                return new GraderResult
                {
                    GraderId = GRADER_ID_MISSING_REFS,
                    Tier = EvaluationTier.Syntactic,
                    Status = GradeStatus.Fail,
                    Score = 0f,
                    Weight = 1.0f,  // Missing refs are always blocking
                    DurationMs = sw.ElapsedMilliseconds,
                    Summary = $"Found {issues.Count} missing references",
                    Issues = issues,
                    Metadata = new Dictionary<string, object>
                    {
                        { "fields_scanned", report.TotalFieldsScanned },
                        { "null_count", report.NullCount }
                    }
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_MISSING_REFS, EvaluationTier.Syntactic,
                    $"Failed to detect missing references: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        public GraderResult GradeAll(string scenePath = null)
        {
            var sw = Stopwatch.StartNew();
            var allIssues = new List<GradingIssue>();
            var results = new List<GraderResult>();

            // Run all syntactic checks
            results.Add(CheckCompilation());
            results.Add(DetectNullReferences(scenePath));
            results.Add(DetectMissingReferences(scenePath));

            // Also run validation rules
            try
            {
                // ValidateScene() takes no params, not ValidateScene(scenePath)
                var validationResult = _validationRules.ValidateScene();
                // ValidationReport has Results (List<ValidationResult>), not Violations
                foreach (var result in validationResult.Results)
                {
                    if (!result.Passed)
                    {
                        allIssues.Add(new GradingIssue
                        {
                            Severity = result.Severity == ValidationSeverity.Error ? IssueSeverity.Error : IssueSeverity.Warning,
                            Code = result.RuleId,
                            Message = result.Message,
                            ObjectPath = result.ObjectPath,
                            SuggestedFix = result.AutoFixSuggestion
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SyntacticGrader] Validation rules check failed: {ex.Message}");
            }

            // Aggregate all issues from sub-results
            foreach (var result in results)
            {
                allIssues.AddRange(result.Issues);
            }

            sw.Stop();

            // Calculate overall status
            var hasBlocker = results.Any(r => r.IsBlocking);
            var hasFail = results.Any(r => r.Status == GradeStatus.Fail);
            var hasWarning = allIssues.Any(i => i.Severity == IssueSeverity.Warning);

            var overallStatus = hasBlocker || hasFail
                ? GradeStatus.Fail
                : (hasWarning ? GradeStatus.Warning : GradeStatus.Pass);

            var weightedScore = results.Sum(r => r.Score * r.Weight) / Math.Max(results.Sum(r => r.Weight), 1);

            return new GraderResult
            {
                GraderId = GRADER_ID_ALL,
                Tier = EvaluationTier.Syntactic,
                Status = overallStatus,
                Score = weightedScore,
                Weight = 1.0f,
                DurationMs = sw.ElapsedMilliseconds,
                Summary = overallStatus == GradeStatus.Pass
                    ? "All syntactic checks passed"
                    : $"Syntactic issues found: {allIssues.Count} total ({allIssues.Count(i => i.Severity == IssueSeverity.Error)} errors)",
                Issues = allIssues,
                Metadata = new Dictionary<string, object>
                {
                    { "sub_results", results.Select(r => new { r.GraderId, r.Status, r.Score }).ToList() }
                }
            };
        }
    }
}
