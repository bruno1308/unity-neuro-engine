using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Result from Meshy.ai API operations.
    /// Contains job status and asset URLs when complete.
    /// </summary>
    public class MeshyJobResult
    {
        public string JobId { get; set; }
        public MeshyJobStatus Status { get; set; }
        public string ErrorMessage { get; set; }

        // For image generation
        public string ImageUrl { get; set; }

        // For 3D generation
        public string FbxUrl { get; set; }
        public string GlbUrl { get; set; }
        public Dictionary<string, string> TextureUrls { get; set; }

        // Progress info
        public int ProgressPercent { get; set; }
        public string TaskError { get; set; }
    }

    /// <summary>
    /// Status of a Meshy job.
    /// </summary>
    public enum MeshyJobStatus
    {
        Pending,
        Processing,
        Succeeded,
        Failed,
        Expired
    }

    /// <summary>
    /// Configuration for 3D mesh generation from image.
    /// </summary>
    public class MeshyMeshConfig
    {
        /// <summary>
        /// AI model to use. Default is "latest" for Meshy 6 Preview.
        /// </summary>
        public string AiModel { get; set; } = "latest";

        /// <summary>
        /// Topology type: "triangle" or "quad".
        /// </summary>
        public string Topology { get; set; } = "triangle";

        /// <summary>
        /// Target polygon count. Default is 30000.
        /// </summary>
        public int TargetPolycount { get; set; } = 30000;

        /// <summary>
        /// Enable PBR textures (albedo, normal, metallic, roughness).
        /// </summary>
        public bool EnablePbr { get; set; } = true;
    }

    /// <summary>
    /// Interface for Meshy.ai 3D asset generation service.
    /// Implements Layer 7 (Generative Asset Pipeline).
    ///
    /// Workflow: Text -> Image -> 3D
    /// 1. GenerateImageAsync() to create concept image
    /// 2. GenerateMeshFromImageAsync() to convert to 3D
    /// 3. DownloadAssetAsync() to import into Unity
    /// </summary>
    public interface IMeshyService
    {
        /// <summary>
        /// Generate a concept image from text prompt.
        /// Uses nano-banana-pro model for game-ready assets.
        /// </summary>
        /// <param name="prompt">Text description of the asset (e.g., "stone tower, medieval style")</param>
        /// <param name="aspectRatio">Aspect ratio (e.g., "1:1", "16:9", "4:3")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Job result with job ID for polling</returns>
        Task<MeshyJobResult> GenerateImageAsync(
            string prompt,
            string aspectRatio = "1:1",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the status of an image generation job.
        /// Poll until status is Succeeded or Failed.
        /// </summary>
        /// <param name="jobId">Job ID from GenerateImageAsync</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Job result with image URL when complete</returns>
        Task<MeshyJobResult> GetImageJobStatusAsync(
            string jobId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate a 3D mesh from a concept image.
        /// Uses Meshy 6 Preview model for best quality.
        /// </summary>
        /// <param name="imageUrl">URL of the source image</param>
        /// <param name="config">Mesh generation configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Job result with job ID for polling</returns>
        Task<MeshyJobResult> GenerateMeshFromImageAsync(
            string imageUrl,
            MeshyMeshConfig config = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the status of a mesh generation job.
        /// Poll until status is Succeeded or Failed.
        /// </summary>
        /// <param name="jobId">Job ID from GenerateMeshFromImageAsync</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Job result with FBX/GLB URLs when complete</returns>
        Task<MeshyJobResult> GetMeshJobStatusAsync(
            string jobId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Download an asset (FBX, texture, etc.) to the Unity project.
        /// </summary>
        /// <param name="url">URL of the asset to download</param>
        /// <param name="destinationPath">Path within Assets/ (e.g., "Models/Generated/tower.fbx")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Full path to the downloaded file</returns>
        Task<string> DownloadAssetAsync(
            string url,
            string destinationPath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate image and wait for completion (convenience method).
        /// Polls automatically with configurable interval.
        /// </summary>
        /// <param name="prompt">Text description of the asset</param>
        /// <param name="aspectRatio">Aspect ratio</param>
        /// <param name="pollIntervalMs">Polling interval in milliseconds (default 5000)</param>
        /// <param name="timeoutMs">Timeout in milliseconds (default 300000 = 5 min)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Completed job result with image URL</returns>
        Task<MeshyJobResult> GenerateImageAndWaitAsync(
            string prompt,
            string aspectRatio = "1:1",
            int pollIntervalMs = 5000,
            int timeoutMs = 300000,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate mesh and wait for completion (convenience method).
        /// Polls automatically with configurable interval.
        /// </summary>
        /// <param name="imageUrl">URL of the source image</param>
        /// <param name="config">Mesh generation configuration</param>
        /// <param name="pollIntervalMs">Polling interval in milliseconds (default 10000)</param>
        /// <param name="timeoutMs">Timeout in milliseconds (default 600000 = 10 min)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Completed job result with FBX/GLB URLs</returns>
        Task<MeshyJobResult> GenerateMeshAndWaitAsync(
            string imageUrl,
            MeshyMeshConfig config = null,
            int pollIntervalMs = 10000,
            int timeoutMs = 600000,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if the service is configured with a valid API key.
        /// </summary>
        bool IsConfigured { get; }
    }
}
