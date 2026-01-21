using System;
using System.IO;
using System.Threading.Tasks;
using NeuroEngine.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Writes and reads persistent state from hooks/ directory.
    /// </summary>
    public class HooksWriterService : IHooksWriter
    {
        private readonly string _hooksRoot;

        public HooksWriterService(IEnvConfig config)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var hooksPath = config.HooksPath;

            // Handle relative paths
            if (hooksPath.StartsWith("./"))
                hooksPath = hooksPath.Substring(2);

            _hooksRoot = Path.Combine(projectRoot, hooksPath);

            // Ensure base directories exist
            EnsureDirectories();
        }

        private void EnsureDirectories()
        {
            string[] categories = { "scenes", "tasks", "convoys", "messages", "validation", "assets", "snapshots" };
            foreach (var cat in categories)
            {
                Directory.CreateDirectory(Path.Combine(_hooksRoot, cat));
            }
        }

        // Use Newtonsoft.Json for proper Dictionary<string, object> serialization
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        public async Task WriteAsync(string category, string filename, object data)
        {
            var dir = Path.Combine(_hooksRoot, category);
            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, filename);
            var json = JsonConvert.SerializeObject(data, _jsonSettings);

            await File.WriteAllTextAsync(path, json);
        }

        public async Task<T> ReadAsync<T>(string category, string filename)
        {
            var path = Path.Combine(_hooksRoot, category, filename);

            if (!File.Exists(path))
                return default;

            var json = await File.ReadAllTextAsync(path);
            return JsonConvert.DeserializeObject<T>(json, _jsonSettings);
        }

        public bool Exists(string category, string filename)
        {
            var path = Path.Combine(_hooksRoot, category, filename);
            return File.Exists(path);
        }
    }
}
