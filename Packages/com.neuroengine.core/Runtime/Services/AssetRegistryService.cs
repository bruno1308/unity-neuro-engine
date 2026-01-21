using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NeuroEngine.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Service for managing the asset registry.
    /// Persists to hooks/assets/registry.json for cross-session memory.
    /// Part of Layer 7 (Generative Asset Pipeline).
    ///
    /// This service tracks all generated assets with their:
    /// - Generation history (prompts, job IDs, costs)
    /// - Import status
    /// - Style validation results
    /// - Links to tasks and iterations
    /// </summary>
    public class AssetRegistryService : IAssetRegistry
    {
        private readonly string _registryPath;
        private readonly object _lock = new object();
        private RegistryData _data;
        private bool _isDirty;

        // JSON serialization settings
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new StringEnumConverter()
            },
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };

        /// <summary>
        /// Internal registry data structure matching hooks/assets/registry.json format.
        /// </summary>
        private class RegistryData
        {
            public List<AssetRegistryEntry> Assets = new List<AssetRegistryEntry>();
        }

        public AssetRegistryService()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            _registryPath = Path.Combine(projectRoot, "hooks", "assets", "registry.json");

            // Ensure directory exists
            var dir = Path.GetDirectoryName(_registryPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Load existing registry
            LoadRegistry();
        }

        public AssetRegistryService(string registryPath)
        {
            _registryPath = registryPath;

            var dir = Path.GetDirectoryName(_registryPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            LoadRegistry();
        }

        private void LoadRegistry()
        {
            lock (_lock)
            {
                if (File.Exists(_registryPath))
                {
                    try
                    {
                        var json = File.ReadAllText(_registryPath);
                        _data = JsonConvert.DeserializeObject<RegistryData>(json, _jsonSettings);

                        // Handle legacy format where root is just { "assets": [...] }
                        if (_data == null)
                        {
                            // Try parsing as legacy format
                            var legacy = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                            if (legacy != null && legacy.ContainsKey("assets"))
                            {
                                var assetsJson = JsonConvert.SerializeObject(legacy["assets"]);
                                var assets = JsonConvert.DeserializeObject<List<AssetRegistryEntry>>(assetsJson, _jsonSettings);
                                _data = new RegistryData { Assets = assets ?? new List<AssetRegistryEntry>() };
                            }
                        }

                        _data ??= new RegistryData();

                        // Initialize asset counter from existing assets
                        InitializeAssetCounter();

                        Debug.Log($"[AssetRegistry] Loaded {_data.Assets.Count} assets from registry");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[AssetRegistry] Failed to load registry: {e.Message}");
                        _data = new RegistryData();
                    }
                }
                else
                {
                    _data = new RegistryData();
                    Debug.Log("[AssetRegistry] Created new empty registry");
                }
            }
        }

        private void InitializeAssetCounter()
        {
            // Find the highest existing asset number to avoid ID collisions
            int maxNumber = 0;
            foreach (var asset in _data.Assets)
            {
                if (asset.Id != null && asset.Id.StartsWith("asset-"))
                {
                    var numStr = asset.Id.Substring(6);
                    if (int.TryParse(numStr, out var num))
                    {
                        maxNumber = Math.Max(maxNumber, num);
                    }
                }
            }

            // Set the counter field via reflection (it's private in AssetRegistryEntry)
            // We'll use a simple approach: just track it here
            _nextAssetNumber = maxNumber + 1;
        }

        private int _nextAssetNumber = 1;

        private string GenerateNextAssetId()
        {
            lock (_lock)
            {
                return $"asset-{_nextAssetNumber++:D3}";
            }
        }

        private async Task SaveRegistryAsync()
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    try
                    {
                        var json = JsonConvert.SerializeObject(_data, _jsonSettings);
                        var tempPath = _registryPath + ".tmp";

                        File.WriteAllText(tempPath, json);

                        if (File.Exists(_registryPath))
                        {
                            File.Delete(_registryPath);
                        }
                        File.Move(tempPath, _registryPath);

                        _isDirty = false;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[AssetRegistry] Failed to save registry: {e.Message}");
                    }
                }
            });
        }

        public async Task<string> RegisterAssetAsync(AssetRegistryEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            lock (_lock)
            {
                // Assign a new ID if not set or using default
                if (string.IsNullOrEmpty(entry.Id) || entry.Id.StartsWith("asset-") && _data.Assets.Any(a => a.Id == entry.Id))
                {
                    entry.Id = GenerateNextAssetId();
                }

                entry.CreatedAt = DateTime.UtcNow;
                entry.ModifiedAt = DateTime.UtcNow;

                _data.Assets.Add(entry);
                _isDirty = true;
            }

            await SaveRegistryAsync();
            Debug.Log($"[AssetRegistry] Registered new asset: {entry.Id} ({entry.Name})");

            return entry.Id;
        }

        public Task<AssetRegistryEntry> GetAssetAsync(string assetId)
        {
            lock (_lock)
            {
                var asset = _data.Assets.FirstOrDefault(a => a.Id == assetId);
                return Task.FromResult(asset);
            }
        }

        public Task<AssetRegistryEntry> GetAssetByPathAsync(string unityPath)
        {
            lock (_lock)
            {
                var asset = _data.Assets.FirstOrDefault(a =>
                    string.Equals(a.Path, unityPath, StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(asset);
            }
        }

        public Task<List<AssetRegistryEntry>> FindAssetsAsync(AssetQuery query)
        {
            lock (_lock)
            {
                IEnumerable<AssetRegistryEntry> results = _data.Assets;

                // Apply filters
                if (query.Type.HasValue)
                    results = results.Where(a => a.Type == query.Type.Value);

                if (query.Source.HasValue)
                    results = results.Where(a => a.Source == query.Source.Value);

                if (query.Status.HasValue)
                    results = results.Where(a => a.Status == query.Status.Value);

                if (!string.IsNullOrEmpty(query.Iteration))
                    results = results.Where(a => a.Iteration == query.Iteration);

                if (!string.IsNullOrEmpty(query.TaskId))
                    results = results.Where(a => a.TaskId == query.TaskId);

                if (query.CreatedAfter.HasValue)
                    results = results.Where(a => a.CreatedAt >= query.CreatedAfter.Value);

                if (query.CreatedBefore.HasValue)
                    results = results.Where(a => a.CreatedAt <= query.CreatedBefore.Value);

                if (!string.IsNullOrEmpty(query.NameContains))
                    results = results.Where(a => a.Name != null &&
                        a.Name.Contains(query.NameContains, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(query.PromptContains))
                    results = results.Where(a => a.Prompt != null &&
                        a.Prompt.Contains(query.PromptContains, StringComparison.OrdinalIgnoreCase));

                if (query.Tags != null && query.Tags.Count > 0)
                    results = results.Where(a => a.Tags != null && query.Tags.All(t => a.Tags.Contains(t)));

                if (query.MinStyleScore.HasValue)
                    results = results.Where(a => a.StyleScore.HasValue && a.StyleScore.Value >= query.MinStyleScore.Value);

                // Apply ordering
                results = query.OrderBy?.ToLowerInvariant() switch
                {
                    "modified" => query.Descending
                        ? results.OrderByDescending(a => a.ModifiedAt)
                        : results.OrderBy(a => a.ModifiedAt),
                    "name" => query.Descending
                        ? results.OrderByDescending(a => a.Name)
                        : results.OrderBy(a => a.Name),
                    "score" => query.Descending
                        ? results.OrderByDescending(a => a.StyleScore ?? 0)
                        : results.OrderBy(a => a.StyleScore ?? 0),
                    _ => query.Descending
                        ? results.OrderByDescending(a => a.CreatedAt)
                        : results.OrderBy(a => a.CreatedAt)
                };

                // Apply pagination
                if (query.Offset.HasValue)
                    results = results.Skip(query.Offset.Value);

                if (query.Limit.HasValue)
                    results = results.Take(query.Limit.Value);

                return Task.FromResult(results.ToList());
            }
        }

        public async Task<RegistryOperationResult> UpdateAssetStatusAsync(string assetId, AssetStatus status)
        {
            lock (_lock)
            {
                var asset = _data.Assets.FirstOrDefault(a => a.Id == assetId);
                if (asset == null)
                    return RegistryOperationResult.Error($"Asset not found: {assetId}");

                asset.Status = status;
                asset.ModifiedAt = DateTime.UtcNow;

                if (status == AssetStatus.Imported)
                    asset.ImportedAt = DateTime.UtcNow;
                else if (status == AssetStatus.Validated)
                    asset.ValidatedAt = DateTime.UtcNow;

                _isDirty = true;
            }

            await SaveRegistryAsync();
            return RegistryOperationResult.Ok(assetId, message: $"Status updated to {status}");
        }

        public async Task<RegistryOperationResult> UpdateAssetAsync(string assetId, Dictionary<string, object> updates)
        {
            lock (_lock)
            {
                var asset = _data.Assets.FirstOrDefault(a => a.Id == assetId);
                if (asset == null)
                    return RegistryOperationResult.Error($"Asset not found: {assetId}");

                foreach (var update in updates)
                {
                    var key = update.Key.ToLowerInvariant();
                    var value = update.Value;

                    switch (key)
                    {
                        case "name": asset.Name = value?.ToString(); break;
                        case "path": asset.Path = value?.ToString(); break;
                        case "guid": asset.Guid = value?.ToString(); break;
                        case "prompt": asset.Prompt = value?.ToString(); break;
                        case "taskid": asset.TaskId = value?.ToString(); break;
                        case "iteration": asset.Iteration = value?.ToString(); break;
                        case "status":
                            if (Enum.TryParse<AssetStatus>(value?.ToString(), true, out var status))
                                asset.Status = status;
                            break;
                        case "type":
                            if (Enum.TryParse<AssetType>(value?.ToString(), true, out var type))
                                asset.Type = type;
                            break;
                        case "source":
                            if (Enum.TryParse<AssetSource>(value?.ToString(), true, out var source))
                                asset.Source = source;
                            break;
                        default:
                            // Store in metadata
                            asset.Metadata[key] = value;
                            break;
                    }
                }

                asset.ModifiedAt = DateTime.UtcNow;
                _isDirty = true;
            }

            await SaveRegistryAsync();
            return RegistryOperationResult.Ok(assetId, message: "Asset updated");
        }

        public async Task<RegistryOperationResult> AddGenerationRecordAsync(string assetId, GenerationRecord record)
        {
            lock (_lock)
            {
                var asset = _data.Assets.FirstOrDefault(a => a.Id == assetId);
                if (asset == null)
                    return RegistryOperationResult.Error($"Asset not found: {assetId}");

                asset.AddGenerationRecord(record);
                _isDirty = true;
            }

            await SaveRegistryAsync();
            return RegistryOperationResult.Ok(assetId, message: "Generation record added");
        }

        public Task<List<GenerationRecord>> GetGenerationHistoryAsync(string assetPath)
        {
            lock (_lock)
            {
                var asset = _data.Assets.FirstOrDefault(a =>
                    string.Equals(a.Path, assetPath, StringComparison.OrdinalIgnoreCase));

                if (asset == null)
                    return Task.FromResult(new List<GenerationRecord>());

                return Task.FromResult(asset.GenerationHistory ?? new List<GenerationRecord>());
            }
        }

        public async Task<RegistryOperationResult> UpdateStyleValidationAsync(string assetId, float score, List<string> violations)
        {
            lock (_lock)
            {
                var asset = _data.Assets.FirstOrDefault(a => a.Id == assetId);
                if (asset == null)
                    return RegistryOperationResult.Error($"Asset not found: {assetId}");

                asset.StyleScore = score;
                asset.StyleViolations = violations ?? new List<string>();
                asset.ModifiedAt = DateTime.UtcNow;

                if (score >= 0.8f && asset.Status == AssetStatus.Imported)
                {
                    asset.Status = AssetStatus.Validated;
                    asset.ValidatedAt = DateTime.UtcNow;
                }

                _isDirty = true;
            }

            await SaveRegistryAsync();
            return RegistryOperationResult.Ok(assetId, message: $"Style validation updated: score={score:F2}");
        }

        public async Task<RegistryOperationResult> DeleteAssetAsync(string assetId)
        {
            lock (_lock)
            {
                var asset = _data.Assets.FirstOrDefault(a => a.Id == assetId);
                if (asset == null)
                    return RegistryOperationResult.Error($"Asset not found: {assetId}");

                _data.Assets.Remove(asset);
                _isDirty = true;
            }

            await SaveRegistryAsync();
            return RegistryOperationResult.Ok(assetId, message: "Asset deleted from registry");
        }

        public async Task<RegistryOperationResult> AddTagAsync(string assetId, string tag)
        {
            lock (_lock)
            {
                var asset = _data.Assets.FirstOrDefault(a => a.Id == assetId);
                if (asset == null)
                    return RegistryOperationResult.Error($"Asset not found: {assetId}");

                if (!asset.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    asset.Tags.Add(tag);
                    asset.ModifiedAt = DateTime.UtcNow;
                    _isDirty = true;
                }
            }

            await SaveRegistryAsync();
            return RegistryOperationResult.Ok(assetId, message: $"Tag '{tag}' added");
        }

        public async Task<RegistryOperationResult> RemoveTagAsync(string assetId, string tag)
        {
            lock (_lock)
            {
                var asset = _data.Assets.FirstOrDefault(a => a.Id == assetId);
                if (asset == null)
                    return RegistryOperationResult.Error($"Asset not found: {assetId}");

                var existing = asset.Tags.FirstOrDefault(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    asset.Tags.Remove(existing);
                    asset.ModifiedAt = DateTime.UtcNow;
                    _isDirty = true;
                }
            }

            await SaveRegistryAsync();
            return RegistryOperationResult.Ok(assetId, message: $"Tag '{tag}' removed");
        }

        public Task<float> GetTotalCostAsync(DateTime? since = null, DateTime? until = null)
        {
            lock (_lock)
            {
                var assets = _data.Assets.AsEnumerable();

                if (since.HasValue)
                    assets = assets.Where(a => a.CreatedAt >= since.Value);

                if (until.HasValue)
                    assets = assets.Where(a => a.CreatedAt <= until.Value);

                var totalCost = assets.Sum(a => a.TotalCostCredits);
                return Task.FromResult(totalCost);
            }
        }

        public Task<Dictionary<AssetStatus, int>> GetStatusCountsAsync()
        {
            lock (_lock)
            {
                var counts = _data.Assets
                    .GroupBy(a => a.Status)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Ensure all statuses are represented
                foreach (AssetStatus status in Enum.GetValues(typeof(AssetStatus)))
                {
                    if (!counts.ContainsKey(status))
                        counts[status] = 0;
                }

                return Task.FromResult(counts);
            }
        }

        public Task<List<AssetRegistryEntry>> GetAllAssetsAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_data.Assets.ToList());
            }
        }

        public async Task SaveAsync()
        {
            await SaveRegistryAsync();
        }

        public Task ReloadAsync()
        {
            LoadRegistry();
            return Task.CompletedTask;
        }
    }
}
