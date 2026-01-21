using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Result from ElevenLabs audio generation.
    /// </summary>
    public class AudioGenerationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Raw audio data (MP3 format).
        /// </summary>
        public byte[] AudioData { get; set; }

        /// <summary>
        /// Path where audio was saved (if downloaded).
        /// </summary>
        public string SavedPath { get; set; }

        /// <summary>
        /// Duration of audio in seconds (if known).
        /// </summary>
        public float? DurationSeconds { get; set; }

        /// <summary>
        /// Content type of the response.
        /// </summary>
        public string ContentType { get; set; }
    }

    /// <summary>
    /// Information about an ElevenLabs voice.
    /// </summary>
    public class VoiceInfo
    {
        public string VoiceId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string PreviewUrl { get; set; }
        public List<string> Labels { get; set; }
    }

    /// <summary>
    /// Settings for text-to-speech generation.
    /// </summary>
    public class VoiceSettings
    {
        /// <summary>
        /// Voice consistency (0.0 - 1.0). Higher = more consistent.
        /// Default: 0.5
        /// </summary>
        public float Stability { get; set; } = 0.5f;

        /// <summary>
        /// How close to original voice (0.0 - 1.0).
        /// Default: 0.75
        /// </summary>
        public float SimilarityBoost { get; set; } = 0.75f;

        /// <summary>
        /// Style exaggeration (0.0 - 1.0). Higher = more expressive.
        /// Default: 0.0
        /// </summary>
        public float Style { get; set; } = 0.0f;

        /// <summary>
        /// Enable speaker boost for clearer audio.
        /// Default: true
        /// </summary>
        public bool UseSpeakerBoost { get; set; } = true;
    }

    /// <summary>
    /// Interface for ElevenLabs audio generation service.
    /// Implements Layer 7 (Generative Asset Pipeline).
    ///
    /// Capabilities:
    /// - Sound effect generation from text descriptions
    /// - Text-to-speech with various voices
    /// - Audio download to Unity project
    /// </summary>
    public interface IElevenLabsService
    {
        /// <summary>
        /// Generate a sound effect from a text description.
        /// </summary>
        /// <param name="description">Description of the sound (e.g., "wooden door creaking open slowly")</param>
        /// <param name="durationSeconds">Duration of sound (0.5 - 22 seconds)</param>
        /// <param name="promptInfluence">How closely to follow the prompt (0.0 - 1.0). Default: 0.3</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Audio generation result with raw audio data</returns>
        Task<AudioGenerationResult> GenerateSoundEffectAsync(
            string description,
            float durationSeconds = 2.0f,
            float promptInfluence = 0.3f,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate speech from text using a specific voice.
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="voiceId">Voice ID (use ListVoicesAsync to get available voices)</param>
        /// <param name="settings">Voice settings (stability, similarity, etc.)</param>
        /// <param name="modelId">Model ID. Default: eleven_monolingual_v1</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Audio generation result with raw audio data</returns>
        Task<AudioGenerationResult> GenerateSpeechAsync(
            string text,
            string voiceId,
            VoiceSettings settings = null,
            string modelId = "eleven_monolingual_v1",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// List all available voices.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of available voices</returns>
        Task<List<VoiceInfo>> ListVoicesAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Download audio data to the Unity project.
        /// </summary>
        /// <param name="audioData">Raw audio data (MP3)</param>
        /// <param name="destinationPath">Path within Assets/ (e.g., "Audio/SFX/door_creak.mp3")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Full path to the saved file</returns>
        Task<string> DownloadAudioAsync(
            byte[] audioData,
            string destinationPath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate sound effect and save to project (convenience method).
        /// Handles rate limiting automatically.
        /// </summary>
        /// <param name="description">Description of the sound</param>
        /// <param name="destinationPath">Path within Assets/</param>
        /// <param name="durationSeconds">Duration of sound</param>
        /// <param name="promptInfluence">How closely to follow the prompt</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Audio generation result with saved path</returns>
        Task<AudioGenerationResult> GenerateSoundEffectAndSaveAsync(
            string description,
            string destinationPath,
            float durationSeconds = 2.0f,
            float promptInfluence = 0.3f,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate speech and save to project (convenience method).
        /// Handles rate limiting automatically.
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="voiceId">Voice ID</param>
        /// <param name="destinationPath">Path within Assets/</param>
        /// <param name="settings">Voice settings</param>
        /// <param name="modelId">Model ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Audio generation result with saved path</returns>
        Task<AudioGenerationResult> GenerateSpeechAndSaveAsync(
            string text,
            string voiceId,
            string destinationPath,
            VoiceSettings settings = null,
            string modelId = "eleven_monolingual_v1",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if the service is configured with a valid API key.
        /// </summary>
        bool IsConfigured { get; }
    }
}
