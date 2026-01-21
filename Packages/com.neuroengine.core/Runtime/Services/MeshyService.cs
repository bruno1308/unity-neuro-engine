using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NeuroEngine.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Implementation of Meshy.ai 3D asset generation service.
    /// Uses UnityWebRequest for HTTP calls.
    /// Part of Layer 7 (Generative Asset Pipeline).
    /// </summary>
    public class MeshyService : IMeshyService
    {
        private const string BaseUrl = "https://api.meshy.ai/openapi/v1";
        private const string ImageModel = "nano-banana-pro";

        private readonly IEnvConfig _config;
        private readonly string _generatedModelsPath;

        public MeshyService(IEnvConfig config)
        {
            _config = config;
            _generatedModelsPath = Path.Combine(Application.dataPath, "Models", "Generated");
        }

        public bool IsConfigured =>
            !string.IsNullOrEmpty(_config.MeshyApiKey) &&
            !_config.MeshyApiKey.Contains("your_");

        public async Task<MeshyJobResult> GenerateImageAsync(
            string prompt,
            string aspectRatio = "1:1",
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return new MeshyJobResult
                {
                    Status = MeshyJobStatus.Failed,
                    ErrorMessage = "Meshy API key not configured. Set MESHY_API_KEY in .env file."
                };
            }

            var requestBody = new
            {
                ai_model = ImageModel,
                prompt = $"{prompt}, isometric view, single object on dark background, game-ready asset",
                aspect_ratio = aspectRatio
            };

            var result = await PostJsonAsync(
                $"{BaseUrl}/text-to-image",
                requestBody,
                cancellationToken);

            if (result.Status == MeshyJobStatus.Failed)
                return result;

            // Parse response to get job ID
            try
            {
                var response = JObject.Parse(result.ErrorMessage ?? "{}");
                result.JobId = response["result"]?.ToString();
                result.Status = MeshyJobStatus.Pending;
                result.ErrorMessage = null;
            }
            catch (Exception e)
            {
                result.Status = MeshyJobStatus.Failed;
                result.ErrorMessage = $"Failed to parse response: {e.Message}";
            }

            return result;
        }

        public async Task<MeshyJobResult> GetImageJobStatusAsync(
            string jobId,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return new MeshyJobResult
                {
                    JobId = jobId,
                    Status = MeshyJobStatus.Failed,
                    ErrorMessage = "Meshy API key not configured."
                };
            }

            var result = await GetJsonAsync(
                $"{BaseUrl}/text-to-image/{jobId}",
                cancellationToken);

            if (result.Status == MeshyJobStatus.Failed)
            {
                result.JobId = jobId;
                return result;
            }

            // Parse response
            try
            {
                var response = JObject.Parse(result.ErrorMessage ?? "{}");
                result.JobId = jobId;
                result.Status = ParseStatus(response["status"]?.ToString());
                result.ProgressPercent = response["progress"]?.Value<int>() ?? 0;
                result.ImageUrl = response["image_url"]?.ToString();
                result.TaskError = response["task_error"]?.ToString();
                result.ErrorMessage = result.Status == MeshyJobStatus.Failed
                    ? result.TaskError
                    : null;
            }
            catch (Exception e)
            {
                result.JobId = jobId;
                result.Status = MeshyJobStatus.Failed;
                result.ErrorMessage = $"Failed to parse response: {e.Message}";
            }

            return result;
        }

        public async Task<MeshyJobResult> GenerateMeshFromImageAsync(
            string imageUrl,
            MeshyMeshConfig config = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return new MeshyJobResult
                {
                    Status = MeshyJobStatus.Failed,
                    ErrorMessage = "Meshy API key not configured. Set MESHY_API_KEY in .env file."
                };
            }

            config ??= new MeshyMeshConfig();

            var requestBody = new
            {
                image_url = imageUrl,
                ai_model = config.AiModel,
                topology = config.Topology,
                target_polycount = config.TargetPolycount,
                enable_pbr = config.EnablePbr
            };

            var result = await PostJsonAsync(
                $"{BaseUrl}/image-to-3d",
                requestBody,
                cancellationToken);

            if (result.Status == MeshyJobStatus.Failed)
                return result;

            // Parse response to get job ID
            try
            {
                var response = JObject.Parse(result.ErrorMessage ?? "{}");
                result.JobId = response["result"]?.ToString();
                result.Status = MeshyJobStatus.Pending;
                result.ErrorMessage = null;
            }
            catch (Exception e)
            {
                result.Status = MeshyJobStatus.Failed;
                result.ErrorMessage = $"Failed to parse response: {e.Message}";
            }

            return result;
        }

        public async Task<MeshyJobResult> GetMeshJobStatusAsync(
            string jobId,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return new MeshyJobResult
                {
                    JobId = jobId,
                    Status = MeshyJobStatus.Failed,
                    ErrorMessage = "Meshy API key not configured."
                };
            }

            var result = await GetJsonAsync(
                $"{BaseUrl}/image-to-3d/{jobId}",
                cancellationToken);

            if (result.Status == MeshyJobStatus.Failed)
            {
                result.JobId = jobId;
                return result;
            }

            // Parse response
            try
            {
                var response = JObject.Parse(result.ErrorMessage ?? "{}");
                result.JobId = jobId;
                result.Status = ParseStatus(response["status"]?.ToString());
                result.ProgressPercent = response["progress"]?.Value<int>() ?? 0;
                result.TaskError = response["task_error"]?.ToString();

                // Parse model URLs
                var modelUrls = response["model_urls"] as JObject;
                if (modelUrls != null)
                {
                    result.FbxUrl = modelUrls["fbx"]?.ToString();
                    result.GlbUrl = modelUrls["glb"]?.ToString();
                }

                // Parse texture URLs
                var textureUrls = response["texture_urls"] as JObject;
                if (textureUrls != null)
                {
                    result.TextureUrls = new Dictionary<string, string>();
                    foreach (var prop in textureUrls.Properties())
                    {
                        result.TextureUrls[prop.Name] = prop.Value?.ToString();
                    }
                }

                result.ErrorMessage = result.Status == MeshyJobStatus.Failed
                    ? result.TaskError
                    : null;
            }
            catch (Exception e)
            {
                result.JobId = jobId;
                result.Status = MeshyJobStatus.Failed;
                result.ErrorMessage = $"Failed to parse response: {e.Message}";
            }

            return result;
        }

        public async Task<string> DownloadAssetAsync(
            string url,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            // Ensure destination directory exists
            var fullPath = destinationPath.StartsWith("Assets/")
                ? Path.Combine(Application.dataPath, "..", destinationPath)
                : Path.Combine(_generatedModelsPath, destinationPath);

            fullPath = Path.GetFullPath(fullPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerFile(fullPath);

            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    throw new OperationCanceledException(cancellationToken);
                }
                await Task.Delay(100, cancellationToken);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception($"Download failed: {request.error}");
            }

            Debug.Log($"[MeshyService] Downloaded asset to: {fullPath}");
            return fullPath;
        }

        public async Task<MeshyJobResult> GenerateImageAndWaitAsync(
            string prompt,
            string aspectRatio = "1:1",
            int pollIntervalMs = 5000,
            int timeoutMs = 300000,
            CancellationToken cancellationToken = default)
        {
            var result = await GenerateImageAsync(prompt, aspectRatio, cancellationToken);
            if (result.Status == MeshyJobStatus.Failed)
                return result;

            var startTime = DateTime.UtcNow;

            while (result.Status == MeshyJobStatus.Pending ||
                   result.Status == MeshyJobStatus.Processing)
            {
                // Check timeout
                if ((DateTime.UtcNow - startTime).TotalMilliseconds > timeoutMs)
                {
                    result.Status = MeshyJobStatus.Failed;
                    result.ErrorMessage = "Image generation timed out.";
                    return result;
                }

                await Task.Delay(pollIntervalMs, cancellationToken);
                result = await GetImageJobStatusAsync(result.JobId, cancellationToken);
            }

            return result;
        }

        public async Task<MeshyJobResult> GenerateMeshAndWaitAsync(
            string imageUrl,
            MeshyMeshConfig config = null,
            int pollIntervalMs = 10000,
            int timeoutMs = 600000,
            CancellationToken cancellationToken = default)
        {
            var result = await GenerateMeshFromImageAsync(imageUrl, config, cancellationToken);
            if (result.Status == MeshyJobStatus.Failed)
                return result;

            var startTime = DateTime.UtcNow;

            while (result.Status == MeshyJobStatus.Pending ||
                   result.Status == MeshyJobStatus.Processing)
            {
                // Check timeout
                if ((DateTime.UtcNow - startTime).TotalMilliseconds > timeoutMs)
                {
                    result.Status = MeshyJobStatus.Failed;
                    result.ErrorMessage = "Mesh generation timed out.";
                    return result;
                }

                await Task.Delay(pollIntervalMs, cancellationToken);
                result = await GetMeshJobStatusAsync(result.JobId, cancellationToken);

                Debug.Log($"[MeshyService] Mesh generation progress: {result.ProgressPercent}%");
            }

            return result;
        }

        #region HTTP Helpers

        private async Task<MeshyJobResult> PostJsonAsync(
            string url,
            object body,
            CancellationToken cancellationToken)
        {
            var json = JsonConvert.SerializeObject(body);
            var bodyBytes = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {_config.MeshyApiKey}");

            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    throw new OperationCanceledException(cancellationToken);
                }
                await Task.Delay(50, cancellationToken);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                return new MeshyJobResult
                {
                    Status = MeshyJobStatus.Failed,
                    ErrorMessage = $"HTTP {request.responseCode}: {request.error}. Response: {request.downloadHandler?.text}"
                };
            }

            // Return raw response in ErrorMessage field for parsing
            return new MeshyJobResult
            {
                Status = MeshyJobStatus.Pending,
                ErrorMessage = request.downloadHandler.text
            };
        }

        private async Task<MeshyJobResult> GetJsonAsync(
            string url,
            CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {_config.MeshyApiKey}");

            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    throw new OperationCanceledException(cancellationToken);
                }
                await Task.Delay(50, cancellationToken);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                return new MeshyJobResult
                {
                    Status = MeshyJobStatus.Failed,
                    ErrorMessage = $"HTTP {request.responseCode}: {request.error}. Response: {request.downloadHandler?.text}"
                };
            }

            // Return raw response in ErrorMessage field for parsing
            return new MeshyJobResult
            {
                Status = MeshyJobStatus.Pending,
                ErrorMessage = request.downloadHandler.text
            };
        }

        private static MeshyJobStatus ParseStatus(string status)
        {
            return status?.ToUpperInvariant() switch
            {
                "PENDING" => MeshyJobStatus.Pending,
                "IN_PROGRESS" => MeshyJobStatus.Processing,
                "PROCESSING" => MeshyJobStatus.Processing,
                "SUCCEEDED" => MeshyJobStatus.Succeeded,
                "FAILED" => MeshyJobStatus.Failed,
                "EXPIRED" => MeshyJobStatus.Expired,
                _ => MeshyJobStatus.Pending
            };
        }

        #endregion
    }
}
