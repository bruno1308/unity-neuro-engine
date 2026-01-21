#if UNITY_EDITOR
using System;
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
    /// MCP tool to scan for null/missing serialized field references.
    /// Detects Inspector fields that were never assigned or have broken references.
    /// </summary>
    [McpForUnityTool("scan_missing_references", Description = "Scans GameObjects, scenes, or prefabs for null/missing serialized field references. Returns a report of all missing references with severity levels.")]
    public static class ScanMissingReferences
    {
        private static MissingReferenceDetector _detector;

        public static object HandleCommand(JObject @params)
        {
            string target = @params["target"]?.ToString();
            string targetType = @params["target_type"]?.ToString() ?? "scene";
            bool includeChildren = @params["include_children"]?.Value<bool>() ?? true;

            _detector ??= new MissingReferenceDetector();

            try
            {
                MissingReferenceReport report;

                switch (targetType.ToLowerInvariant())
                {
                    case "gameobject":
                        if (string.IsNullOrEmpty(target))
                        {
                            return new ErrorResponse("target parameter required when target_type is 'gameobject'");
                        }
                        var go = GameObject.Find(target);
                        if (go == null)
                        {
                            return new ErrorResponse($"GameObject '{target}' not found in scene");
                        }
                        report = _detector.Scan(go, includeChildren);
                        break;

                    case "prefab":
                        if (string.IsNullOrEmpty(target))
                        {
                            return new ErrorResponse("target parameter required when target_type is 'prefab'");
                        }
                        report = _detector.ScanPrefab(target);
                        break;

                    case "scene":
                    default:
                        report = _detector.ScanScene();
                        break;
                }

                return new SuccessResponse(report.Summary, new
                {
                    passed = report.Passed,
                    scanned_target = report.ScannedTarget,
                    timestamp = report.Timestamp,
                    total_fields_scanned = report.TotalFieldsScanned,
                    null_count = report.NullCount,
                    error_count = report.ErrorCount,
                    warning_count = report.WarningCount,
                    references = report.References.ConvertAll(r => new
                    {
                        object_path = r.ObjectPath,
                        component_type = r.ComponentType,
                        field_name = r.FieldName,
                        expected_type = r.ExpectedType,
                        severity = r.Severity,
                        array_index = r.ArrayIndex,
                        description = r.Description
                    })
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error scanning for missing references: {e.Message}");
            }
        }
    }
}
#endif
