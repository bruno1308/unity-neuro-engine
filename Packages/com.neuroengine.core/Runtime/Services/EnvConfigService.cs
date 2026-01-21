using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NeuroEngine.Core;
using UnityEngine;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Reads configuration from .env file.
    /// </summary>
    public class EnvConfigService : IEnvConfig
    {
        private Dictionary<string, string> _vars = new();
        private readonly string _envPath;

        public EnvConfigService()
        {
            _envPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), ".env");
            Reload();
        }

        public string MeshyApiKey => GetValue("MESHY_API_KEY", "");
        public string ElevenLabsApiKey => GetValue("ELEVENLABS_API_KEY", "");
        public string GeminiApiKey => GetValue("GEMINI_API_KEY", "");
        public int McpPort => int.TryParse(GetValue("UNITY_MCP_PORT", "8080"), out var p) ? p : 8080;
        public string HooksPath => GetValue("HOOKS_PATH", "./hooks");

        public bool IsConfigured =>
            !string.IsNullOrEmpty(MeshyApiKey) && !MeshyApiKey.Contains("your_") &&
            !string.IsNullOrEmpty(ElevenLabsApiKey) && !ElevenLabsApiKey.Contains("your_") &&
            !string.IsNullOrEmpty(GeminiApiKey) && !GeminiApiKey.Contains("your_");

        public void Reload()
        {
            _vars.Clear();

            if (!File.Exists(_envPath))
            {
                Debug.LogWarning($"[NeuroEngine] .env file not found at {_envPath}");
                return;
            }

            foreach (var line in File.ReadAllLines(_envPath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                    continue;

                var match = Regex.Match(line, @"^([^=]+)=(.*)$");
                if (match.Success)
                {
                    var key = match.Groups[1].Value.Trim();
                    var value = match.Groups[2].Value.Trim();
                    _vars[key] = value;
                }
            }
        }

        private string GetValue(string key, string defaultValue)
        {
            return _vars.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }
}
