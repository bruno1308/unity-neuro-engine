#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool for managing the asset registry.
    /// Part of Layer 7 (Generative Asset Pipeline).
    ///
    /// Usage:
    /// - manage_asset_registry action=register name="Player" type="model" source="meshy" path="Assets/Models/Player.fbx"
    /// - manage_asset_registry action=get asset_id="asset-001"
    /// - manage_asset_registry action=get_by_path path="Assets/Models/Player.fbx"
    /// - manage_asset_registry action=find type="model" source="meshy" iteration="Iteration1"
    /// - manage_asset_registry action=update_status asset_id="asset-001" status="imported"
    /// - manage_asset_registry action=add_generation asset_id="asset-001" prompt="..." job_id="..." success=true
    /// - manage_asset_registry action=get_history path="Assets/Models/Player.fbx"
    /// - manage_asset_registry action=list
    /// - manage_asset_registry action=stats
    /// - manage_asset_registry action=add_tag asset_id="asset-001" tag="hero"
    /// - manage_asset_registry action=delete asset_id="asset-001"
    /// </summary>
    [McpForUnityTool("manage_asset_registry", Description = "Manages the asset registry - register, query, and update generated assets with their history, prompts, and validation status.")]
    public static class ManageAssetRegistry
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString() ?? "list";

            try
            {
                var registry = EditorServiceLocator.Get<IAssetRegistry>();

                // Execute async method synchronously for MCP compatibility
                return action.ToLowerInvariant() switch
                {
                    "register" => RunAsync(() => RegisterAsset(@params, registry)),
                    "get" => RunAsync(() => GetAsset(@params, registry)),
                    "get_by_path" or "getbypath" => RunAsync(() => GetAssetByPath(@params, registry)),
                    "find" or "query" or "search" => RunAsync(() => FindAssets(@params, registry)),
                    "update_status" or "updatestatus" => RunAsync(() => UpdateStatus(@params, registry)),
                    "update" => RunAsync(() => UpdateAsset(@params, registry)),
                    "add_generation" or "addgeneration" => RunAsync(() => AddGeneration(@params, registry)),
                    "get_history" or "gethistory" => RunAsync(() => GetHistory(@params, registry)),
                    "validate_style" or "validatestyle" => RunAsync(() => ValidateStyle(@params, registry)),
                    "list" or "all" => RunAsync(() => ListAssets(registry)),
                    "stats" or "statistics" => RunAsync(() => GetStats(registry)),
                    "add_tag" or "addtag" => RunAsync(() => AddTag(@params, registry)),
                    "remove_tag" or "removetag" => RunAsync(() => RemoveTag(@params, registry)),
                    "delete" => RunAsync(() => DeleteAsset(@params, registry)),
                    "reload" => RunAsync(() => ReloadRegistry(registry)),
                    _ => new ErrorResponse($"Unknown action: {action}. Valid actions: register, get, get_by_path, find, update_status, update, add_generation, get_history, validate_style, list, stats, add_tag, remove_tag, delete, reload")
                };
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error in manage_asset_registry: {e.Message}");
            }
        }

        private static object RunAsync(Func<Task<object>> asyncFunc)
        {
            return asyncFunc().GetAwaiter().GetResult();
        }

        private static async Task<object> RegisterAsset(JObject @params, IAssetRegistry registry)
        {
            var entry = new AssetRegistryEntry
            {
                Name = @params["name"]?.ToString(),
                Path = @params["path"]?.ToString(),
                Guid = @params["guid"]?.ToString(),
                Prompt = @params["prompt"]?.ToString(),
                TaskId = @params["task_id"]?.ToString(),
                Iteration = @params["iteration"]?.ToString()
            };

            // Parse type
            if (Enum.TryParse<AssetType>(@params["type"]?.ToString(), true, out var assetType))
                entry.Type = assetType;

            // Parse source
            if (Enum.TryParse<AssetSource>(@params["source"]?.ToString(), true, out var source))
                entry.Source = source;

            // Parse status
            if (Enum.TryParse<AssetStatus>(@params["status"]?.ToString(), true, out var status))
                entry.Status = status;

            // Parse tags
            var tagsToken = @params["tags"];
            if (tagsToken != null)
            {
                if (tagsToken.Type == JTokenType.Array)
                    entry.Tags = tagsToken.ToObject<List<string>>();
                else if (tagsToken.Type == JTokenType.String)
                    entry.Tags = tagsToken.ToString().Split(',').Select(t => t.Trim()).ToList();
            }

            // Parse cost
            if (float.TryParse(@params["cost"]?.ToString(), out var cost))
                entry.TotalCostCredits = cost;

            var assetId = await registry.RegisterAssetAsync(entry);

            return new SuccessResponse($"Registered asset: {assetId}", new
            {
                asset_id = assetId,
                name = entry.Name,
                type = entry.Type.ToString(),
                source = entry.Source.ToString(),
                status = entry.Status.ToString(),
                path = entry.Path
            });
        }

        private static async Task<object> GetAsset(JObject @params, IAssetRegistry registry)
        {
            string assetId = @params["asset_id"]?.ToString() ?? @params["id"]?.ToString();
            if (string.IsNullOrEmpty(assetId))
                return new ErrorResponse("Missing required parameter: asset_id");

            var asset = await registry.GetAssetAsync(assetId);
            if (asset == null)
                return new ErrorResponse($"Asset not found: {assetId}");

            return new SuccessResponse($"Asset: {asset.Name} ({assetId})", FormatAssetEntry(asset));
        }

        private static async Task<object> GetAssetByPath(JObject @params, IAssetRegistry registry)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("Missing required parameter: path");

            var asset = await registry.GetAssetByPathAsync(path);
            if (asset == null)
                return new ErrorResponse($"Asset not found at path: {path}");

            return new SuccessResponse($"Asset: {asset.Name} ({asset.Id})", FormatAssetEntry(asset));
        }

        private static async Task<object> FindAssets(JObject @params, IAssetRegistry registry)
        {
            var query = new AssetQuery();

            // Parse query parameters
            if (Enum.TryParse<AssetType>(@params["type"]?.ToString(), true, out var assetType))
                query.Type = assetType;

            if (Enum.TryParse<AssetSource>(@params["source"]?.ToString(), true, out var source))
                query.Source = source;

            if (Enum.TryParse<AssetStatus>(@params["status"]?.ToString(), true, out var status))
                query.Status = status;

            query.Iteration = @params["iteration"]?.ToString();
            query.TaskId = @params["task_id"]?.ToString();
            query.NameContains = @params["name_contains"]?.ToString() ?? @params["name"]?.ToString();
            query.PromptContains = @params["prompt_contains"]?.ToString() ?? @params["prompt"]?.ToString();

            if (int.TryParse(@params["limit"]?.ToString(), out var limit))
                query.Limit = limit;

            if (int.TryParse(@params["offset"]?.ToString(), out var offset))
                query.Offset = offset;

            query.OrderBy = @params["order_by"]?.ToString() ?? "created";
            query.Descending = @params["descending"]?.ToString()?.ToLower() != "false";

            var assets = await registry.FindAssetsAsync(query);

            return new SuccessResponse($"Found {assets.Count} assets", new
            {
                count = assets.Count,
                query = new
                {
                    type = query.Type?.ToString(),
                    source = query.Source?.ToString(),
                    status = query.Status?.ToString(),
                    iteration = query.Iteration,
                    limit = query.Limit,
                    offset = query.Offset
                },
                assets = assets.Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    type = a.Type.ToString(),
                    source = a.Source.ToString(),
                    status = a.Status.ToString(),
                    path = a.Path,
                    iteration = a.Iteration,
                    style_score = a.StyleScore,
                    created_at = a.CreatedAt.ToString("o")
                }).ToList()
            });
        }

        private static async Task<object> UpdateStatus(JObject @params, IAssetRegistry registry)
        {
            string assetId = @params["asset_id"]?.ToString() ?? @params["id"]?.ToString();
            if (string.IsNullOrEmpty(assetId))
                return new ErrorResponse("Missing required parameter: asset_id");

            string statusStr = @params["status"]?.ToString();
            if (!Enum.TryParse<AssetStatus>(statusStr, true, out var status))
                return new ErrorResponse($"Invalid status: {statusStr}. Valid values: {string.Join(", ", Enum.GetNames(typeof(AssetStatus)))}");

            var result = await registry.UpdateAssetStatusAsync(assetId, status);

            if (!result.Success)
                return new ErrorResponse(result.Message);

            return new SuccessResponse(result.Message, new
            {
                asset_id = assetId,
                new_status = status.ToString()
            });
        }

        private static async Task<object> UpdateAsset(JObject @params, IAssetRegistry registry)
        {
            string assetId = @params["asset_id"]?.ToString() ?? @params["id"]?.ToString();
            if (string.IsNullOrEmpty(assetId))
                return new ErrorResponse("Missing required parameter: asset_id");

            var updates = new Dictionary<string, object>();

            // Extract update properties
            foreach (var prop in @params.Properties())
            {
                if (prop.Name == "action" || prop.Name == "asset_id" || prop.Name == "id")
                    continue;

                updates[prop.Name] = prop.Value.Type switch
                {
                    JTokenType.String => prop.Value.ToString(),
                    JTokenType.Integer => prop.Value.ToObject<long>(),
                    JTokenType.Float => prop.Value.ToObject<double>(),
                    JTokenType.Boolean => prop.Value.ToObject<bool>(),
                    _ => prop.Value.ToString()
                };
            }

            if (updates.Count == 0)
                return new ErrorResponse("No properties to update");

            var result = await registry.UpdateAssetAsync(assetId, updates);

            if (!result.Success)
                return new ErrorResponse(result.Message);

            return new SuccessResponse($"Updated {updates.Count} properties on {assetId}", new
            {
                asset_id = assetId,
                updated_properties = updates.Keys.ToList()
            });
        }

        private static async Task<object> AddGeneration(JObject @params, IAssetRegistry registry)
        {
            string assetId = @params["asset_id"]?.ToString() ?? @params["id"]?.ToString();
            if (string.IsNullOrEmpty(assetId))
                return new ErrorResponse("Missing required parameter: asset_id");

            var record = new GenerationRecord
            {
                Prompt = @params["prompt"]?.ToString(),
                JobId = @params["job_id"]?.ToString(),
                TaskId = @params["task_id"]?.ToString(),
                OutputPath = @params["output_path"]?.ToString()
            };

            // Parse source
            if (Enum.TryParse<AssetSource>(@params["source"]?.ToString(), true, out var source))
                record.Source = source;

            // Parse success
            record.Success = @params["success"]?.ToString()?.ToLower() != "false";

            record.ErrorMessage = @params["error"]?.ToString();

            // Parse duration
            if (float.TryParse(@params["duration"]?.ToString(), out var duration))
                record.DurationSeconds = duration;

            // Parse cost
            if (float.TryParse(@params["cost"]?.ToString(), out var cost))
                record.CostCredits = cost;

            // Parse parameters
            var paramsToken = @params["parameters"];
            if (paramsToken != null && paramsToken.Type == JTokenType.Object)
            {
                record.Parameters = paramsToken.ToObject<Dictionary<string, object>>();
            }

            var result = await registry.AddGenerationRecordAsync(assetId, record);

            if (!result.Success)
                return new ErrorResponse(result.Message);

            return new SuccessResponse($"Added generation record to {assetId}", new
            {
                asset_id = assetId,
                record_id = record.Id,
                source = record.Source.ToString(),
                success = record.Success,
                job_id = record.JobId
            });
        }

        private static async Task<object> GetHistory(JObject @params, IAssetRegistry registry)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("Missing required parameter: path");

            var history = await registry.GetGenerationHistoryAsync(path);

            return new SuccessResponse($"Generation history for {path}: {history.Count} records", new
            {
                path = path,
                record_count = history.Count,
                total_cost = history.Sum(r => r.CostCredits),
                records = history.Select(r => new
                {
                    id = r.Id,
                    timestamp = r.Timestamp.ToString("o"),
                    source = r.Source.ToString(),
                    prompt = r.Prompt,
                    job_id = r.JobId,
                    success = r.Success,
                    error = r.ErrorMessage,
                    duration_seconds = r.DurationSeconds,
                    cost_credits = r.CostCredits,
                    task_id = r.TaskId
                }).ToList()
            });
        }

        private static async Task<object> ValidateStyle(JObject @params, IAssetRegistry registry)
        {
            string assetId = @params["asset_id"]?.ToString() ?? @params["id"]?.ToString();
            if (string.IsNullOrEmpty(assetId))
                return new ErrorResponse("Missing required parameter: asset_id");

            if (!float.TryParse(@params["score"]?.ToString(), out var score))
                return new ErrorResponse("Missing required parameter: score (0.0 to 1.0)");

            var violationsToken = @params["violations"];
            List<string> violations = new List<string>();
            if (violationsToken != null)
            {
                if (violationsToken.Type == JTokenType.Array)
                    violations = violationsToken.ToObject<List<string>>();
                else if (violationsToken.Type == JTokenType.String)
                    violations = violationsToken.ToString().Split(',').Select(v => v.Trim()).ToList();
            }

            var result = await registry.UpdateStyleValidationAsync(assetId, score, violations);

            if (!result.Success)
                return new ErrorResponse(result.Message);

            return new SuccessResponse(result.Message, new
            {
                asset_id = assetId,
                style_score = score,
                violation_count = violations.Count,
                passed = score >= 0.8f
            });
        }

        private static async Task<object> ListAssets(IAssetRegistry registry)
        {
            var assets = await registry.GetAllAssetsAsync();

            return new SuccessResponse($"Registry contains {assets.Count} assets", new
            {
                total_count = assets.Count,
                assets = assets.Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    type = a.Type.ToString(),
                    source = a.Source.ToString(),
                    status = a.Status.ToString(),
                    path = a.Path,
                    iteration = a.Iteration,
                    style_score = a.StyleScore,
                    total_cost = a.TotalCostCredits,
                    generation_count = a.GenerationHistory?.Count ?? 0
                }).ToList()
            });
        }

        private static async Task<object> GetStats(IAssetRegistry registry)
        {
            var statusCounts = await registry.GetStatusCountsAsync();
            var totalCost = await registry.GetTotalCostAsync();
            var assets = await registry.GetAllAssetsAsync();

            var typeGroups = assets.GroupBy(a => a.Type)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

            var sourceGroups = assets.GroupBy(a => a.Source)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

            var iterationGroups = assets.GroupBy(a => a.Iteration ?? "unassigned")
                .ToDictionary(g => g.Key, g => g.Count());

            return new SuccessResponse($"Registry stats: {assets.Count} assets, {totalCost:F1} credits", new
            {
                total_assets = assets.Count,
                total_cost_credits = totalCost,
                by_status = statusCounts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                by_type = typeGroups,
                by_source = sourceGroups,
                by_iteration = iterationGroups,
                average_style_score = assets.Where(a => a.StyleScore.HasValue).Select(a => a.StyleScore.Value).DefaultIfEmpty(0).Average(),
                validated_count = assets.Count(a => a.Status == AssetStatus.Validated),
                failed_count = assets.Count(a => a.Status == AssetStatus.Failed)
            });
        }

        private static async Task<object> AddTag(JObject @params, IAssetRegistry registry)
        {
            string assetId = @params["asset_id"]?.ToString() ?? @params["id"]?.ToString();
            if (string.IsNullOrEmpty(assetId))
                return new ErrorResponse("Missing required parameter: asset_id");

            string tag = @params["tag"]?.ToString();
            if (string.IsNullOrEmpty(tag))
                return new ErrorResponse("Missing required parameter: tag");

            var result = await registry.AddTagAsync(assetId, tag);

            if (!result.Success)
                return new ErrorResponse(result.Message);

            return new SuccessResponse(result.Message, new
            {
                asset_id = assetId,
                tag = tag
            });
        }

        private static async Task<object> RemoveTag(JObject @params, IAssetRegistry registry)
        {
            string assetId = @params["asset_id"]?.ToString() ?? @params["id"]?.ToString();
            if (string.IsNullOrEmpty(assetId))
                return new ErrorResponse("Missing required parameter: asset_id");

            string tag = @params["tag"]?.ToString();
            if (string.IsNullOrEmpty(tag))
                return new ErrorResponse("Missing required parameter: tag");

            var result = await registry.RemoveTagAsync(assetId, tag);

            if (!result.Success)
                return new ErrorResponse(result.Message);

            return new SuccessResponse(result.Message, new
            {
                asset_id = assetId,
                tag = tag
            });
        }

        private static async Task<object> DeleteAsset(JObject @params, IAssetRegistry registry)
        {
            string assetId = @params["asset_id"]?.ToString() ?? @params["id"]?.ToString();
            if (string.IsNullOrEmpty(assetId))
                return new ErrorResponse("Missing required parameter: asset_id");

            var result = await registry.DeleteAssetAsync(assetId);

            if (!result.Success)
                return new ErrorResponse(result.Message);

            return new SuccessResponse(result.Message, new
            {
                asset_id = assetId,
                deleted = true
            });
        }

        private static async Task<object> ReloadRegistry(IAssetRegistry registry)
        {
            await registry.ReloadAsync();
            var assets = await registry.GetAllAssetsAsync();

            return new SuccessResponse($"Registry reloaded with {assets.Count} assets", new
            {
                asset_count = assets.Count
            });
        }

        private static object FormatAssetEntry(AssetRegistryEntry asset)
        {
            return new
            {
                id = asset.Id,
                name = asset.Name,
                type = asset.Type.ToString(),
                source = asset.Source.ToString(),
                status = asset.Status.ToString(),
                path = asset.Path,
                guid = asset.Guid,
                prompt = asset.Prompt,
                iteration = asset.Iteration,
                task_id = asset.TaskId,
                tags = asset.Tags,
                style_score = asset.StyleScore,
                style_violations = asset.StyleViolations,
                total_cost_credits = asset.TotalCostCredits,
                generation_count = asset.GenerationHistory?.Count ?? 0,
                created_at = asset.CreatedAt.ToString("o"),
                modified_at = asset.ModifiedAt.ToString("o"),
                imported_at = asset.ImportedAt?.ToString("o"),
                validated_at = asset.ValidatedAt?.ToString("o"),
                textures = asset.Textures != null ? new
                {
                    base_color = asset.Textures.BaseColor?.Path,
                    metallic = asset.Textures.Metallic?.Path,
                    roughness = asset.Textures.Roughness?.Path,
                    normal = asset.Textures.Normal?.Path
                } : null,
                metadata = asset.Metadata
            };
        }
    }
}
#endif
