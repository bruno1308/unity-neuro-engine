#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// MCP tool to generate audio using ElevenLabs.
    /// Part of Layer 7 (Generative Asset Pipeline).
    ///
    /// Capabilities:
    /// - Sound effect generation from text descriptions
    /// - Text-to-speech with various voices
    /// - Listing available voices
    /// </summary>
    [McpForUnityTool("generate_audio", Description = "Generate audio using ElevenLabs. Actions: sound_effect, speech, list_voices. For sound effects, provide description. For speech, provide text and voice_id.")]
    public static class GenerateAudio
    {
        // Common voice IDs for quick reference
        private static readonly Dictionary<string, string> CommonVoices = new()
        {
            { "rachel", "21m00Tcm4TlvDq8ikWAM" },     // Female, calm
            { "domi", "AZnzlk1XvdvUeBnXmlld" },       // Female, confident
            { "bella", "EXAVITQu4vr4xnSDxMaL" },      // Female, soft
            { "antoni", "ErXwobaYiN019PkySvjV" },     // Male, calm
            { "elli", "MF3mGyEYCl7XYWbV9V6O" }        // Female, young
        };

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();

            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse(
                    "Required parameter 'action' is missing. " +
                    "Use: sound_effect, speech, list_voices");
            }

            var elevenLabsService = GetElevenLabsService();
            if (elevenLabsService == null)
            {
                return new ErrorResponse("Failed to initialize ElevenLabsService. Check configuration.");
            }

            if (!elevenLabsService.IsConfigured && action != "list_voices")
            {
                return new ErrorResponse(
                    "ElevenLabs API key not configured. Set ELEVENLABS_API_KEY in .env file.");
            }

            try
            {
                return action switch
                {
                    "sound_effect" => HandleSoundEffect(@params, elevenLabsService),
                    "speech" => HandleSpeech(@params, elevenLabsService),
                    "list_voices" => HandleListVoices(@params, elevenLabsService),
                    _ => new ErrorResponse($"Unknown action '{action}'. Use: sound_effect, speech, list_voices")
                };
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error in ElevenLabs operation: {e.Message}");
            }
        }

        private static IElevenLabsService GetElevenLabsService()
        {
            // Try EditorServiceLocator first
            try
            {
                return EditorServiceLocator.Get<IElevenLabsService>();
            }
            catch
            {
                // Fall back to creating manually
                var config = new EnvConfigService();
                return new ElevenLabsService(config);
            }
        }

        private static object HandleSoundEffect(JObject @params, IElevenLabsService service)
        {
            string description = @params["description"]?.ToString();
            if (string.IsNullOrEmpty(description))
            {
                return new ErrorResponse("Required parameter 'description' is missing.");
            }

            float durationSeconds = @params["duration_seconds"]?.Value<float>() ?? 2.0f;
            float promptInfluence = @params["prompt_influence"]?.Value<float>() ?? 0.3f;
            string destinationPath = @params["destination_path"]?.ToString();
            string category = @params["category"]?.ToString() ?? "SFX";

            // Generate destination path if not provided
            if (string.IsNullOrEmpty(destinationPath))
            {
                var filename = SanitizeFilename(description);
                if (filename.Length > 50)
                    filename = filename.Substring(0, 50);
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                destinationPath = $"Assets/Audio/{category}/{filename}_{timestamp}.mp3";
            }

            // Ensure it starts with Assets/
            if (!destinationPath.StartsWith("Assets/"))
            {
                destinationPath = $"Assets/Audio/{category}/{destinationPath}";
            }

            // Ensure .mp3 extension
            if (!destinationPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                destinationPath += ".mp3";
            }

            Debug.Log($"[GenerateAudio] Generating sound effect: {description}");

            var task = Task.Run(() => service.GenerateSoundEffectAndSaveAsync(
                description,
                destinationPath,
                durationSeconds,
                promptInfluence));
            var result = task.GetAwaiter().GetResult();

            if (!result.Success)
            {
                return new ErrorResponse($"Sound effect generation failed: {result.ErrorMessage}");
            }

            // Refresh asset database
            AssetDatabase.Refresh();

            return new SuccessResponse($"Sound effect generated", new
            {
                description = description,
                duration_seconds = durationSeconds,
                saved_to = result.SavedPath,
                asset_path = destinationPath,
                bytes = result.AudioData?.Length ?? 0,
                hint = "Audio imported. Use AudioSource component to play."
            });
        }

        private static object HandleSpeech(JObject @params, IElevenLabsService service)
        {
            string text = @params["text"]?.ToString();
            if (string.IsNullOrEmpty(text))
            {
                return new ErrorResponse("Required parameter 'text' is missing.");
            }

            string voiceId = @params["voice_id"]?.ToString();
            string voiceName = @params["voice_name"]?.ToString();

            // Try to resolve voice by name if ID not provided
            if (string.IsNullOrEmpty(voiceId))
            {
                if (!string.IsNullOrEmpty(voiceName) &&
                    CommonVoices.TryGetValue(voiceName.ToLowerInvariant(), out var commonVoiceId))
                {
                    voiceId = commonVoiceId;
                }
                else
                {
                    return new ErrorResponse(
                        "Required parameter 'voice_id' or valid 'voice_name' is missing. " +
                        $"Common voice names: {string.Join(", ", CommonVoices.Keys)}");
                }
            }

            string destinationPath = @params["destination_path"]?.ToString();
            string character = @params["character"]?.ToString() ?? "Default";
            string modelId = @params["model_id"]?.ToString() ?? "eleven_monolingual_v1";

            // Parse voice settings
            var settings = new VoiceSettings
            {
                Stability = @params["stability"]?.Value<float>() ?? 0.5f,
                SimilarityBoost = @params["similarity_boost"]?.Value<float>() ?? 0.75f,
                Style = @params["style"]?.Value<float>() ?? 0.0f,
                UseSpeakerBoost = @params["use_speaker_boost"]?.Value<bool>() ?? true
            };

            // Generate destination path if not provided
            if (string.IsNullOrEmpty(destinationPath))
            {
                var filename = SanitizeFilename(text);
                if (filename.Length > 50)
                    filename = filename.Substring(0, 50);
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                destinationPath = $"Assets/Audio/Voice/{character}/{filename}_{timestamp}.mp3";
            }

            // Ensure it starts with Assets/
            if (!destinationPath.StartsWith("Assets/"))
            {
                destinationPath = $"Assets/Audio/Voice/{character}/{destinationPath}";
            }

            // Ensure .mp3 extension
            if (!destinationPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                destinationPath += ".mp3";
            }

            Debug.Log($"[GenerateAudio] Generating speech: {text.Substring(0, Math.Min(50, text.Length))}...");

            var task = Task.Run(() => service.GenerateSpeechAndSaveAsync(
                text,
                voiceId,
                destinationPath,
                settings,
                modelId));
            var result = task.GetAwaiter().GetResult();

            if (!result.Success)
            {
                return new ErrorResponse($"Speech generation failed: {result.ErrorMessage}");
            }

            // Refresh asset database
            AssetDatabase.Refresh();

            return new SuccessResponse($"Speech generated", new
            {
                text = text,
                voice_id = voiceId,
                saved_to = result.SavedPath,
                asset_path = destinationPath,
                bytes = result.AudioData?.Length ?? 0,
                settings = new
                {
                    stability = settings.Stability,
                    similarity_boost = settings.SimilarityBoost,
                    style = settings.Style
                },
                hint = "Audio imported. Use AudioSource component to play."
            });
        }

        private static object HandleListVoices(JObject @params, IElevenLabsService service)
        {
            if (!service.IsConfigured)
            {
                // Return common voices as fallback
                return new SuccessResponse("Common voices (API key not configured)", new
                {
                    configured = false,
                    common_voices = CommonVoices.Select(kvp => new
                    {
                        name = kvp.Key,
                        voice_id = kvp.Value
                    }).ToList(),
                    hint = "Set ELEVENLABS_API_KEY in .env to list all available voices"
                });
            }

            var task = Task.Run(() => service.ListVoicesAsync());
            var voices = task.GetAwaiter().GetResult();

            return new SuccessResponse($"Found {voices.Count} voices", new
            {
                configured = true,
                total_count = voices.Count,
                voices = voices.Select(v => new
                {
                    voice_id = v.VoiceId,
                    name = v.Name,
                    category = v.Category,
                    description = v.Description,
                    preview_url = v.PreviewUrl,
                    labels = v.Labels
                }).ToList(),
                common_voices = CommonVoices.Select(kvp => new
                {
                    name = kvp.Key,
                    voice_id = kvp.Value
                }).ToList()
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
