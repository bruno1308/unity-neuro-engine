using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NeuroEngine.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Tier 4: Visual grading - VLM screenshot/video analysis.
    /// Prepares visual data for external AI models (Claude, Gemini) to analyze.
    /// This service captures screenshots, encodes them to base64, and formats prompts
    /// for VLM analysis. The actual VLM calls are made externally by Claude/Gemini.
    /// </summary>
    public class VisualGraderService : IVisualGrader
    {
        private const string GRADER_ID_SCREENSHOT = "visual.screenshot";
        private const string GRADER_ID_IMAGE = "visual.image";
        private const string GRADER_ID_UI_LAYOUT = "visual.ui-layout";
        private const string GRADER_ID_GLITCHES = "visual.glitches";

        private readonly string _screenshotDir;

        // Standard queries for visual analysis
        private static readonly List<string> DefaultGlitchQueries = new List<string>
        {
            "Are there any visual glitches, artifacts, or rendering errors visible?",
            "Is any geometry clipping through other objects inappropriately?",
            "Are there any z-fighting (flickering) issues visible?",
            "Are textures loading correctly without missing or placeholder textures?",
            "Are there any floating objects that should be grounded?",
            "Is the lighting appropriate and not causing over-exposure or pitch black areas?"
        };

        public VisualGraderService()
        {
            _screenshotDir = Path.Combine(Application.dataPath, "..", "hooks", "screenshots");
            EnsureDirectoryExists(_screenshotDir);
        }

        public VisualGraderService(string screenshotDirectory)
        {
            _screenshotDir = screenshotDirectory;
            EnsureDirectoryExists(_screenshotDir);
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public async Task<GraderResult> AnalyzeScreenshotAsync(VisualAnalysisConfig config)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                // Capture screenshot
                var captureResult = await CaptureScreenshotAsync(config.CameraPath, config.Width, config.Height);

                if (captureResult.pngBytes == null || captureResult.pngBytes.Length == 0)
                {
                    sw.Stop();
                    return GraderResult.Error(GRADER_ID_SCREENSHOT, EvaluationTier.Visual,
                        "Failed to capture screenshot. Ensure Game View is visible or a camera exists.");
                }

                // Build the prompt for VLM analysis
                var prompt = BuildAnalysisPrompt(config.Queries ?? new List<string> { "Describe what you see in this game screenshot." });

                // Save screenshot for reference
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                var filename = $"analysis_{timestamp}.png";
                var savedPath = Path.Combine(_screenshotDir, filename);
                File.WriteAllBytes(savedPath, captureResult.pngBytes);

                // Convert to base64 for VLM
                var base64Image = Convert.ToBase64String(captureResult.pngBytes);

                sw.Stop();

                // Return result with the prepared data for VLM analysis
                // The actual VLM call happens externally
                return new GraderResult
                {
                    GraderId = GRADER_ID_SCREENSHOT,
                    Tier = EvaluationTier.Visual,
                    Status = GradeStatus.Pass, // Capture succeeded, VLM analysis is external
                    Score = 1.0f,
                    Weight = 0.5f, // Visual checks are supplementary
                    DurationMs = sw.ElapsedMilliseconds,
                    Summary = $"Screenshot captured ({captureResult.pngBytes.Length} bytes). Ready for VLM analysis.",
                    Metadata = new Dictionary<string, object>
                    {
                        { "image_path", savedPath },
                        { "image_base64", base64Image },
                        { "width", captureResult.width },
                        { "height", captureResult.height },
                        { "bytes", captureResult.pngBytes.Length },
                        { "vlm_provider", config.VLMProvider ?? "claude" },
                        { "prompt", prompt },
                        { "queries", config.Queries },
                        { "hint", "Use the base64 image with Claude/Gemini vision to analyze. Parse VLM response to determine pass/fail." }
                    }
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_SCREENSHOT, EvaluationTier.Visual,
                    $"Failed to capture screenshot: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        public async Task<GraderResult> AnalyzeImageAsync(string imagePath, List<string> queries)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                {
                    sw.Stop();
                    return GraderResult.Error(GRADER_ID_IMAGE, EvaluationTier.Visual,
                        $"Image file not found: {imagePath}");
                }

                // Read the image file
                var imageBytes = await Task.Run(() => File.ReadAllBytes(imagePath));
                var base64Image = Convert.ToBase64String(imageBytes);

                // Build the prompt
                var prompt = BuildAnalysisPrompt(queries ?? new List<string> { "Describe what you see in this image." });

                sw.Stop();

                return new GraderResult
                {
                    GraderId = GRADER_ID_IMAGE,
                    Tier = EvaluationTier.Visual,
                    Status = GradeStatus.Pass,
                    Score = 1.0f,
                    Weight = 0.5f,
                    DurationMs = sw.ElapsedMilliseconds,
                    Summary = $"Image loaded ({imageBytes.Length} bytes). Ready for VLM analysis.",
                    Metadata = new Dictionary<string, object>
                    {
                        { "image_path", imagePath },
                        { "image_base64", base64Image },
                        { "bytes", imageBytes.Length },
                        { "prompt", prompt },
                        { "queries", queries },
                        { "hint", "Use the base64 image with Claude/Gemini vision to analyze." }
                    }
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_IMAGE, EvaluationTier.Visual,
                    $"Failed to load image: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        public async Task<GraderResult> VerifyUILayoutAsync(UILayoutExpectation expectation)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                if (expectation == null || expectation.Elements == null || expectation.Elements.Count == 0)
                {
                    sw.Stop();
                    return GraderResult.Skipped(GRADER_ID_UI_LAYOUT, EvaluationTier.Visual,
                        "No UI layout expectations provided");
                }

                // Capture screenshot for UI verification
                var captureResult = await CaptureScreenshotAsync(null, 0, 0);

                if (captureResult.pngBytes == null)
                {
                    sw.Stop();
                    return GraderResult.Error(GRADER_ID_UI_LAYOUT, EvaluationTier.Visual,
                        "Failed to capture screenshot for UI verification");
                }

                // Save screenshot
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                var filename = $"ui_layout_{timestamp}.png";
                var savedPath = Path.Combine(_screenshotDir, filename);
                File.WriteAllBytes(savedPath, captureResult.pngBytes);

                var base64Image = Convert.ToBase64String(captureResult.pngBytes);

                // Build UI-specific prompt
                var queries = new List<string>
                {
                    $"UI Layout Verification: {expectation.Description}"
                };

                foreach (var element in expectation.Elements)
                {
                    if (element.ShouldBeVisible)
                    {
                        queries.Add($"Is there a UI element at '{element.ElementPath}' that is visible?");
                        if (!string.IsNullOrEmpty(element.ExpectedText))
                        {
                            queries.Add($"Does the element at '{element.ElementPath}' show text containing '{element.ExpectedText}'?");
                        }
                        if (!string.IsNullOrEmpty(element.ExpectedPosition))
                        {
                            queries.Add($"Is the element at '{element.ElementPath}' positioned at the {element.ExpectedPosition} of the screen?");
                        }
                    }
                    else
                    {
                        queries.Add($"Verify that '{element.ElementPath}' is NOT visible on screen.");
                    }
                }

                var prompt = BuildAnalysisPrompt(queries);

                sw.Stop();

                return new GraderResult
                {
                    GraderId = GRADER_ID_UI_LAYOUT,
                    Tier = EvaluationTier.Visual,
                    Status = GradeStatus.Pass,
                    Score = 1.0f,
                    Weight = 0.7f,
                    DurationMs = sw.ElapsedMilliseconds,
                    Summary = $"UI layout screenshot captured. Ready for VLM verification with {expectation.Elements.Count} elements to check.",
                    Metadata = new Dictionary<string, object>
                    {
                        { "image_path", savedPath },
                        { "image_base64", base64Image },
                        { "width", captureResult.width },
                        { "height", captureResult.height },
                        { "description", expectation.Description },
                        { "element_count", expectation.Elements.Count },
                        { "prompt", prompt },
                        { "elements", SerializeUIElements(expectation.Elements) },
                        { "hint", "Use VLM to verify each UI element expectation. Return pass/fail for each." }
                    }
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_UI_LAYOUT, EvaluationTier.Visual,
                    $"Failed to verify UI layout: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        public async Task<GraderResult> DetectVisualGlitchesAsync(string imagePath = null)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                byte[] imageBytes;
                string savedPath;

                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    // Use provided image
                    imageBytes = await Task.Run(() => File.ReadAllBytes(imagePath));
                    savedPath = imagePath;
                }
                else
                {
                    // Capture new screenshot
                    var captureResult = await CaptureScreenshotAsync(null, 0, 0);

                    if (captureResult.pngBytes == null)
                    {
                        sw.Stop();
                        return GraderResult.Error(GRADER_ID_GLITCHES, EvaluationTier.Visual,
                            "Failed to capture screenshot for glitch detection");
                    }

                    imageBytes = captureResult.pngBytes;

                    // Save screenshot
                    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                    var filename = $"glitch_check_{timestamp}.png";
                    savedPath = Path.Combine(_screenshotDir, filename);
                    File.WriteAllBytes(savedPath, imageBytes);
                }

                var base64Image = Convert.ToBase64String(imageBytes);

                // Build glitch detection prompt
                var prompt = BuildGlitchDetectionPrompt();

                sw.Stop();

                return new GraderResult
                {
                    GraderId = GRADER_ID_GLITCHES,
                    Tier = EvaluationTier.Visual,
                    Status = GradeStatus.Pass,
                    Score = 1.0f,
                    Weight = 0.6f,
                    DurationMs = sw.ElapsedMilliseconds,
                    Summary = $"Screenshot ready for visual glitch analysis ({imageBytes.Length} bytes).",
                    Metadata = new Dictionary<string, object>
                    {
                        { "image_path", savedPath },
                        { "image_base64", base64Image },
                        { "bytes", imageBytes.Length },
                        { "prompt", prompt },
                        { "glitch_queries", DefaultGlitchQueries },
                        { "hint", "Analyze the image for visual glitches. Return issues found or confirm clean render." }
                    }
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_GLITCHES, EvaluationTier.Visual,
                    $"Failed to detect visual glitches: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        #region Helper Methods

        /// <summary>
        /// Captures a screenshot synchronously. Must be called from the main thread.
        /// Returns a Task for API consistency but executes synchronously.
        /// </summary>
        private Task<(byte[] pngBytes, int width, int height)> CaptureScreenshotAsync(string cameraPath, int requestedWidth, int requestedHeight)
        {
            // Unity API must run on main thread - execute synchronously
            var result = CaptureScreenshotSync(cameraPath, requestedWidth, requestedHeight);
            return Task.FromResult(result);
        }

        /// <summary>
        /// Synchronous screenshot capture using RenderTexture.
        /// </summary>
        private (byte[] pngBytes, int width, int height) CaptureScreenshotSync(string cameraPath, int requestedWidth, int requestedHeight)
        {
            // Find camera
            Camera camera = null;

            if (!string.IsNullOrEmpty(cameraPath))
            {
                var cameraGO = GameObject.Find(cameraPath);
                if (cameraGO != null)
                {
                    camera = cameraGO.GetComponent<Camera>();
                }
            }

            if (camera == null)
            {
                camera = Camera.main;
            }

            if (camera == null)
            {
                camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            }

            if (camera == null)
            {
                return (null, 0, 0);
            }

            // Use camera dimensions if not specified
            int width = requestedWidth > 0 ? requestedWidth : camera.pixelWidth;
            int height = requestedHeight > 0 ? requestedHeight : camera.pixelHeight;

            // Create render texture
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;

            // Store original camera target
            var originalTarget = camera.targetTexture;

            try
            {
                // Render to our texture
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = originalTarget;

                // Read pixels
                RenderTexture.active = rt;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = null;

                // Encode to PNG
                byte[] pngBytes = texture.EncodeToPNG();

                // Cleanup
                UnityEngine.Object.DestroyImmediate(texture);

                return (pngBytes, width, height);
            }
            finally
            {
                // Always cleanup render texture
                if (rt != null)
                {
                    UnityEngine.Object.DestroyImmediate(rt);
                }
            }
        }

        private string BuildAnalysisPrompt(List<string> queries)
        {
            var prompt = "Analyze this game screenshot and answer the following questions:\n\n";

            for (int i = 0; i < queries.Count; i++)
            {
                prompt += $"{i + 1}. {queries[i]}\n";
            }

            prompt += "\nProvide a clear answer for each question. ";
            prompt += "If you identify any issues, describe them specifically with location in the image.";

            return prompt;
        }

        private string BuildGlitchDetectionPrompt()
        {
            var prompt = "Analyze this game screenshot for visual glitches and rendering issues.\n\n";
            prompt += "Check for the following:\n";

            for (int i = 0; i < DefaultGlitchQueries.Count; i++)
            {
                prompt += $"{i + 1}. {DefaultGlitchQueries[i]}\n";
            }

            prompt += "\nFor each issue found, describe:\n";
            prompt += "- What the issue is\n";
            prompt += "- Where it appears in the image (location/region)\n";
            prompt += "- Severity (minor, moderate, severe)\n\n";
            prompt += "If no issues are found, confirm the image appears clean and properly rendered.";

            return prompt;
        }

        private List<Dictionary<string, object>> SerializeUIElements(List<UIElementExpectation> elements)
        {
            var result = new List<Dictionary<string, object>>();

            foreach (var element in elements)
            {
                result.Add(new Dictionary<string, object>
                {
                    { "path", element.ElementPath },
                    { "should_be_visible", element.ShouldBeVisible },
                    { "expected_text", element.ExpectedText },
                    { "expected_position", element.ExpectedPosition }
                });
            }

            return result;
        }

        #endregion
    }
}
