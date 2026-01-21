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
    /// MCP tool for Tier 4 (Visual) evaluation.
    /// Captures screenshots and prepares them for VLM (Claude/Gemini) analysis.
    /// Returns base64 images and formatted prompts for visual verification.
    /// </summary>
    [McpForUnityTool("evaluate_visual", Description = "Runs Tier 4 (Visual) evaluation. Actions: 'screenshot' (capture and analyze), 'image' (analyze existing image), 'ui_layout' (verify UI layout), 'glitches' (detect visual glitches). Returns base64 images for VLM analysis.")]
    public static class EvaluateVisual
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant() ?? "screenshot";

            var grader = EditorServiceLocator.Get<IVisualGrader>();
            if (grader == null)
            {
                return new ErrorResponse("Failed to get IVisualGrader service");
            }

            try
            {
                switch (action)
                {
                    case "screenshot":
                    case "capture":
                        return HandleScreenshot(@params, grader);

                    case "image":
                    case "analyze":
                        return HandleImage(@params, grader);

                    case "ui_layout":
                    case "ui":
                        return HandleUILayout(@params, grader);

                    case "glitches":
                    case "detect_glitches":
                        return HandleGlitches(@params, grader);

                    default:
                        throw new ArgumentException($"Unknown action '{action}'. Use: screenshot, image, ui_layout, glitches");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Visual evaluation failed: {e.Message}");
            }
        }

        private static object HandleScreenshot(JObject @params, IVisualGrader grader)
        {
            var config = new VisualAnalysisConfig
            {
                CameraPath = @params["camera_path"]?.ToString(),
                Width = @params["width"]?.Value<int>() ?? 0,
                Height = @params["height"]?.Value<int>() ?? 0,
                VLMProvider = @params["vlm_provider"]?.ToString() ?? "claude",
                Queries = ParseQueries(@params["queries"])
            };

            var task = grader.AnalyzeScreenshotAsync(config);
            task.Wait();
            var result = task.Result;

            return FormatResult(result);
        }

        private static object HandleImage(JObject @params, IVisualGrader grader)
        {
            string imagePath = @params["image_path"]?.ToString();
            if (string.IsNullOrEmpty(imagePath))
            {
                return new ErrorResponse("image_path is required for 'image' action");
            }

            var queries = ParseQueries(@params["queries"]);
            if (queries.Count == 0)
            {
                queries.Add("Describe what you see in this image.");
            }

            var task = grader.AnalyzeImageAsync(imagePath, queries);
            task.Wait();
            var result = task.Result;

            return FormatResult(result);
        }

        private static object HandleUILayout(JObject @params, IVisualGrader grader)
        {
            var expectation = new UILayoutExpectation
            {
                Description = @params["description"]?.ToString() ?? "UI Layout Verification",
                Elements = ParseUIElements(@params["elements"])
            };

            if (expectation.Elements.Count == 0)
            {
                return new ErrorResponse("At least one UI element expectation is required. Provide 'elements' array with {path, visible, text, position}.");
            }

            var task = grader.VerifyUILayoutAsync(expectation);
            task.Wait();
            var result = task.Result;

            return FormatResult(result);
        }

        private static object HandleGlitches(JObject @params, IVisualGrader grader)
        {
            string imagePath = @params["image_path"]?.ToString();

            var task = grader.DetectVisualGlitchesAsync(imagePath);
            task.Wait();
            var result = task.Result;

            return FormatResult(result);
        }

        private static List<string> ParseQueries(JToken queriesToken)
        {
            var queries = new List<string>();

            if (queriesToken == null) return queries;

            if (queriesToken is JArray arr)
            {
                foreach (var item in arr)
                {
                    var query = item?.ToString();
                    if (!string.IsNullOrEmpty(query))
                    {
                        queries.Add(query);
                    }
                }
            }
            else if (queriesToken is JValue val)
            {
                var query = val.ToString();
                if (!string.IsNullOrEmpty(query))
                {
                    queries.Add(query);
                }
            }

            return queries;
        }

        private static List<UIElementExpectation> ParseUIElements(JToken elementsToken)
        {
            var elements = new List<UIElementExpectation>();

            if (elementsToken is JArray arr)
            {
                foreach (var item in arr)
                {
                    if (item is JObject obj)
                    {
                        elements.Add(new UIElementExpectation
                        {
                            ElementPath = obj["path"]?.ToString() ?? obj["element_path"]?.ToString(),
                            ShouldBeVisible = obj["visible"]?.Value<bool>() ?? obj["should_be_visible"]?.Value<bool>() ?? true,
                            ExpectedText = obj["text"]?.ToString() ?? obj["expected_text"]?.ToString(),
                            ExpectedPosition = obj["position"]?.ToString() ?? obj["expected_position"]?.ToString()
                        });
                    }
                }
            }

            return elements;
        }

        private static object FormatResult(GraderResult result)
        {
            // Extract base64 image from metadata if present
            string base64Image = null;
            string imagePath = null;
            string prompt = null;

            if (result.Metadata != null)
            {
                if (result.Metadata.TryGetValue("image_base64", out var b64))
                {
                    base64Image = b64?.ToString();
                }
                if (result.Metadata.TryGetValue("image_path", out var path))
                {
                    imagePath = path?.ToString();
                }
                if (result.Metadata.TryGetValue("prompt", out var p))
                {
                    prompt = p?.ToString();
                }
            }

            return new SuccessResponse($"Visual evaluation: {result.Status}", new
            {
                grader_id = result.GraderId,
                tier = "visual",
                status = result.Status.ToString().ToLowerInvariant(),
                score = result.Score,
                is_blocking = result.IsBlocking,
                duration_ms = result.DurationMs,
                summary = result.Summary,

                // Image data for VLM analysis
                image_path = imagePath,
                image_base64 = base64Image,
                prompt = prompt,

                // Instructions for Claude
                vlm_instructions = base64Image != null
                    ? "Use this base64 PNG image with Claude vision to perform the analysis. The 'prompt' field contains the questions to answer."
                    : null,

                // Standard GraderResult fields
                issue_count = result.Issues?.Count ?? 0,
                issues = result.Issues?.Select(i => new
                {
                    severity = i.Severity.ToString().ToLowerInvariant(),
                    code = i.Code,
                    message = i.Message,
                    object_path = i.ObjectPath,
                    suggested_fix = i.SuggestedFix
                }),

                metadata = result.Metadata != null
                    ? result.Metadata
                        .Where(kvp => kvp.Key != "image_base64") // Exclude base64 from metadata (already returned above)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    : null
            });
        }
    }
}
#endif
