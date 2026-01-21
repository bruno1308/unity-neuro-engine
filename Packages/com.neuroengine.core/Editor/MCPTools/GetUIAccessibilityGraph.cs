#if UNITY_EDITOR
using System;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using Newtonsoft.Json.Linq;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to capture UI state as a queryable accessibility graph.
    /// The "DOM for games" - shows all UI elements, their states, and what's interactable.
    /// </summary>
    [McpForUnityTool("get_ui_accessibility_graph", Description = "Captures complete UI state as an accessibility graph. Shows all UI elements (buttons, fields, labels), their positions, visibility, interactability, and what's blocking them. Works with both UI Toolkit and uGUI.")]
    public static class GetUIAccessibilityGraph
    {
        public static object HandleCommand(JObject @params)
        {
            string filter = @params["filter"]?.ToString();
            bool interactableOnly = @params["interactable_only"]?.Value<bool>() ?? false;
            bool blockedOnly = @params["blocked_only"]?.Value<bool>() ?? false;

            var uiService = EditorServiceLocator.Get<IUIAccessibility>();

            try
            {
                var graph = uiService.CaptureUIState();
                var elements = graph.Elements;

                // Apply filters
                if (!string.IsNullOrEmpty(filter))
                {
                    elements = elements.Where(e =>
                        e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        e.Type.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        e.Path.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                if (interactableOnly)
                {
                    elements = elements.Where(e => e.Interactable && e.Visible && string.IsNullOrEmpty(e.BlockedBy)).ToList();
                }

                if (blockedOnly)
                {
                    elements = elements.Where(e => !string.IsNullOrEmpty(e.BlockedBy)).ToList();
                }

                return new SuccessResponse($"Captured {elements.Count} UI elements", new
                {
                    timestamp = graph.Timestamp,
                    ui_system = graph.UISystem,
                    total_elements = graph.TotalElements,
                    interactable_count = graph.InteractableCount,
                    blocked_count = graph.BlockedCount,
                    filtered_count = elements.Count,
                    elements = elements.Select(e => new
                    {
                        name = e.Name,
                        type = e.Type,
                        path = e.Path,
                        screen_position = e.ScreenPosition,
                        size = e.Size,
                        visible = e.Visible,
                        interactable = e.Interactable,
                        blocked_by = e.BlockedBy,
                        source = e.Source,
                        properties = e.Properties
                    }).ToList()
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error capturing UI accessibility graph: {e.Message}");
            }
        }
    }
}
#endif
