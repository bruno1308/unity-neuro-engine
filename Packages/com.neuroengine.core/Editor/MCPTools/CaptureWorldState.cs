#if UNITY_EDITOR
using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to capture a complete world state snapshot.
    /// Uses EyesPolecat to aggregate all Layer 2 observation data.
    /// </summary>
    [McpForUnityTool("capture_world_state", Description = "Captures a complete world state snapshot including scene state, UI graph, spatial analysis, and validation. Optionally saves the snapshot to disk.")]
    public static class CaptureWorldState
    {
        public static object HandleCommand(JObject @params)
        {
            bool saveSnapshot = @params["save"]?.Value<bool>() ?? false;
            bool includeScreenshot = @params["screenshot"]?.Value<bool>() ?? false;

            try
            {
                var worldState = EyesPolecat.CaptureWorldState();

                string snapshotPath = null;
                if (saveSnapshot)
                {
                    snapshotPath = EyesPolecat.SaveSnapshot(worldState);
                }

                string screenshotPath = null;
                if (includeScreenshot)
                {
                    screenshotPath = EyesPolecat.CaptureScreenshot(worldState.SceneName);
                }

                return new SuccessResponse($"World state captured for scene '{worldState.SceneName}'", new
                {
                    timestamp = worldState.Timestamp,
                    scene_name = worldState.SceneName,
                    trigger = worldState.Trigger ?? "API",
                    snapshot_path = snapshotPath,
                    screenshot_path = screenshotPath,
                    scene = worldState.Scene != null ? new
                    {
                        scene_name = worldState.Scene.SceneName,
                        root_objects = worldState.Scene.RootObjects?.Length ?? 0
                    } : null,
                    ui = worldState.UI != null ? new
                    {
                        total_elements = worldState.UI.TotalElements,
                        interactable_count = worldState.UI.InteractableCount,
                        blocked_count = worldState.UI.BlockedCount,
                        ui_system = worldState.UI.UISystem
                    } : null,
                    spatial = worldState.Spatial != null ? new
                    {
                        issues_found = worldState.Spatial.IssuesFound,
                        off_screen_count = worldState.Spatial.OffScreenObjects?.Count ?? 0,
                        scale_anomaly_count = worldState.Spatial.ScaleAnomalies?.Count ?? 0,
                        overlap_count = worldState.Spatial.Overlaps?.Count ?? 0
                    } : null,
                    validation = worldState.Validation != null ? new
                    {
                        passed = !worldState.Validation.HasErrors,
                        error_count = worldState.Validation.ErrorCount,
                        warning_count = worldState.Validation.WarningCount,
                        summary = worldState.Validation.Summary
                    } : null
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error capturing world state: {e.Message}");
            }
        }
    }
}
#endif
