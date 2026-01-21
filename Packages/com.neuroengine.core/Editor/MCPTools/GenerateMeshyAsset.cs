#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using NeuroEngine.Services;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to generate 3D assets using Meshy.ai.
    /// Part of Layer 7 (Generative Asset Pipeline).
    ///
    /// Workflow:
    /// 1. generate_image: Create concept image from text prompt
    /// 2. get_image_status: Poll until complete
    /// 3. generate_mesh: Convert image to 3D mesh
    /// 4. get_mesh_status: Poll until complete
    /// 5. download: Download FBX and textures to Unity project
    /// </summary>
    [McpForUnityTool("generate_meshy_asset", Description = "Generate 3D assets using Meshy.ai. Actions: generate_image, get_image_status, generate_mesh, get_mesh_status, download, full_pipeline. Returns job IDs for async operations.")]
    public static class GenerateMeshyAsset
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();

            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse(
                    "Required parameter 'action' is missing. " +
                    "Use: generate_image, get_image_status, generate_mesh, get_mesh_status, download, full_pipeline");
            }

            var meshyService = GetMeshyService();
            if (meshyService == null)
            {
                return new ErrorResponse("Failed to initialize MeshyService. Check configuration.");
            }

            if (!meshyService.IsConfigured)
            {
                return new ErrorResponse(
                    "Meshy API key not configured. Set MESHY_API_KEY in .env file.");
            }

            try
            {
                return action switch
                {
                    "generate_image" => HandleGenerateImage(@params, meshyService),
                    "get_image_status" => HandleGetImageStatus(@params, meshyService),
                    "generate_mesh" => HandleGenerateMesh(@params, meshyService),
                    "get_mesh_status" => HandleGetMeshStatus(@params, meshyService),
                    "download" => HandleDownload(@params, meshyService),
                    "full_pipeline" => HandleFullPipeline(@params, meshyService),
                    _ => new ErrorResponse($"Unknown action '{action}'. Use: generate_image, get_image_status, generate_mesh, get_mesh_status, download, full_pipeline")
                };
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error in Meshy operation: {e.Message}");
            }
        }

        private static IMeshyService GetMeshyService()
        {
            // Try EditorServiceLocator first
            try
            {
                return EditorServiceLocator.Get<IMeshyService>();
            }
            catch
            {
                // Fall back to creating manually
                var config = new EnvConfigService();
                return new MeshyService(config);
            }
        }

        private static object HandleGenerateImage(JObject @params, IMeshyService meshyService)
        {
            string prompt = @params["prompt"]?.ToString();
            if (string.IsNullOrEmpty(prompt))
            {
                return new ErrorResponse("Required parameter 'prompt' is missing.");
            }

            string aspectRatio = @params["aspect_ratio"]?.ToString() ?? "1:1";

            var task = Task.Run(() => meshyService.GenerateImageAsync(prompt, aspectRatio));
            var result = task.GetAwaiter().GetResult();

            if (result.Status == MeshyJobStatus.Failed)
            {
                return new ErrorResponse($"Image generation failed: {result.ErrorMessage}");
            }

            return new SuccessResponse($"Image generation started", new
            {
                job_id = result.JobId,
                status = result.Status.ToString().ToLowerInvariant(),
                prompt = prompt,
                aspect_ratio = aspectRatio,
                next_step = "Use get_image_status to poll until status is 'succeeded'"
            });
        }

        private static object HandleGetImageStatus(JObject @params, IMeshyService meshyService)
        {
            string jobId = @params["job_id"]?.ToString();
            if (string.IsNullOrEmpty(jobId))
            {
                return new ErrorResponse("Required parameter 'job_id' is missing.");
            }

            var task = Task.Run(() => meshyService.GetImageJobStatusAsync(jobId));
            var result = task.GetAwaiter().GetResult();

            if (result.Status == MeshyJobStatus.Failed)
            {
                return new ErrorResponse($"Image job failed: {result.ErrorMessage}");
            }

            var responseData = new
            {
                job_id = result.JobId,
                status = result.Status.ToString().ToLowerInvariant(),
                progress_percent = result.ProgressPercent,
                image_url = result.ImageUrl,
                next_step = result.Status == MeshyJobStatus.Succeeded
                    ? "Use generate_mesh with this image_url"
                    : "Continue polling with get_image_status"
            };

            return new SuccessResponse($"Image job status: {result.Status}", responseData);
        }

        private static object HandleGenerateMesh(JObject @params, IMeshyService meshyService)
        {
            string imageUrl = @params["image_url"]?.ToString();
            if (string.IsNullOrEmpty(imageUrl))
            {
                return new ErrorResponse("Required parameter 'image_url' is missing.");
            }

            var config = new MeshyMeshConfig
            {
                AiModel = @params["ai_model"]?.ToString() ?? "latest",
                Topology = @params["topology"]?.ToString() ?? "triangle",
                TargetPolycount = @params["target_polycount"]?.Value<int>() ?? 30000,
                EnablePbr = @params["enable_pbr"]?.Value<bool>() ?? true
            };

            var task = Task.Run(() => meshyService.GenerateMeshFromImageAsync(imageUrl, config));
            var result = task.GetAwaiter().GetResult();

            if (result.Status == MeshyJobStatus.Failed)
            {
                return new ErrorResponse($"Mesh generation failed: {result.ErrorMessage}");
            }

            return new SuccessResponse($"Mesh generation started", new
            {
                job_id = result.JobId,
                status = result.Status.ToString().ToLowerInvariant(),
                config = new
                {
                    ai_model = config.AiModel,
                    topology = config.Topology,
                    target_polycount = config.TargetPolycount,
                    enable_pbr = config.EnablePbr
                },
                next_step = "Use get_mesh_status to poll until status is 'succeeded'"
            });
        }

        private static object HandleGetMeshStatus(JObject @params, IMeshyService meshyService)
        {
            string jobId = @params["job_id"]?.ToString();
            if (string.IsNullOrEmpty(jobId))
            {
                return new ErrorResponse("Required parameter 'job_id' is missing.");
            }

            var task = Task.Run(() => meshyService.GetMeshJobStatusAsync(jobId));
            var result = task.GetAwaiter().GetResult();

            if (result.Status == MeshyJobStatus.Failed)
            {
                return new ErrorResponse($"Mesh job failed: {result.ErrorMessage}");
            }

            var responseData = new
            {
                job_id = result.JobId,
                status = result.Status.ToString().ToLowerInvariant(),
                progress_percent = result.ProgressPercent,
                fbx_url = result.FbxUrl,
                glb_url = result.GlbUrl,
                texture_urls = result.TextureUrls,
                next_step = result.Status == MeshyJobStatus.Succeeded
                    ? "Use download to save FBX and textures to Unity project"
                    : "Continue polling with get_mesh_status"
            };

            return new SuccessResponse($"Mesh job status: {result.Status}", responseData);
        }

        private static object HandleDownload(JObject @params, IMeshyService meshyService)
        {
            string url = @params["url"]?.ToString();
            string destinationPath = @params["destination_path"]?.ToString();

            if (string.IsNullOrEmpty(url))
            {
                return new ErrorResponse("Required parameter 'url' is missing.");
            }

            if (string.IsNullOrEmpty(destinationPath))
            {
                // Generate default path from URL
                var filename = Path.GetFileName(new Uri(url).LocalPath);
                destinationPath = $"Assets/Models/Generated/{filename}";
            }

            // Ensure it starts with Assets/
            if (!destinationPath.StartsWith("Assets/"))
            {
                destinationPath = $"Assets/Models/Generated/{destinationPath}";
            }

            var task = Task.Run(() => meshyService.DownloadAssetAsync(url, destinationPath));
            var fullPath = task.GetAwaiter().GetResult();

            // Trigger asset import
            AssetDatabase.Refresh();

            return new SuccessResponse($"Asset downloaded", new
            {
                url = url,
                saved_to = destinationPath,
                full_path = fullPath,
                hint = "Use manage_asset to configure import settings"
            });
        }

        private static object HandleFullPipeline(JObject @params, IMeshyService meshyService)
        {
            string prompt = @params["prompt"]?.ToString();
            if (string.IsNullOrEmpty(prompt))
            {
                return new ErrorResponse("Required parameter 'prompt' is missing.");
            }

            string aspectRatio = @params["aspect_ratio"]?.ToString() ?? "1:1";
            string assetName = @params["asset_name"]?.ToString();
            int imagePollMs = @params["image_poll_interval_ms"]?.Value<int>() ?? 5000;
            int meshPollMs = @params["mesh_poll_interval_ms"]?.Value<int>() ?? 10000;
            int imageTimeoutMs = @params["image_timeout_ms"]?.Value<int>() ?? 300000;
            int meshTimeoutMs = @params["mesh_timeout_ms"]?.Value<int>() ?? 600000;

            // Generate asset name from prompt if not provided
            if (string.IsNullOrEmpty(assetName))
            {
                assetName = SanitizeFilename(prompt);
                if (assetName.Length > 50)
                    assetName = assetName.Substring(0, 50);
            }

            var config = new MeshyMeshConfig
            {
                AiModel = @params["ai_model"]?.ToString() ?? "latest",
                Topology = @params["topology"]?.ToString() ?? "triangle",
                TargetPolycount = @params["target_polycount"]?.Value<int>() ?? 30000,
                EnablePbr = @params["enable_pbr"]?.Value<bool>() ?? true
            };

            // Step 1: Generate image
            Debug.Log($"[GenerateMeshyAsset] Starting image generation for: {prompt}");
            var imageTask = Task.Run(() => meshyService.GenerateImageAndWaitAsync(
                prompt, aspectRatio, imagePollMs, imageTimeoutMs));
            var imageResult = imageTask.GetAwaiter().GetResult();

            if (imageResult.Status != MeshyJobStatus.Succeeded)
            {
                return new ErrorResponse($"Image generation failed: {imageResult.ErrorMessage}");
            }

            Debug.Log($"[GenerateMeshyAsset] Image generated: {imageResult.ImageUrl}");

            // Step 2: Generate mesh from image
            Debug.Log($"[GenerateMeshyAsset] Starting mesh generation...");
            var meshTask = Task.Run(() => meshyService.GenerateMeshAndWaitAsync(
                imageResult.ImageUrl, config, meshPollMs, meshTimeoutMs));
            var meshResult = meshTask.GetAwaiter().GetResult();

            if (meshResult.Status != MeshyJobStatus.Succeeded)
            {
                return new ErrorResponse($"Mesh generation failed: {meshResult.ErrorMessage}");
            }

            Debug.Log($"[GenerateMeshyAsset] Mesh generated. Downloading...");

            // Step 3: Download FBX
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fbxPath = $"Assets/Models/Generated/{assetName}_{timestamp}.fbx";

            var downloadTask = Task.Run(() => meshyService.DownloadAssetAsync(meshResult.FbxUrl, fbxPath));
            var savedPath = downloadTask.GetAwaiter().GetResult();

            // Step 4: Download textures
            var textureFolder = $"Assets/Models/Generated/{assetName}_{timestamp}_Textures";
            var downloadedTextures = new System.Collections.Generic.List<string>();

            if (meshResult.TextureUrls != null)
            {
                foreach (var kvp in meshResult.TextureUrls)
                {
                    var texturePath = $"{textureFolder}/{kvp.Key}.png";
                    try
                    {
                        var textureDownloadTask = Task.Run(() =>
                            meshyService.DownloadAssetAsync(kvp.Value, texturePath));
                        textureDownloadTask.GetAwaiter().GetResult();
                        downloadedTextures.Add(texturePath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[GenerateMeshyAsset] Failed to download texture {kvp.Key}: {e.Message}");
                    }
                }
            }

            // Refresh asset database
            AssetDatabase.Refresh();

            Debug.Log($"[GenerateMeshyAsset] Pipeline complete. FBX saved to: {fbxPath}");

            return new SuccessResponse($"Full pipeline completed", new
            {
                prompt = prompt,
                image_job_id = imageResult.JobId,
                mesh_job_id = meshResult.JobId,
                image_url = imageResult.ImageUrl,
                fbx_path = fbxPath,
                textures = downloadedTextures,
                hint = "Model imported. You may need to set up URP materials using AutodeskInteractive shader."
            });
        }

        private static string SanitizeFilename(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new System.Text.StringBuilder();

            foreach (var c in name)
            {
                if (Array.IndexOf(invalidChars, c) < 0 && c != ' ')
                {
                    sanitized.Append(c);
                }
                else if (c == ' ')
                {
                    sanitized.Append('_');
                }
            }

            return sanitized.ToString();
        }
    }
}
#endif
