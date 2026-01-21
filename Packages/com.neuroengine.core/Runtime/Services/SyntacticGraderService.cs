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
                // Check for compilation errors
                var hasErrors = UnityEditor.EditorUtility.scriptCompilationFailed;

                if (hasErrors)
                {
                    // Get compiler messages
                    var compilerMessages = UnityEditor.Compilation.CompilationPipeline.GetCompilationMessages(
                        UnityEditor.Compilation.ReportType.Errors);

                    foreach (var msg in compilerMessages)
                    {
                        issues.Add(new GradingIssue
                        {
                            Severity = IssueSeverity.Error,
                            Code = "COMPILE_ERROR",
                            Message = msg.message,
                            FilePath = msg.file,
                            Line = msg.line
                        });
                    }

                    sw.Stop();
                    return new GraderResult
                    {
                        GraderId = GRADER_ID_COMPILATION,
                        Tier = EvaluationTier.Syntactic,
                        Status = GradeStatus.Fail,
                        Score = 0f,
                        Weight = 1.0f,
                        DurationMs = sw.ElapsedMilliseconds,
                        Summary = $"Compilation failed with {issues.Count} errors",
                        Issues = issues
                    };
                }

                sw.Stop();
                return GraderResult.Pass(GRADER_ID_COMPILATION, EvaluationTier.Syntactic,
                    "Compilation successful") with { DurationMs = sw.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return GraderResult.Error(GRADER_ID_COMPILATION, EvaluationTier.Syntactic,
                    $"Failed to check compilation: {ex.Message}") with { DurationMs = sw.ElapsedMilliseconds };
            }
#else
            sw.Stop();
            return GraderResult.Skipped(GRADER_ID_COMPILATION, EvaluationTier.Syntactic,
                "Compilation check only available in editor") with { DurationMs = sw.ElapsedMilliseconds };
#endif
        }

        public GraderResult DetectNullReferences(string scenePath = null)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var snapshot = _sceneCapture.CaptureScene(scenePath);
                var issues = new List<GradingIssue>();

                // Check all GameObjects for null serialized fields
                foreach (var go in snapshot.GameObjects)
                {
                    foreach (var component in go.Components)
                    {
                        if (component.SerializedFields == null) continue;

                        foreach (var field in component.SerializedFields)
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
                                        ObjectPath = go.Path,
                                        SuggestedFix = $"Assign a value to {field.Key}"
                                    });
                                }
                            }
                        }
                    }
                }

                sw.Stop();

                if (issues.Count == 0)
                {
                    return GraderResult.Pass(GRADER_ID_NULL_REFS, EvaluationTier.Syntactic,
                        "No null references found") with { DurationMs = sw.ElapsedMilliseconds };
                }

                var errorCount = issues.Count(i => i.Severity == IssueSeverity.Error);
                var status = errorCount > 0 ? GradeStatus.Fail : GradeStatus.Warning;
                var score = 1f - (issues.Count / (float)Math.Max(snapshot.GameObjects.Count * 5, 1));

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
                return GraderResult.Error(GRADER_ID_NULL_REFS, EvaluationTier.Syntactic,
                    $"Failed to detect null references: {ex.Message}") with { DurationMs = sw.ElapsedMilliseconds };
            }
        }

        public GraderResult DetectMissingReferences(string scenePath = null)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var report = _missingRefDetector.DetectInScene(scenePath);
                var issues = new List<GradingIssue>();

                foreach (var missing in report.MissingReferences)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Error,
                        Code = "MISSING_REF",
                        Message = $"Missing reference: {missing.FieldName} on {missing.ComponentType}",
                        ObjectPath = missing.HierarchyPath,
                        SuggestedFix = missing.LastKnownGUID != null
                            ? $"Reassign asset with GUID: {missing.LastKnownGUID}"
                            : "Locate and reassign the missing asset",
                        Metadata = new Dictionary<string, object>
                        {
                            { "field_name", missing.FieldName },
                            { "component_type", missing.ComponentType },
                            { "last_known_guid", missing.LastKnownGUID }
                        }
                    });
                }

                sw.Stop();

                if (issues.Count == 0)
                {
                    return GraderResult.Pass(GRADER_ID_MISSING_REFS, EvaluationTier.Syntactic,
                        "No missing references found") with
                    {
                        DurationMs = sw.ElapsedMilliseconds,
                        Metadata = new Dictionary<string, object>
                        {
                            { "objects_scanned", report.ObjectsScanned },
                            { "components_scanned", report.ComponentsScanned }
                        }
                    };
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
                        { "objects_scanned", report.ObjectsScanned },
                        { "components_scanned", report.ComponentsScanned }
                    }
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return GraderResult.Error(GRADER_ID_MISSING_REFS, EvaluationTier.Syntactic,
                    $"Failed to detect missing references: {ex.Message}") with { DurationMs = sw.ElapsedMilliseconds };
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
                var validationResult = _validationRules.ValidateScene(scenePath);
                foreach (var violation in validationResult.Violations)
                {
                    allIssues.Add(new GradingIssue
                    {
                        Severity = violation.Severity == "error" ? IssueSeverity.Error : IssueSeverity.Warning,
                        Code = violation.RuleId,
                        Message = violation.Message,
                        ObjectPath = violation.ObjectPath,
                        SuggestedFix = violation.SuggestedFix
                    });
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
