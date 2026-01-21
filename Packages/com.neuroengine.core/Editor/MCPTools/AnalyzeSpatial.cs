#if UNITY_EDITOR
using System;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Services;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to analyze spatial relationships and detect anomalies.
    /// Finds off-screen objects, scale problems, and collider overlaps.
    /// </summary>
    [McpForUnityTool("analyze_spatial", Description = "Analyzes spatial relationships in the scene. Detects off-screen objects, scale anomalies, and collider overlaps. Useful for finding objects that may be invisible or incorrectly placed.")]
    public static class AnalyzeSpatial
    {
        private static SpatialAnalysisService _spatialService;

        public static object HandleCommand(JObject @params)
        {
            float minScale = @params["min_scale"]?.Value<float>() ?? 0.01f;
            float maxScale = @params["max_scale"]?.Value<float>() ?? 100f;
            bool checkOverlaps = @params["check_overlaps"]?.Value<bool>() ?? true;
            string cameraName = @params["camera"]?.ToString();

            _spatialService ??= new SpatialAnalysisService();

            try
            {
                // Build report using individual methods to respect parameters
                var offScreen = _spatialService.FindOffScreenObjects();
                var scaleAnomalies = _spatialService.FindScaleAnomalies(minScale, maxScale);
                var overlaps = checkOverlaps ? _spatialService.FindOverlappingColliders() : new System.Collections.Generic.List<NeuroEngine.Core.ColliderOverlap>();

                int issuesFound = offScreen.Count + scaleAnomalies.Count + overlaps.Count;
                string summary = $"Found {issuesFound} issues: {offScreen.Count} off-screen, {scaleAnomalies.Count} scale anomalies, {overlaps.Count} overlaps";

                return new SuccessResponse(summary, new
                {
                    timestamp = DateTime.UtcNow.ToString("o"),
                    issues_found = issuesFound,
                    parameters_used = new { min_scale = minScale, max_scale = maxScale, check_overlaps = checkOverlaps },
                    off_screen_objects = offScreen.Select(o => new
                    {
                        path = o.ObjectPath,
                        position = o.WorldPosition,
                        reason = o.Reason,
                        distance = o.DistanceFromView
                    }).ToList(),
                    scale_anomalies = scaleAnomalies.Select(a => new
                    {
                        path = a.ObjectPath,
                        scale = a.Scale,
                        reason = a.Reason
                    }).ToList(),
                    collider_overlaps = overlaps.Select(o => new
                    {
                        object1_path = o.Object1Path,
                        object2_path = o.Object2Path,
                        collider_type1 = o.ColliderType1,
                        collider_type2 = o.ColliderType2,
                        penetration_depth = o.PenetrationDepth
                    }).ToList()
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error analyzing spatial relationships: {e.Message}");
            }
        }
    }
}
#endif
