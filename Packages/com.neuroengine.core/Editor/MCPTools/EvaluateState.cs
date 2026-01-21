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
    /// MCP tool for Tier 2 (State) evaluation.
    /// JSON snapshot assertions, baseline comparison, validation rules.
    /// </summary>
    [McpForUnityTool("evaluate_state", Description = "Runs Tier 2 (State) evaluation. Actions: 'validation' (default), 'capture', 'expectations'. Validates scene state via JSON snapshots, expectations, and validation rules.")]
    public static class EvaluateState
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant() ?? "validation";
            string scenePath = @params["scene_path"]?.ToString();

            var grader = EditorServiceLocator.Get<IStateGrader>();
            if (grader == null)
            {
                return new ErrorResponse("Failed to get IStateGrader service");
            }

            try
            {
                GraderResult result;

                switch (action)
                {
                    case "capture":
                        return HandleCapture(grader, scenePath);

                    case "expectations" or "expect":
                        result = HandleExpectations(@params, grader, scenePath);
                        break;

                    case "baseline" or "compare":
                        return new ErrorResponse("Baseline comparison requires a baseline snapshot. Use 'capture' first, then pass the snapshot to 'baseline' action.");

                    case "validation" or "rules":
                        result = grader.RunValidationRules(scenePath);
                        break;

                    default:
                        throw new ArgumentException($"Unknown action '{action}'. Use: capture, expectations, validation");
                }

                return new SuccessResponse($"State evaluation: {result.Status}", new
                {
                    grader_id = result.GraderId,
                    tier = "state",
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
                        object_path = i.ObjectPath,
                        suggested_fix = i.SuggestedFix,
                        metadata = i.Metadata
                    }),
                    metadata = result.Metadata
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"State evaluation failed: {e.Message}");
            }
        }

        private static object HandleCapture(IStateGrader grader, string scenePath)
        {
            var snapshot = grader.CaptureSnapshotAsync(scenePath).GetAwaiter().GetResult();

            // SceneSnapshot has: SceneName, Timestamp, RootObjects (hierarchical), TotalObjectCount, TotalComponentsWithData
            // GameObjectSnapshot has: Name, Active (not IsActive), Tag, Layer, Position/Rotation/Scale (float[]), Children
            return new SuccessResponse("Scene snapshot captured", new
            {
                scene_name = snapshot.SceneName,
                timestamp = snapshot.Timestamp,
                total_object_count = snapshot.TotalObjectCount,
                total_components_with_data = snapshot.TotalComponentsWithData,
                root_objects = snapshot.RootObjects?
                    .Select(g => new { name = g.Name, active = g.Active, tag = g.Tag })
                    .ToList()
            });
        }

        private static GraderResult HandleExpectations(JObject @params, IStateGrader grader, string scenePath)
        {
            var expectationsJson = @params["expectations"];
            if (expectationsJson == null)
            {
                return GraderResult.Error("state.expectations", EvaluationTier.State,
                    "Missing 'expectations' parameter. Provide an array of expectations.");
            }

            var expectations = new List<StateExpectation>();
            foreach (var exp in expectationsJson)
            {
                expectations.Add(new StateExpectation
                {
                    GameObjectPath = exp["game_object_path"]?.ToString() ?? exp["path"]?.ToString(),
                    ComponentType = exp["component_type"]?.ToString() ?? exp["component"]?.ToString(),
                    PropertyPath = exp["property_path"]?.ToString() ?? exp["property"]?.ToString(),
                    ExpectedValue = exp["expected_value"]?.ToObject<object>() ?? exp["expected"]?.ToObject<object>(),
                    Mode = ParseComparisonMode(exp["mode"]?.ToString()),
                    Description = exp["description"]?.ToString()
                });
            }

            return grader.ValidateExpectations(expectations, scenePath);
        }

        private static StateComparisonMode ParseComparisonMode(string mode)
        {
            return mode?.ToLowerInvariant() switch
            {
                "exact" => StateComparisonMode.Exact,
                "contains" => StateComparisonMode.Contains,
                "range" => StateComparisonMode.Range,
                "regex" => StateComparisonMode.Regex,
                "notnull" or "not_null" => StateComparisonMode.NotNull,
                "isnull" or "is_null" => StateComparisonMode.IsNull,
                _ => StateComparisonMode.Exact
            };
        }
    }
}
#endif
