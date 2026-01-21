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
    /// Layer 5: Unified evaluation runner service.
    /// Orchestrates multiple graders across evaluation tiers and aggregates results.
    ///
    /// Supports:
    /// - Running all or specific tiers
    /// - Fail-fast mode (stop on first blocking failure)
    /// - Timeout handling
    /// - Result aggregation into EvaluationReport
    /// - Writing results to hooks/evaluations/ for persistence
    /// </summary>
    public class EvaluationRunnerService : IEvaluationRunner
    {
        private readonly ISyntacticGrader _syntacticGrader;
        private readonly IStateGrader _stateGrader;
        private readonly IPolishGrader _polishGrader;
        private readonly IHooksWriter _hooksWriter;

        // Optional graders that may not be available
        private readonly IBehavioralGrader _behavioralGrader;
        private readonly IVisualGrader _visualGrader;
        private readonly IQualityGrader _qualityGrader;

        private const string EVALUATIONS_CATEGORY = "evaluations";

        /// <summary>
        /// Full constructor with all dependencies.
        /// </summary>
        public EvaluationRunnerService(
            ISyntacticGrader syntacticGrader,
            IStateGrader stateGrader,
            IPolishGrader polishGrader,
            IHooksWriter hooksWriter,
            IBehavioralGrader behavioralGrader = null,
            IVisualGrader visualGrader = null,
            IQualityGrader qualityGrader = null)
        {
            _syntacticGrader = syntacticGrader ?? throw new ArgumentNullException(nameof(syntacticGrader));
            _stateGrader = stateGrader ?? throw new ArgumentNullException(nameof(stateGrader));
            _polishGrader = polishGrader ?? throw new ArgumentNullException(nameof(polishGrader));
            _hooksWriter = hooksWriter;

            // Optional graders
            _behavioralGrader = behavioralGrader;
            _visualGrader = visualGrader;
            _qualityGrader = qualityGrader;
        }

        /// <summary>
        /// Parameterless constructor for editor/standalone use.
        /// Creates default implementations of required services.
        /// </summary>
        public EvaluationRunnerService()
        {
            _syntacticGrader = new SyntacticGraderService();
            _stateGrader = new StateGraderService();
            _polishGrader = new PolishGraderService();

            // HooksWriter requires config - create with default config
            try
            {
                var config = new EnvConfigService();
                _hooksWriter = new HooksWriterService(config);
            }
            catch
            {
                // If config fails, we'll skip hooks writing
                _hooksWriter = null;
            }
        }

        public async Task<EvaluationReport> EvaluateAsync(EvaluationTarget target, EvaluationConfig config = null)
        {
            config ??= new EvaluationConfig();
            var sw = Stopwatch.StartNew();

            var report = new EvaluationReport
            {
                TargetId = target.Id,
                TargetType = target.Type,
                Timestamp = DateTime.UtcNow,
                AllResults = new List<GraderResult>(),
                TierSummaries = new Dictionary<EvaluationTier, TierSummary>()
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));

            try
            {
                // Run tiers in order (they're designed to be layered)
                var tiersToRun = GetEnabledTiers(config);

                foreach (var tier in tiersToRun)
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        report.AllResults.Add(GraderResult.Skipped(
                            $"tier.{tier}",
                            tier,
                            "Evaluation timed out"));
                        continue;
                    }

                    var tierResults = await RunTierAsync(tier, target, config, cts.Token);
                    report.AllResults.AddRange(tierResults);

                    // Build tier summary
                    var summary = BuildTierSummary(tier, tierResults);
                    report.TierSummaries[tier] = summary;

                    // Check for fail-fast
                    if (config.FailFast && summary.HasBlocker)
                    {
                        // Skip remaining tiers
                        Debug.Log($"[EvaluationRunner] Fail-fast triggered at tier {tier}");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[EvaluationRunner] Evaluation timed out");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EvaluationRunner] Evaluation failed: {ex.Message}");
                report.AllResults.Add(GraderResult.Error("evaluation.runner", EvaluationTier.Syntactic, ex.Message));
            }

            sw.Stop();

            // Calculate overall metrics
            FinalizeReport(report, sw.ElapsedMilliseconds);

            // Persist to hooks if available
            await WriteReportToHooksAsync(report);

            return report;
        }

        public async Task<EvaluationReport> EvaluateTiersAsync(EvaluationTarget target, params EvaluationTier[] tiers)
        {
            if (tiers == null || tiers.Length == 0)
            {
                tiers = new[] { EvaluationTier.Syntactic };
            }

            var config = new EvaluationConfig
            {
                EnabledTiers = new Dictionary<EvaluationTier, bool>()
            };

            // Disable all tiers first
            foreach (EvaluationTier tier in Enum.GetValues(typeof(EvaluationTier)))
            {
                config.EnabledTiers[tier] = false;
            }

            // Enable only requested tiers
            foreach (var tier in tiers)
            {
                config.EnabledTiers[tier] = true;
            }

            return await EvaluateAsync(target, config);
        }

        public GraderResult QuickCheck(string scenePath = null)
        {
            return _syntacticGrader.GradeAll(scenePath);
        }

        #region Private Methods

        private List<EvaluationTier> GetEnabledTiers(EvaluationConfig config)
        {
            var result = new List<EvaluationTier>();

            // Order matters - run in tier order
            foreach (EvaluationTier tier in Enum.GetValues(typeof(EvaluationTier)))
            {
                if (tier == EvaluationTier.Human)
                    continue; // Human tier is never auto-run

                if (config.IsTierEnabled(tier))
                {
                    result.Add(tier);
                }
            }

            return result;
        }

        private async Task<List<GraderResult>> RunTierAsync(
            EvaluationTier tier,
            EvaluationTarget target,
            EvaluationConfig config,
            CancellationToken ct)
        {
            var results = new List<GraderResult>();

            try
            {
                switch (tier)
                {
                    case EvaluationTier.Syntactic:
                        results.Add(await Task.Run(() => _syntacticGrader.GradeAll(target.Path), ct));
                        break;

                    case EvaluationTier.State:
                        results.AddRange(await RunStateGraderAsync(target, ct));
                        break;

                    case EvaluationTier.Behavioral:
                        if (_behavioralGrader != null)
                        {
                            results.AddRange(await RunBehavioralGraderAsync(target, ct));
                        }
                        else
                        {
                            results.Add(GraderResult.Skipped(
                                "behavioral.all",
                                tier,
                                "Behavioral grader not available"));
                        }
                        break;

                    case EvaluationTier.Visual:
                        if (_visualGrader != null)
                        {
                            // Visual grader not yet implemented - placeholder
                            results.Add(GraderResult.Skipped(
                                "visual.screenshot",
                                tier,
                                "Visual grader not yet implemented"));
                        }
                        else
                        {
                            results.Add(GraderResult.Skipped(
                                "visual.all",
                                tier,
                                "Visual grader not available"));
                        }
                        break;

                    case EvaluationTier.Quality:
                        results.AddRange(RunQualityGraders(target));
                        break;

                    default:
                        results.Add(GraderResult.Skipped(
                            $"tier.{tier}",
                            tier,
                            $"Tier {tier} not implemented"));
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                results.Add(GraderResult.Skipped($"tier.{tier}", tier, "Cancelled due to timeout"));
            }
            catch (Exception ex)
            {
                results.Add(GraderResult.Error($"tier.{tier}", tier, ex.Message));
            }

            // Apply weight overrides from config
            foreach (var result in results)
            {
                if (config.GraderWeights.TryGetValue(result.GraderId, out var weight))
                {
                    result.Weight = weight;
                }
            }

            return results;
        }

        private async Task<List<GraderResult>> RunStateGraderAsync(EvaluationTarget target, CancellationToken ct)
        {
            var results = new List<GraderResult>();

            // Run validation rules
            var validationResult = await Task.Run(() => _stateGrader.RunValidationRules(target.Path), ct);
            results.Add(validationResult);

            // Compare to baseline if provided
            if (target.BaselineSnapshot != null)
            {
                var baselineResult = await Task.Run(() =>
                    _stateGrader.CompareToBaseline(target.BaselineSnapshot, target.Path), ct);
                results.Add(baselineResult);
            }

            return results;
        }

        private async Task<List<GraderResult>> RunBehavioralGraderAsync(EvaluationTarget target, CancellationToken ct)
        {
            var results = new List<GraderResult>();

#if UNITY_EDITOR
            // Behavioral tests require Play Mode
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                results.Add(GraderResult.Skipped(
                    "behavioral.playtest",
                    EvaluationTier.Behavioral,
                    "Behavioral tests require Play Mode. Enter Play Mode to run behavioral evaluation."));
                return results;
            }
#endif

            try
            {
                // Run a basic playtest with default config
                // More sophisticated configs can be passed via target.Context
                PlaytestConfig playtestConfig = null;

                if (target.Context.TryGetValue("playtest_config", out var configObj) && configObj is PlaytestConfig pc)
                {
                    playtestConfig = pc;
                }
                else
                {
                    // Default: run basic verification that scene loads and is interactive
                    playtestConfig = new PlaytestConfig
                    {
                        ScenePath = target.Path,
                        TimeoutSeconds = 30,
                        SuccessConditions = new List<string>(),
                        FailureConditions = new List<string>(),
                        InputScripts = new List<InputSequence>()
                    };
                }

                var playtestResult = await _behavioralGrader.RunPlaytestAsync(playtestConfig);
                results.Add(playtestResult);

                // If there's an interaction test config, run it too
                if (target.Context.TryGetValue("interaction_config", out var interactionObj) && interactionObj is InteractionTestConfig itc)
                {
                    var interactionResult = await _behavioralGrader.TestInteractionAsync(itc);
                    results.Add(interactionResult);
                }

                // If there's a flow config, run flow verification
                if (target.Context.TryGetValue("flow_config", out var flowObj) && flowObj is GameFlowConfig gfc)
                {
                    var flowResult = await _behavioralGrader.VerifyFlowAsync(gfc);
                    results.Add(flowResult);
                }
            }
            catch (OperationCanceledException)
            {
                results.Add(GraderResult.Skipped("behavioral.playtest", EvaluationTier.Behavioral, "Cancelled due to timeout"));
            }
            catch (Exception ex)
            {
                results.Add(GraderResult.Error("behavioral.playtest", EvaluationTier.Behavioral, ex.Message));
            }

            return results;
        }

        private List<GraderResult> RunQualityGraders(EvaluationTarget target)
        {
            var results = new List<GraderResult>();

            // Run polish grader (part of Quality tier)
            var polishReport = _polishGrader.GradePolish(new PolishConfig
            {
                ScenePath = target.Path
            });
            results.Add(_polishGrader.ToGraderResult(polishReport));

            // Quality grader if available
            if (_qualityGrader != null)
            {
                try
                {
                    var polishResult = _qualityGrader.AssessPolish(target.Path);
                    results.Add(polishResult);

                    var accessibilityResult = _qualityGrader.CheckAccessibility(target.Path);
                    results.Add(accessibilityResult);
                }
                catch (Exception ex)
                {
                    results.Add(GraderResult.Error("quality.assess", EvaluationTier.Quality, ex.Message));
                }
            }

            return results;
        }

        private TierSummary BuildTierSummary(EvaluationTier tier, List<GraderResult> results)
        {
            var summary = new TierSummary
            {
                Tier = tier,
                TotalGraders = results.Count,
                PassedGraders = results.Count(r => r.Status == GradeStatus.Pass),
                FailedGraders = results.Count(r => r.Status == GradeStatus.Fail),
                SkippedGraders = results.Count(r => r.Status == GradeStatus.Skipped),
                HasBlocker = results.Any(r => r.IsBlocking),
                TotalDurationMs = results.Sum(r => r.DurationMs)
            };

            // Calculate average score (excluding skipped)
            var scoredResults = results.Where(r => r.Status != GradeStatus.Skipped).ToList();
            if (scoredResults.Any())
            {
                summary.AverageScore = scoredResults.Sum(r => r.Score * r.Weight) /
                                       scoredResults.Sum(r => r.Weight);
            }
            else
            {
                summary.AverageScore = 0f;
            }

            // Determine overall status
            if (summary.HasBlocker || summary.FailedGraders > 0)
            {
                summary.OverallStatus = GradeStatus.Fail;
            }
            else if (results.Any(r => r.Status == GradeStatus.Warning))
            {
                summary.OverallStatus = GradeStatus.Warning;
            }
            else if (summary.SkippedGraders == summary.TotalGraders)
            {
                summary.OverallStatus = GradeStatus.Skipped;
            }
            else
            {
                summary.OverallStatus = GradeStatus.Pass;
            }

            return summary;
        }

        private void FinalizeReport(EvaluationReport report, long totalDurationMs)
        {
            report.TotalDurationMs = totalDurationMs;

            // Calculate overall score (weighted average of tier scores)
            var tierWeights = new Dictionary<EvaluationTier, float>
            {
                { EvaluationTier.Syntactic, 1.5f },
                { EvaluationTier.State, 1.2f },
                { EvaluationTier.Behavioral, 1.0f },
                { EvaluationTier.Visual, 0.8f },
                { EvaluationTier.Quality, 0.6f }
            };

            float totalWeight = 0f;
            float weightedScore = 0f;

            foreach (var kvp in report.TierSummaries)
            {
                if (kvp.Value.OverallStatus != GradeStatus.Skipped)
                {
                    var weight = tierWeights.GetValueOrDefault(kvp.Key, 1.0f);
                    weightedScore += kvp.Value.AverageScore * weight;
                    totalWeight += weight;
                }
            }

            report.OverallScore = totalWeight > 0 ? weightedScore / totalWeight : 0f;

            // Determine overall status
            report.HasBlockingFailure = report.AllResults.Any(r => r.IsBlocking);

            if (report.HasBlockingFailure)
            {
                report.OverallStatus = GradeStatus.Fail;
            }
            else if (report.AllResults.Any(r => r.Status == GradeStatus.Fail))
            {
                report.OverallStatus = GradeStatus.Fail;
            }
            else if (report.AllResults.Any(r => r.Status == GradeStatus.Warning))
            {
                report.OverallStatus = GradeStatus.Warning;
            }
            else
            {
                report.OverallStatus = GradeStatus.Pass;
            }

            // Build summary
            var passCount = report.AllResults.Count(r => r.Status == GradeStatus.Pass);
            var failCount = report.AllResults.Count(r => r.Status == GradeStatus.Fail);
            var warnCount = report.AllResults.Count(r => r.Status == GradeStatus.Warning);
            var totalIssues = report.AllResults.Sum(r => r.Issues?.Count ?? 0);

            report.Summary = $"Evaluation: {passCount} passed, {failCount} failed, {warnCount} warnings. " +
                            $"Total issues: {totalIssues}. Score: {report.OverallScore:P0}";
        }

        private async Task WriteReportToHooksAsync(EvaluationReport report)
        {
            if (_hooksWriter == null)
                return;

            try
            {
                var filename = $"{SanitizeFilename(report.TargetId)}_{report.Timestamp:yyyyMMdd_HHmmss}.json";
                await _hooksWriter.WriteAsync(EVALUATIONS_CATEGORY, filename, report);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EvaluationRunner] Failed to write report to hooks: {ex.Message}");
            }
        }

        private string SanitizeFilename(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "unknown";

            // Remove path separators and invalid filename chars
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return string.Join("_", input.Split(invalid, StringSplitOptions.RemoveEmptyEntries))
                         .Replace("/", "_")
                         .Replace("\\", "_")
                         .Replace(".", "_");
        }

        #endregion
    }
}
