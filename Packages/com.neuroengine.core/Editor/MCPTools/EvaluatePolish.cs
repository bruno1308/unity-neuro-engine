#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using Newtonsoft.Json.Linq;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool for Tier 5.5 (Polish) evaluation.
    /// Checks game feel elements: audio, visual feedback, environment, code cleanliness.
    /// Addresses Problem #9: agents missing sound, environment, and visual feedback.
    /// </summary>
    [McpForUnityTool("evaluate_polish", Description = "Runs Tier 5.5 (Polish) evaluation. Actions: 'all' (default), 'audio', 'visual', 'environment', 'code'. Checks game feel elements that technical evaluations miss: audio feedback, visual effects, environment setup, and debug code removal.")]
    public static class EvaluatePolish
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant() ?? "all";
            string scenePath = @params["scene_path"]?.ToString();
            string scriptsPath = @params["scripts_path"]?.ToString() ?? "Assets/Scripts";

            var grader = EditorServiceLocator.Get<IPolishGrader>();
            if (grader == null)
            {
                return new ErrorResponse("Failed to get IPolishGrader service");
            }

            try
            {
                switch (action)
                {
                    case "audio":
                        return HandleSingleCheck(grader.CheckAudioFeedback(scenePath));

                    case "visual":
                    case "visual_feedback":
                        return HandleSingleCheck(grader.CheckVisualFeedback(scenePath));

                    case "environment":
                    case "env":
                        return HandleSingleCheck(grader.CheckEnvironment(scenePath));

                    case "code":
                    case "code_cleanliness":
                        return HandleSingleCheck(grader.CheckCodeCleanliness(scriptsPath));

                    case "all":
                        return HandleFullReport(grader, scenePath, scriptsPath);

                    default:
                        throw new ArgumentException($"Unknown action '{action}'. Use: all, audio, visual, environment, code");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Polish evaluation failed: {e.Message}");
            }
        }

        private static object HandleSingleCheck(PolishCheckResult result)
        {
            return new SuccessResponse($"Polish check: {result.CheckName} - {(result.Passed ? "PASS" : "FAIL")}", new
            {
                check_name = result.CheckName,
                category = result.Category.ToString().ToLowerInvariant(),
                passed = result.Passed,
                message = result.Message,
                issue_count = result.Issues?.Count ?? 0,
                issues = result.Issues,
                details = result.Details
            });
        }

        private static object HandleFullReport(IPolishGrader grader, string scenePath, string scriptsPath)
        {
            var config = new PolishConfig
            {
                ScenePath = scenePath,
                GameScriptsPath = scriptsPath
            };

            var report = grader.GradePolish(config);
            var graderResult = grader.ToGraderResult(report);

            return new SuccessResponse($"Polish evaluation: {graderResult.Status}", new
            {
                grader_id = graderResult.GraderId,
                tier = "polish",
                status = graderResult.Status.ToString().ToLowerInvariant(),
                score = graderResult.Score,
                is_blocking = graderResult.IsBlocking,
                duration_ms = graderResult.DurationMs,
                summary = graderResult.Summary,
                has_critical_issues = report.HasCriticalIssues,

                // Category summaries
                categories = new
                {
                    audio = new
                    {
                        passed = report.AudioSummary.Passed,
                        score = report.AudioSummary.Score,
                        critical_issues = report.AudioSummary.CriticalIssues
                    },
                    visual_feedback = new
                    {
                        passed = report.VisualFeedbackSummary.Passed,
                        score = report.VisualFeedbackSummary.Score,
                        critical_issues = report.VisualFeedbackSummary.CriticalIssues
                    },
                    environment = new
                    {
                        passed = report.EnvironmentSummary.Passed,
                        score = report.EnvironmentSummary.Score,
                        critical_issues = report.EnvironmentSummary.CriticalIssues
                    },
                    code_cleanliness = new
                    {
                        passed = report.CodeCleanlinessSummary.Passed,
                        score = report.CodeCleanlinessSummary.Score,
                        critical_issues = report.CodeCleanlinessSummary.CriticalIssues
                    }
                },

                // All checks for detailed view
                checks = report.AllChecks?.Select(c => new
                {
                    category = c.Category.ToString().ToLowerInvariant(),
                    check_name = c.CheckName,
                    passed = c.Passed,
                    message = c.Message,
                    issues = c.Issues,
                    details = c.Details
                }),

                // Standard GraderResult issues format
                issue_count = graderResult.Issues?.Count ?? 0,
                issues = graderResult.Issues?.Select(i => new
                {
                    severity = i.Severity.ToString().ToLowerInvariant(),
                    code = i.Code,
                    message = i.Message
                }),

                metadata = graderResult.Metadata
            });
        }
    }
}
#endif
