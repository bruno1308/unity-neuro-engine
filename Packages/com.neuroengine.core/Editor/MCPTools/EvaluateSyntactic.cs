#if UNITY_EDITOR
using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using Newtonsoft.Json.Linq;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool for Tier 1 (Syntactic) evaluation.
    /// Fast, deterministic checks: compilation, null refs, missing refs.
    /// </summary>
    [McpForUnityTool("evaluate_syntactic", Description = "Runs Tier 1 (Syntactic) evaluation. Actions: 'all' (default), 'compilation', 'null_refs', 'missing_refs'. Fast, deterministic checks for compilation errors, null references, and missing asset references.")]
    public static class EvaluateSyntactic
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant() ?? "all";
            string scenePath = @params["scene_path"]?.ToString();

            var grader = EditorServiceLocator.Get<ISyntacticGrader>();
            if (grader == null)
            {
                return new ErrorResponse("Failed to get ISyntacticGrader service");
            }

            try
            {
                GraderResult result = action switch
                {
                    "compilation" or "compile" => grader.CheckCompilation(),
                    "null_refs" or "null" => grader.DetectNullReferences(scenePath),
                    "missing_refs" or "missing" => grader.DetectMissingReferences(scenePath),
                    "all" => grader.GradeAll(scenePath),
                    _ => throw new ArgumentException($"Unknown action '{action}'. Use: compilation, null_refs, missing_refs, all")
                };

                return new SuccessResponse($"Syntactic evaluation: {result.Status}", new
                {
                    grader_id = result.GraderId,
                    tier = "syntactic",
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
                        file_path = i.FilePath,
                        line = i.Line,
                        suggested_fix = i.SuggestedFix
                    }),
                    metadata = result.Metadata
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Syntactic evaluation failed: {e.Message}");
            }
        }
    }
}
#endif
