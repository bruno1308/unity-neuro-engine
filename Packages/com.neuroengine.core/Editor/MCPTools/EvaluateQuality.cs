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
    /// MCP tool for Tier 5 (Quality) evaluation.
    /// Measures juice metrics, polish, accessibility, and performance.
    /// Complements the Polish grader with additional quality metrics.
    /// </summary>
    [McpForUnityTool("evaluate_quality", Description = "Runs Tier 5 (Quality) evaluation. Actions: 'all' (default), 'juice' (particles, screenshake, audio), 'polish' (game feel elements), 'accessibility' (text size, navigation), 'performance' (FPS, memory). Measures subjective quality through objective proxies.")]
    public static class EvaluateQuality
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant() ?? "all";
            string scenePath = @params["scene_path"]?.ToString();

            var grader = EditorServiceLocator.Get<IQualityGrader>();
            if (grader == null)
            {
                return new ErrorResponse("Failed to get IQualityGrader service");
            }

            try
            {
                switch (action)
                {
                    case "juice":
                        return HandleJuice(@params, grader);

                    case "polish":
                        return HandlePolish(scenePath, grader);

                    case "accessibility":
                    case "a11y":
                        return HandleAccessibility(scenePath, grader);

                    case "performance":
                    case "perf":
                        return HandlePerformance(@params, grader);

                    case "all":
                        return HandleAll(@params, grader, scenePath);

                    default:
                        throw new ArgumentException($"Unknown action '{action}'. Use: all, juice, polish, accessibility, performance");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Quality evaluation failed: {e.Message}");
            }
        }

        private static object HandleJuice(JObject @params, IQualityGrader grader)
        {
            var config = new JuiceConfig
            {
                ScenePath = @params["scene_path"]?.ToString(),
                MinScreenShakeIntensity = @params["min_screenshake"]?.Value<float>() ?? 0f,
                MinParticlesPerAction = @params["min_particles"]?.Value<int>() ?? 10,
                MaxResponseTimeMs = @params["max_response_time_ms"]?.Value<float>() ?? 100f,
                MinAudioReactivity = @params["min_audio_reactivity"]?.Value<float>() ?? 0f
            };

            var result = grader.MeasureJuice(config);
            return FormatResult(result, "juice");
        }

        private static object HandlePolish(string scenePath, IQualityGrader grader)
        {
            var result = grader.AssessPolish(scenePath);
            return FormatResult(result, "polish");
        }

        private static object HandleAccessibility(string scenePath, IQualityGrader grader)
        {
            var result = grader.CheckAccessibility(scenePath);
            return FormatResult(result, "accessibility");
        }

        private static object HandlePerformance(JObject @params, IQualityGrader grader)
        {
            var config = new PerformanceConfig
            {
                ScenePath = @params["scene_path"]?.ToString(),
                TargetFPS = @params["target_fps"]?.Value<float>() ?? 60f,
                MaxFrameTimeMs = @params["max_frame_time_ms"]?.Value<float>() ?? 33.33f,
                MaxMemoryMB = @params["max_memory_mb"]?.Value<float>() ?? 2048f,
                DurationSeconds = @params["duration_seconds"]?.Value<int>() ?? 5
            };

            var task = grader.ProfilePerformanceAsync(config);
            task.Wait();
            var result = task.Result;

            return FormatResult(result, "performance");
        }

        private static object HandleAll(JObject @params, IQualityGrader grader, string scenePath)
        {
            var results = new List<GraderResult>();
            var issues = new List<object>();

            // Run all quality checks
            var juiceResult = grader.MeasureJuice(new JuiceConfig { ScenePath = scenePath });
            results.Add(juiceResult);

            var polishResult = grader.AssessPolish(scenePath);
            results.Add(polishResult);

            var accessibilityResult = grader.CheckAccessibility(scenePath);
            results.Add(accessibilityResult);

            var performanceTask = grader.ProfilePerformanceAsync(new PerformanceConfig
            {
                ScenePath = scenePath,
                TargetFPS = @params["target_fps"]?.Value<float>() ?? 60f,
                MaxFrameTimeMs = @params["max_frame_time_ms"]?.Value<float>() ?? 33.33f,
                MaxMemoryMB = @params["max_memory_mb"]?.Value<float>() ?? 2048f
            });
            performanceTask.Wait();
            var performanceResult = performanceTask.Result;
            results.Add(performanceResult);

            // Aggregate results
            float totalScore = 0f;
            float totalWeight = 0f;
            long totalDuration = 0;
            bool hasBlocker = false;

            foreach (var result in results)
            {
                totalScore += result.Score * result.Weight;
                totalWeight += result.Weight;
                totalDuration += result.DurationMs;
                if (result.IsBlocking) hasBlocker = true;

                if (result.Issues != null)
                {
                    foreach (var issue in result.Issues)
                    {
                        issues.Add(new
                        {
                            severity = issue.Severity.ToString().ToLowerInvariant(),
                            code = issue.Code,
                            message = issue.Message,
                            object_path = issue.ObjectPath,
                            suggested_fix = issue.SuggestedFix
                        });
                    }
                }
            }

            float overallScore = totalWeight > 0 ? totalScore / totalWeight : 0f;
            var overallStatus = hasBlocker ? GradeStatus.Fail :
                               overallScore >= 0.7f ? GradeStatus.Pass :
                               overallScore >= 0.4f ? GradeStatus.Warning :
                               GradeStatus.Fail;

            return new SuccessResponse($"Quality evaluation: {overallStatus}", new
            {
                tier = "quality",
                status = overallStatus.ToString().ToLowerInvariant(),
                score = overallScore,
                is_blocking = hasBlocker,
                duration_ms = totalDuration,
                summary = $"Quality score: {overallScore:P0}",

                // Individual check results
                checks = new
                {
                    juice = new
                    {
                        status = juiceResult.Status.ToString().ToLowerInvariant(),
                        score = juiceResult.Score,
                        summary = juiceResult.Summary,
                        metrics = juiceResult.Metadata
                    },
                    polish = new
                    {
                        status = polishResult.Status.ToString().ToLowerInvariant(),
                        score = polishResult.Score,
                        summary = polishResult.Summary,
                        metrics = polishResult.Metadata
                    },
                    accessibility = new
                    {
                        status = accessibilityResult.Status.ToString().ToLowerInvariant(),
                        score = accessibilityResult.Score,
                        summary = accessibilityResult.Summary,
                        metrics = accessibilityResult.Metadata
                    },
                    performance = new
                    {
                        status = performanceResult.Status.ToString().ToLowerInvariant(),
                        score = performanceResult.Score,
                        summary = performanceResult.Summary,
                        metrics = performanceResult.Metadata
                    }
                },

                // All issues
                issue_count = issues.Count,
                issues = issues
            });
        }

        private static object FormatResult(GraderResult result, string checkType)
        {
            return new SuccessResponse($"Quality check ({checkType}): {result.Status}", new
            {
                grader_id = result.GraderId,
                tier = "quality",
                check_type = checkType,
                status = result.Status.ToString().ToLowerInvariant(),
                score = result.Score,
                weight = result.Weight,
                is_blocking = result.IsBlocking,
                duration_ms = result.DurationMs,
                summary = result.Summary,

                // Metrics from metadata
                metrics = result.Metadata,

                // Issues
                issue_count = result.Issues?.Count ?? 0,
                issues = result.Issues?.Select(i => new
                {
                    severity = i.Severity.ToString().ToLowerInvariant(),
                    code = i.Code,
                    message = i.Message,
                    object_path = i.ObjectPath,
                    suggested_fix = i.SuggestedFix
                })
            });
        }
    }
}
#endif
