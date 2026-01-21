namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for accessing environment configuration (.env file).
    /// </summary>
    public interface IEnvConfig
    {
        string MeshyApiKey { get; }
        string ElevenLabsApiKey { get; }
        string GeminiApiKey { get; }
        int McpPort { get; }
        string HooksPath { get; }

        /// <summary>
        /// Check if all required API keys are configured (not placeholders).
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Reload configuration from .env file.
        /// </summary>
        void Reload();
    }
}
