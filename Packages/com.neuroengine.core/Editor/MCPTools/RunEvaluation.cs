#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using Newtonsoft.Json.Linq;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool for running unified evaluations across all tiers.
    /// This is the main entry point for AI-driven quality assurance.
    ///
    /// Parameters:
    /// - target: Path to scene or prefab to evaluate
    /// - target_type: "scene" (default), "prefab", or "script"
    /// - tiers: Array of tier numbers to run (1-5), or "all"
    /// - fail_fast: Stop on first blocking failure (default: false)
    /// - timeout: Maximum seconds to run (default: 300)
    ///
    /// Returns full evaluation report as JSON.
    /// </summary>
    [McpForUnityTool("run_evaluation", Description = "Run unified evaluation across tiers. Params: target (path), target_type ('scene'|'prefab'), tiers (array of 1-5 or 'all'), fail_fast (bool), timeout (seconds). Returns full evaluation report.")]
    public static class RunEvaluation
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                // Parse parameters
                var target = ParseTarget(@params);
                var config = ParseConfig(@params);

                // Get the evaluation runner
                var runner = EditorServiceLocator.Get<IEvaluationRunner>();
                if (runner == null)
                {
                    return new ErrorResponse("Failed to get IEvaluationRunner service");
                }

                // Run evaluation (blocking in editor context)
                var task = runner.EvaluateAsync(target, config);
                task.Wait();
                var report = task.Result;

                // Format response
                return FormatResponse(report);
            }
            catch (AggregateException ae)
            {
                var innerMessage = ae.InnerExceptions.FirstOrDefault()?.Message ?? ae.Message;
                return new ErrorResponse($"Evaluation failed: {innerMessage}");
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Evaluation failed: {e.Message}");
            }
        }

        private static EvaluationTarget ParseTarget(JObject @params)
        {
            var targetPath = @params["target"]?.ToString() ?? @params["path"]?.ToString();
            var targetType = @params["target_type"]?.ToString()?.ToLowerInvariant() ?? "scene";

            var target = new EvaluationTarget
            {
                Id = targetPath ?? "active_scene",
                Type = targetType,
                Path = targetPath
            };

            // Add any context from params
            if (@params["context"] is JObject context)
            {
                foreach (var prop in context.Properties())
                {
                    target.Context[prop.Name] = prop.Value.ToObject<object>();
                }
            }

            return target;
        }

        private static EvaluationConfig ParseConfig(JObject @params)
        {
            var config = new EvaluationConfig();

            // Parse fail_fast
            if (@params["fail_fast"] != null)
            {
                config.FailFast = @params["fail_fast"].ToObject<bool>();
            }

            // Parse timeout
            if (@params["timeout"] != null)
            {
                config.TimeoutSeconds = @params["timeout"].ToObject<int>();
            }

            // Parse tiers
            var tiersParam = @params["tiers"];
            if (tiersParam != null)
            {
                // Disable all tiers first
                config.EnabledTiers = new Dictionary<EvaluationTier, bool>
                {
                    { EvaluationTier.Syntactic, false },
                    { EvaluationTier.State, false },
                    { EvaluationTier.Behavioral, false },
                    { EvaluationTier.Visual, false },
                    { EvaluationTier.Quality, false },
                    { EvaluationTier.Human, false }
                };

                if (tiersParam.Type == JTokenType.String)
                {
                    var tierStr = tiersParam.ToString().ToLowerInvariant();
                    if (tierStr == "all")
                    {
                        // Enable all automated tiers
                        config.EnabledTiers[EvaluationTier.Syntactic] = true;
                        config.EnabledTiers[EvaluationTier.State] = true;
                        config.EnabledTiers[EvaluationTier.Behavioral] = true;
                        config.EnabledTiers[EvaluationTier.Visual] = true;
                        config.EnabledTiers[EvaluationTier.Quality] = true;
                    }
                    else if (tierStr == "quick" || tierStr == "syntactic")
                    {
                        config.EnabledTiers[EvaluationTier.Syntactic] = true;
                    }
                    else if (tierStr == "full")
                    {
                        config = EvaluationConfig.FullEvaluation();
                    }
                }
                else if (tiersParam.Type == JTokenType.Array)
                {
                    foreach (var tierToken in tiersParam)
                    {
                        var tier = ParseTier(tierToken);
                        if (tier.HasValue)
                        {
                            config.EnabledTiers[tier.Value] = true;
                        }
                    }
                }
                else if (tiersParam.Type == JTokenType.Integer)
                {
                    var tier = ParseTier(tiersParam);
                    if (tier.HasValue)
                    {
                        config.EnabledTiers[tier.Value] = true;
                    }
                }
            }

            // Parse grader weights
            if (@params["weights"] is JObject weights)
            {
                foreach (var prop in weights.Properties())
                {
                    config.GraderWeights[prop.Name] = prop.Value.ToObject<float>();
                }
            }

            return config;
        }

        private static EvaluationTier? ParseTier(JToken token)
        {
            if (token.Type == JTokenType.Integer)
            {
                var tierNum = token.ToObject<int>();
                return tierNum switch
                {
                    1 => EvaluationTier.Syntactic,
                    2 => EvaluationTier.State,
                    3 => EvaluationTier.Behavioral,
                    4 => EvaluationTier.Visual,
                    5 => EvaluationTier.Quality,
                    6 => EvaluationTier.Human,
                    _ => null
                };
            }
            else if (token.Type == JTokenType.String)
            {
                var tierStr = token.ToString().ToLowerInvariant();
                return tierStr switch
                {
                    "syntactic" or "syntax" or "compile" => EvaluationTier.Syntactic,
                    "state" or "snapshot" => EvaluationTier.State,
                    "behavioral" or "playtest" => EvaluationTier.Behavioral,
                    "visual" or "screenshot" or "vlm" => EvaluationTier.Visual,
                    "quality" or "polish" => EvaluationTier.Quality,
                    "human" => EvaluationTier.Human,
                    _ => null
                };
            }

            return null;
        }

        private static object FormatResponse(EvaluationReport report)
        {
            var tierSummaries = report.TierSummaries.ToDictionary(
                kvp => kvp.Key.ToString().ToLowerInvariant(),
                kvp => new
                {
                    status = kvp.Value.OverallStatus.ToString().ToLowerInvariant(),
                    average_score = kvp.Value.AverageScore,
                    total_graders = kvp.Value.TotalGraders,
                    passed = kvp.Value.PassedGraders,
                    failed = kvp.Value.FailedGraders,
                    skipped = kvp.Value.SkippedGraders,
                    has_blocker = kvp.Value.HasBlocker,
                    duration_ms = kvp.Value.TotalDurationMs
                });

            var allResults = report.AllResults.Select(r => new
            {
                grader_id = r.GraderId,
                tier = r.Tier.ToString().ToLowerInvariant(),
                status = r.Status.ToString().ToLowerInvariant(),
                score = r.Score,
                weight = r.Weight,
                is_blocking = r.IsBlocking,
                duration_ms = r.DurationMs,
                summary = r.Summary,
                issue_count = r.Issues?.Count ?? 0,
                issues = r.Issues?.Take(20).Select(i => new
                {
                    severity = i.Severity.ToString().ToLowerInvariant(),
                    code = i.Code,
                    message = i.Message,
                    object_path = i.ObjectPath,
                    file_path = i.FilePath,
                    line = i.Line,
                    suggested_fix = i.SuggestedFix
                }).ToList()
            }).ToList();

            return new SuccessResponse($"Evaluation complete: {report.OverallStatus}", new
            {
                target_id = report.TargetId,
                target_type = report.TargetType,
                timestamp = report.Timestamp.ToString("O"),
                duration_ms = report.TotalDurationMs,
                overall_status = report.OverallStatus.ToString().ToLowerInvariant(),
                overall_score = report.OverallScore,
                has_blocking_failure = report.HasBlockingFailure,
                summary = report.Summary,
                tier_summaries = tierSummaries,
                grader_count = allResults.Count,
                total_issues = report.AllResults.Sum(r => r.Issues?.Count ?? 0),
                results = allResults
            });
        }
    }
}
#endif
