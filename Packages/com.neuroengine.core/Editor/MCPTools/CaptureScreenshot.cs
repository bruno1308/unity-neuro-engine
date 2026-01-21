#if UNITY_EDITOR
using System;
using System.IO;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to capture screenshots for VLM analysis.
    /// Part of Layer 5 (Evaluation) - enables visual verification.
    /// Returns base64 image for Claude vision analysis.
    /// </summary>
    [McpForUnityTool("capture_screenshot", Description = "Captures a screenshot from the Game View. Returns base64-encoded PNG for visual analysis. Use during Play Mode for runtime screenshots, or in Edit Mode for scene view.")]
    public static class CaptureScreenshot
    {
        private static string _screenshotDir;

        public static object HandleCommand(JObject @params)
        {
            bool returnBase64 = @params["return_base64"]?.Value<bool>() ?? true;
            bool saveToFile = @params["save_to_file"]?.Value<bool>() ?? false;
            string filename = @params["filename"]?.ToString();
            int superSize = @params["super_size"]?.Value<int>() ?? 1;

            try
            {
                // Ensure screenshot directory exists
                if (string.IsNullOrEmpty(_screenshotDir))
                {
                    _screenshotDir = Path.Combine(Application.dataPath, "..", "hooks", "screenshots");
                    Directory.CreateDirectory(_screenshotDir);
                }

                // Generate filename if not provided
                if (string.IsNullOrEmpty(filename))
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                    filename = $"screenshot_{timestamp}.png";
                }

                if (!filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    filename += ".png";

                var fullPath = Path.Combine(_screenshotDir, filename);

                // Capture screenshot
                // Note: ScreenCapture.CaptureScreenshot is async - we need to wait for it
                // For synchronous capture, we use RenderTexture approach
                byte[] pngBytes = CaptureGameView(superSize);

                if (pngBytes == null || pngBytes.Length == 0)
                {
                    return new ErrorResponse("Failed to capture screenshot. Ensure Game View is visible.");
                }

                // Save to file if requested
                if (saveToFile)
                {
                    File.WriteAllBytes(fullPath, pngBytes);
                }

                // Build response
                var responseData = new
                {
                    success = true,
                    width = Screen.width * superSize,
                    height = Screen.height * superSize,
                    bytes = pngBytes.Length,
                    saved_to = saveToFile ? fullPath : null,
                    base64 = returnBase64 ? Convert.ToBase64String(pngBytes) : null,
                    hint = returnBase64
                        ? "Use this base64 image with Claude's vision capability to analyze the screenshot"
                        : "Screenshot captured. Set return_base64=true to get image data for analysis"
                };

                return new SuccessResponse(
                    $"Screenshot captured ({pngBytes.Length} bytes)",
                    responseData
                );
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error capturing screenshot: {e.Message}");
            }
        }

        /// <summary>
        /// Captures the Game View synchronously using RenderTexture.
        /// </summary>
        private static byte[] CaptureGameView(int superSize)
        {
            // Try to find a camera
            Camera camera = Camera.main;
            if (camera == null)
            {
                // Find any camera
                camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            }

            if (camera == null)
            {
                // No camera - try screen capture fallback (async, will save to file)
                Debug.LogWarning("[CaptureScreenshot] No camera found. Using fallback capture.");
                return CaptureScreenFallback(superSize);
            }

            int width = camera.pixelWidth * superSize;
            int height = camera.pixelHeight * superSize;

            // Create render texture
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;

            // Store original camera target
            var originalTarget = camera.targetTexture;

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
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(texture);

            return pngBytes;
        }

        /// <summary>
        /// Fallback capture when no camera is available.
        /// Uses Unity's built-in screen capture.
        /// </summary>
        private static byte[] CaptureScreenFallback(int superSize)
        {
            // Create a temporary file path
            var tempPath = Path.Combine(Application.temporaryCachePath, $"temp_screenshot_{Guid.NewGuid()}.png");

            // This is async, but we can wait for it
            ScreenCapture.CaptureScreenshot(tempPath, superSize);

            // Wait for file to exist (with timeout)
            float startTime = Time.realtimeSinceStartup;
            while (!File.Exists(tempPath) && Time.realtimeSinceStartup - startTime < 5f)
            {
                System.Threading.Thread.Sleep(100);
            }

            if (!File.Exists(tempPath))
            {
                return null;
            }

            var bytes = File.ReadAllBytes(tempPath);

            // Cleanup temp file
            try { File.Delete(tempPath); } catch { }

            return bytes;
        }
    }
}
#endif
