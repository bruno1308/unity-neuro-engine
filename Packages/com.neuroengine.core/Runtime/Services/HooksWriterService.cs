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
        private readonly object _fileLock = new object();

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

            await WriteFileAtomicAsync(path, json);
        }

        /// <summary>
        /// Write file atomically using temp file + rename pattern.
        /// Uses unique temp filename to avoid race conditions between concurrent writes.
        /// Prevents corruption if process is interrupted during write.
        /// </summary>
        private async Task WriteFileAtomicAsync(string path, string content)
        {
            // Use unique temp filename to avoid race conditions
            var tempPath = path + $".{Guid.NewGuid():N}.tmp";
            try
            {
                // Write to temp file first
                await File.WriteAllTextAsync(tempPath, content);

                // Atomic rename (overwrite if exists)
                lock (_fileLock)
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    File.Move(tempPath, path);
                }
            }
            catch
            {
                // Clean up temp file on failure
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
                throw;
            }
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
