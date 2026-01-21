using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeuroEngine.Core
{
    #region Data Classes

    /// <summary>
    /// Source of an asset generation.
    /// </summary>
    public enum AssetSource
    {
        Manual,         // Created by human
        Meshy,          // Generated via Meshy.ai (3D models, textures)
        ElevenLabs,     // Generated via ElevenLabs (audio)
        Mixamo,         // Rigging/animation from Mixamo
        Procedural,     // Generated procedurally in code
        Imported,       // Imported from external source
        Unknown
    }

    /// <summary>
    /// Type of asset in the registry.
    /// </summary>
    public enum AssetType
    {
        Model,
        Texture,
        Audio,
        Animation,
        Material,
        Prefab,
        Scene,
        Script,
        Shader,
        VFX,
        UI,
        Other
    }

    /// <summary>
    /// Status of an asset in the pipeline.
    /// </summary>
    public enum AssetStatus
    {
        Pending,        // Queued for generation
        Generating,     // Currently being generated
        Generated,      // Generation complete, not yet imported
        Importing,      // Being imported into Unity
        Imported,       // Successfully imported
        Validated,      // Passed style validation
        Failed,         // Generation or import failed
        Archived        // No longer in use
    }

    /// <summary>
    /// A single generation attempt record.
    /// Tracks the history of how an asset was created/modified.
    /// </summary>
    public class GenerationRecord
    {
        public string Id;
        public DateTime Timestamp;
        public AssetSource Source;
        public string Prompt;           // Text prompt used for generation
        public string JobId;            // External API job ID (meshy, elevenlabs, etc.)
        public bool Success;
        public string ErrorMessage;
        public float DurationSeconds;
        public float CostCredits;       // Estimated API credits used
        public Dictionary<string, object> Parameters;  // Additional generation parameters
        public string OutputPath;       // Where the result was saved
        public string TaskId;           // Link to task that triggered this

        public GenerationRecord()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 12);
            Timestamp = DateTime.UtcNow;
            Parameters = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Texture paths associated with a model asset.
    /// </summary>
    public class TextureSet
    {
        public TextureReference BaseColor;
        public TextureReference Metallic;
        public TextureReference Roughness;
        public TextureReference Normal;
        public TextureReference Height;
        public TextureReference Emission;
        public TextureReference Occlusion;

        public class TextureReference
        {
            public string Path;
            public string Guid;
        }
    }

    /// <summary>
    /// Entry in the asset registry.
    /// Tracks all generated/imported assets with their metadata.
    /// </summary>
    public class AssetRegistryEntry
    {
        public string Id;               // Unique asset ID (asset-XXX)
        public string Name;             // Display name
        public AssetType Type;
        public AssetSource Source;
        public AssetStatus Status;

        // Unity integration
        public string Path;             // Unity asset path
        public string Guid;             // Unity GUID

        // Generation info
        public string Prompt;           // Original generation prompt
        public List<GenerationRecord> GenerationHistory;

        // For models with textures
        public TextureSet Textures;

        // Timestamps
        public DateTime CreatedAt;
        public DateTime ModifiedAt;
        public DateTime? ImportedAt;
        public DateTime? ValidatedAt;

        // Cost tracking
        public float TotalCostCredits;

        // Linking
        public string TaskId;           // Task that generated this
        public string Iteration;        // Game iteration (e.g., "Iteration1")
        public List<string> Tags;       // User-defined tags
        public List<string> DependentAssets;  // Assets that depend on this

        // Style validation
        public float? StyleScore;       // Last validation score
        public List<string> StyleViolations;

        // Custom metadata
        public Dictionary<string, object> Metadata;

        public AssetRegistryEntry()
        {
            Id = GenerateAssetId();
            CreatedAt = DateTime.UtcNow;
            ModifiedAt = DateTime.UtcNow;
            Status = AssetStatus.Pending;
            GenerationHistory = new List<GenerationRecord>();
            Tags = new List<string>();
            DependentAssets = new List<string>();
            StyleViolations = new List<string>();
            Metadata = new Dictionary<string, object>();
        }

        private static int _assetCounter = 0;
        private static readonly object _counterLock = new object();

        private static string GenerateAssetId()
        {
            lock (_counterLock)
            {
                _assetCounter++;
                return $"asset-{_assetCounter:D3}";
            }
        }

        /// <summary>
        /// Add a generation record to history.
        /// </summary>
        public void AddGenerationRecord(GenerationRecord record)
        {
            GenerationHistory.Add(record);
            ModifiedAt = DateTime.UtcNow;
            if (record.Success && !string.IsNullOrEmpty(record.OutputPath))
            {
                Path = record.OutputPath;
            }
            TotalCostCredits += record.CostCredits;
        }
    }

    /// <summary>
    /// Query parameters for finding assets in the registry.
    /// </summary>
    public class AssetQuery
    {
        public AssetType? Type;
        public AssetSource? Source;
        public AssetStatus? Status;
        public string Iteration;
        public string TaskId;
        public DateTime? CreatedAfter;
        public DateTime? CreatedBefore;
        public string NameContains;
        public string PromptContains;
        public List<string> Tags;
        public float? MinStyleScore;
        public int? Limit;
        public int? Offset;
        public string OrderBy;          // "created", "modified", "name", "score"
        public bool Descending;

        public AssetQuery()
        {
            Tags = new List<string>();
            Descending = true;  // Newest first by default
            OrderBy = "created";
        }

        /// <summary>
        /// Create a query for all assets of a specific type.
        /// </summary>
        public static AssetQuery ByType(AssetType type)
        {
            return new AssetQuery { Type = type };
        }

        /// <summary>
        /// Create a query for all assets from a specific source.
        /// </summary>
        public static AssetQuery BySource(AssetSource source)
        {
            return new AssetQuery { Source = source };
        }

        /// <summary>
        /// Create a query for all assets in an iteration.
        /// </summary>
        public static AssetQuery ByIteration(string iteration)
        {
            return new AssetQuery { Iteration = iteration };
        }

        /// <summary>
        /// Create a query for assets linked to a task.
        /// </summary>
        public static AssetQuery ByTask(string taskId)
        {
            return new AssetQuery { TaskId = taskId };
        }
    }

    /// <summary>
    /// Result of a registry operation.
    /// </summary>
    public class RegistryOperationResult
    {
        public bool Success;
        public string AssetId;
        public string Message;
        public AssetRegistryEntry Entry;

        public static RegistryOperationResult Ok(string assetId, AssetRegistryEntry entry = null, string message = null)
        {
            return new RegistryOperationResult
            {
                Success = true,
                AssetId = assetId,
                Entry = entry,
                Message = message ?? "Operation completed successfully"
            };
        }

        public static RegistryOperationResult Error(string message)
        {
            return new RegistryOperationResult
            {
                Success = false,
                Message = message
            };
        }
    }

    #endregion

    /// <summary>
    /// Interface for managing the asset registry.
    /// Part of Layer 7 (Generative Asset Pipeline) - tracks all generated assets
    /// with their generation history, prompts, costs, and validation status.
    ///
    /// Persists to hooks/assets/registry.json for cross-session memory.
    /// </summary>
    public interface IAssetRegistry
    {
        /// <summary>
        /// Register a new asset in the registry.
        /// </summary>
        /// <param name="entry">Asset entry to register</param>
        /// <returns>Assigned asset ID</returns>
        Task<string> RegisterAssetAsync(AssetRegistryEntry entry);

        /// <summary>
        /// Get an asset by its ID.
        /// </summary>
        /// <param name="assetId">Asset ID (e.g., "asset-001")</param>
        /// <returns>Asset entry or null if not found</returns>
        Task<AssetRegistryEntry> GetAssetAsync(string assetId);

        /// <summary>
        /// Get an asset by its Unity path.
        /// </summary>
        /// <param name="unityPath">Unity asset path</param>
        /// <returns>Asset entry or null if not found</returns>
        Task<AssetRegistryEntry> GetAssetByPathAsync(string unityPath);

        /// <summary>
        /// Find assets matching a query.
        /// </summary>
        /// <param name="query">Query parameters</param>
        /// <returns>List of matching assets</returns>
        Task<List<AssetRegistryEntry>> FindAssetsAsync(AssetQuery query);

        /// <summary>
        /// Update an asset's status.
        /// </summary>
        /// <param name="assetId">Asset ID</param>
        /// <param name="status">New status</param>
        Task<RegistryOperationResult> UpdateAssetStatusAsync(string assetId, AssetStatus status);

        /// <summary>
        /// Update asset properties.
        /// </summary>
        /// <param name="assetId">Asset ID</param>
        /// <param name="updates">Dictionary of property updates</param>
        Task<RegistryOperationResult> UpdateAssetAsync(string assetId, Dictionary<string, object> updates);

        /// <summary>
        /// Add a generation record to an asset's history.
        /// </summary>
        /// <param name="assetId">Asset ID</param>
        /// <param name="record">Generation record to add</param>
        Task<RegistryOperationResult> AddGenerationRecordAsync(string assetId, GenerationRecord record);

        /// <summary>
        /// Get the generation history for an asset.
        /// </summary>
        /// <param name="assetPath">Unity asset path</param>
        /// <returns>List of generation records</returns>
        Task<List<GenerationRecord>> GetGenerationHistoryAsync(string assetPath);

        /// <summary>
        /// Update style validation result for an asset.
        /// </summary>
        /// <param name="assetId">Asset ID</param>
        /// <param name="score">Style score (0.0 to 1.0)</param>
        /// <param name="violations">List of violation messages</param>
        Task<RegistryOperationResult> UpdateStyleValidationAsync(string assetId, float score, List<string> violations);

        /// <summary>
        /// Delete an asset from the registry.
        /// </summary>
        /// <param name="assetId">Asset ID</param>
        Task<RegistryOperationResult> DeleteAssetAsync(string assetId);

        /// <summary>
        /// Add a tag to an asset.
        /// </summary>
        Task<RegistryOperationResult> AddTagAsync(string assetId, string tag);

        /// <summary>
        /// Remove a tag from an asset.
        /// </summary>
        Task<RegistryOperationResult> RemoveTagAsync(string assetId, string tag);

        /// <summary>
        /// Get total cost of all generations in a time range.
        /// </summary>
        Task<float> GetTotalCostAsync(DateTime? since = null, DateTime? until = null);

        /// <summary>
        /// Get count of assets by status.
        /// </summary>
        Task<Dictionary<AssetStatus, int>> GetStatusCountsAsync();

        /// <summary>
        /// Get all assets (use with caution for large registries).
        /// </summary>
        Task<List<AssetRegistryEntry>> GetAllAssetsAsync();

        /// <summary>
        /// Force save the registry to disk.
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// Reload the registry from disk.
        /// </summary>
        Task ReloadAsync();
    }
}
