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
    /// Implementation of ElevenLabs audio generation service.
    /// Uses UnityWebRequest for HTTP calls.
    /// Part of Layer 7 (Generative Asset Pipeline).
    /// </summary>
    public class ElevenLabsService : IElevenLabsService
    {
        private const string BaseUrl = "https://api.elevenlabs.io/v1";
        private const int RateLimitDelayMs = 1000; // 1 second between requests

        private readonly IEnvConfig _config;
        private readonly string _generatedAudioPath;

        // Track last request time for rate limiting
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly object _rateLimitLock = new object();

        public ElevenLabsService(IEnvConfig config)
        {
            _config = config;
            _generatedAudioPath = Path.Combine(Application.dataPath, "Audio", "Generated");
        }

        public bool IsConfigured =>
            !string.IsNullOrEmpty(_config.ElevenLabsApiKey) &&
            !_config.ElevenLabsApiKey.Contains("your_");

        public async Task<AudioGenerationResult> GenerateSoundEffectAsync(
            string description,
            float durationSeconds = 2.0f,
            float promptInfluence = 0.3f,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return new AudioGenerationResult
                {
                    Success = false,
                    ErrorMessage = "ElevenLabs API key not configured. Set ELEVENLABS_API_KEY in .env file."
                };
            }

            // Validate parameters
            durationSeconds = Mathf.Clamp(durationSeconds, 0.5f, 22.0f);
            promptInfluence = Mathf.Clamp01(promptInfluence);

            await EnforceRateLimitAsync(cancellationToken);

            var requestBody = new
            {
                text = description,
                duration_seconds = durationSeconds,
                prompt_influence = promptInfluence
            };

            return await PostAudioAsync(
                $"{BaseUrl}/sound-generation",
                requestBody,
                cancellationToken);
        }

        public async Task<AudioGenerationResult> GenerateSpeechAsync(
            string text,
            string voiceId,
            VoiceSettings settings = null,
            string modelId = "eleven_monolingual_v1",
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return new AudioGenerationResult
                {
                    Success = false,
                    ErrorMessage = "ElevenLabs API key not configured. Set ELEVENLABS_API_KEY in .env file."
                };
            }

            if (string.IsNullOrEmpty(voiceId))
            {
                return new AudioGenerationResult
                {
                    Success = false,
                    ErrorMessage = "Voice ID is required. Use ListVoicesAsync to get available voices."
                };
            }

            settings ??= new VoiceSettings();

            await EnforceRateLimitAsync(cancellationToken);

            var requestBody = new
            {
                text = text,
                model_id = modelId,
                voice_settings = new
                {
                    stability = settings.Stability,
                    similarity_boost = settings.SimilarityBoost,
                    style = settings.Style,
                    use_speaker_boost = settings.UseSpeakerBoost
                }
            };

            return await PostAudioAsync(
                $"{BaseUrl}/text-to-speech/{voiceId}",
                requestBody,
                cancellationToken);
        }

        public async Task<List<VoiceInfo>> ListVoicesAsync(
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                Debug.LogError("[ElevenLabsService] API key not configured.");
                return new List<VoiceInfo>();
            }

            await EnforceRateLimitAsync(cancellationToken);

            using var request = UnityWebRequest.Get($"{BaseUrl}/voices");
            request.SetRequestHeader("xi-api-key", _config.ElevenLabsApiKey);

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
                Debug.LogError($"[ElevenLabsService] Failed to list voices: {request.error}");
                return new List<VoiceInfo>();
            }

            var voices = new List<VoiceInfo>();

            try
            {
                var response = JObject.Parse(request.downloadHandler.text);
                var voicesArray = response["voices"] as JArray;

                if (voicesArray != null)
                {
                    foreach (var voice in voicesArray)
                    {
                        voices.Add(new VoiceInfo
                        {
                            VoiceId = voice["voice_id"]?.ToString(),
                            Name = voice["name"]?.ToString(),
                            Category = voice["category"]?.ToString(),
                            Description = voice["description"]?.ToString(),
                            PreviewUrl = voice["preview_url"]?.ToString(),
                            Labels = ParseLabels(voice["labels"])
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ElevenLabsService] Failed to parse voices response: {e.Message}");
            }

            return voices;
        }

        public async Task<string> DownloadAudioAsync(
            byte[] audioData,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            if (audioData == null || audioData.Length == 0)
            {
                throw new ArgumentException("Audio data is empty.");
            }

            // Ensure destination directory exists
            var fullPath = destinationPath.StartsWith("Assets/")
                ? Path.Combine(Application.dataPath, "..", destinationPath)
                : Path.Combine(_generatedAudioPath, destinationPath);

            fullPath = Path.GetFullPath(fullPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write audio data
            await File.WriteAllBytesAsync(fullPath, audioData, cancellationToken);

            Debug.Log($"[ElevenLabsService] Saved audio to: {fullPath}");
            return fullPath;
        }

        public async Task<AudioGenerationResult> GenerateSoundEffectAndSaveAsync(
            string description,
            string destinationPath,
            float durationSeconds = 2.0f,
            float promptInfluence = 0.3f,
            CancellationToken cancellationToken = default)
        {
            var result = await GenerateSoundEffectAsync(
                description,
                durationSeconds,
                promptInfluence,
                cancellationToken);

            if (!result.Success)
                return result;

            try
            {
                result.SavedPath = await DownloadAudioAsync(
                    result.AudioData,
                    destinationPath,
                    cancellationToken);
                result.DurationSeconds = durationSeconds;
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to save audio: {e.Message}";
            }

            return result;
        }

        public async Task<AudioGenerationResult> GenerateSpeechAndSaveAsync(
            string text,
            string voiceId,
            string destinationPath,
            VoiceSettings settings = null,
            string modelId = "eleven_monolingual_v1",
            CancellationToken cancellationToken = default)
        {
            var result = await GenerateSpeechAsync(
                text,
                voiceId,
                settings,
                modelId,
                cancellationToken);

            if (!result.Success)
                return result;

            try
            {
                result.SavedPath = await DownloadAudioAsync(
                    result.AudioData,
                    destinationPath,
                    cancellationToken);
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to save audio: {e.Message}";
            }

            return result;
        }

        #region Private Helpers

        private async Task EnforceRateLimitAsync(CancellationToken cancellationToken)
        {
            TimeSpan delay;

            lock (_rateLimitLock)
            {
                var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
                var requiredDelay = TimeSpan.FromMilliseconds(RateLimitDelayMs);

                if (timeSinceLastRequest < requiredDelay)
                {
                    delay = requiredDelay - timeSinceLastRequest;
                }
                else
                {
                    delay = TimeSpan.Zero;
                }

                _lastRequestTime = DateTime.UtcNow + delay;
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        private async Task<AudioGenerationResult> PostAudioAsync(
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
            request.SetRequestHeader("xi-api-key", _config.ElevenLabsApiKey);

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
                // Check for rate limiting
                if (request.responseCode == 429)
                {
                    return new AudioGenerationResult
                    {
                        Success = false,
                        ErrorMessage = "Rate limit exceeded. Please wait and try again."
                    };
                }

                // Try to parse error response
                string errorMessage = request.error;
                try
                {
                    var errorJson = JObject.Parse(request.downloadHandler.text);
                    errorMessage = errorJson["detail"]?["message"]?.ToString() ??
                                   errorJson["error"]?.ToString() ??
                                   request.error;
                }
                catch
                {
                    // Use default error message
                }

                return new AudioGenerationResult
                {
                    Success = false,
                    ErrorMessage = $"HTTP {request.responseCode}: {errorMessage}"
                };
            }

            return new AudioGenerationResult
            {
                Success = true,
                AudioData = request.downloadHandler.data,
                ContentType = request.GetResponseHeader("Content-Type")
            };
        }

        private static List<string> ParseLabels(JToken labelsToken)
        {
            var labels = new List<string>();

            if (labelsToken is JObject labelsObj)
            {
                foreach (var prop in labelsObj.Properties())
                {
                    labels.Add($"{prop.Name}: {prop.Value}");
                }
            }

            return labels;
        }

        #endregion
    }
}
