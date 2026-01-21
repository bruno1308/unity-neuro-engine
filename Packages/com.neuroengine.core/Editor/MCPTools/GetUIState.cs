using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to query the current state of UI Toolkit screens and elements.
    /// Works with any Unity game using UI Toolkit. Essential for automated UI testing.
    /// </summary>
    [McpForUnityTool("get_ui_state", Description = "Gets the current state of UI Toolkit screens - which are visible, what buttons/fields/labels are available, and their states. Requires Play Mode.")]
    public static class GetUIState
    {
        public static object HandleCommand(JObject @params)
        {
            string screenFilter = @params["screen"]?.ToString();
            bool includeHidden = @params["include_hidden"]?.Value<bool>() ?? false;

            if (!EditorApplication.isPlaying)
            {
                return new ErrorResponse("Cannot query UI state outside of Play Mode. Use manage_editor(action='play') first.");
            }

            try
            {
                var uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);

                if (uiDocuments.Length == 0)
                {
                    return new ErrorResponse("No UIDocument found in scene. The game may not be using UI Toolkit.");
                }

                var screens = new List<object>();

                foreach (var doc in uiDocuments)
                {
                    if (doc.rootVisualElement == null) continue;

                    string screenName = doc.gameObject.name;

                    // Apply filter if provided
                    if (!string.IsNullOrEmpty(screenFilter) &&
                        !screenName.Contains(screenFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    bool isVisible = doc.rootVisualElement.style.display != DisplayStyle.None &&
                                    doc.gameObject.activeInHierarchy;

                    // Skip hidden screens unless requested
                    if (!isVisible && !includeHidden) continue;

                    var buttons = GetButtonInfo(doc.rootVisualElement);
                    var textFields = GetTextFieldInfo(doc.rootVisualElement);
                    var labels = GetLabelInfo(doc.rootVisualElement);
                    var toggles = GetToggleInfo(doc.rootVisualElement);
                    var dropdowns = GetDropdownInfo(doc.rootVisualElement);

                    screens.Add(new
                    {
                        name = screenName,
                        visible = isVisible,
                        buttons = buttons,
                        text_fields = textFields,
                        labels = labels,
                        toggles = toggles,
                        dropdowns = dropdowns
                    });
                }

                return new SuccessResponse("UI state retrieved successfully", new
                {
                    play_mode = EditorApplication.isPlaying,
                    screen_count = screens.Count,
                    screens = screens
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error getting UI state: {e.Message}");
            }
        }

        private static List<object> GetButtonInfo(VisualElement root)
        {
            var buttons = new List<object>();
            var allButtons = root.Query<Button>().ToList();

            foreach (var btn in allButtons)
            {
                if (string.IsNullOrEmpty(btn.name)) continue;

                buttons.Add(new
                {
                    name = btn.name,
                    text = btn.text,
                    enabled = btn.enabledSelf,
                    visible = btn.visible && btn.resolvedStyle.display != DisplayStyle.None
                });
            }
            return buttons;
        }

        private static List<object> GetTextFieldInfo(VisualElement root)
        {
            var fields = new List<object>();
            var allFields = root.Query<TextField>().ToList();

            foreach (var field in allFields)
            {
                if (string.IsNullOrEmpty(field.name)) continue;

                fields.Add(new
                {
                    name = field.name,
                    value = field.value,
                    label = field.label,
                    enabled = field.enabledSelf,
                    is_password = field.isPasswordField
                });
            }
            return fields;
        }

        private static List<object> GetLabelInfo(VisualElement root)
        {
            var labels = new List<object>();
            var allLabels = root.Query<Label>().ToList();

            foreach (var label in allLabels)
            {
                // Only include named labels or labels with meaningful text
                if (string.IsNullOrEmpty(label.name) && string.IsNullOrEmpty(label.text)) continue;

                labels.Add(new
                {
                    name = label.name,
                    text = label.text
                });
            }
            return labels;
        }

        private static List<object> GetToggleInfo(VisualElement root)
        {
            var toggles = new List<object>();
            var allToggles = root.Query<Toggle>().ToList();

            foreach (var toggle in allToggles)
            {
                if (string.IsNullOrEmpty(toggle.name)) continue;

                toggles.Add(new
                {
                    name = toggle.name,
                    label = toggle.label,
                    value = toggle.value,
                    enabled = toggle.enabledSelf
                });
            }
            return toggles;
        }

        private static List<object> GetDropdownInfo(VisualElement root)
        {
            var dropdowns = new List<object>();
            var allDropdowns = root.Query<DropdownField>().ToList();

            foreach (var dropdown in allDropdowns)
            {
                if (string.IsNullOrEmpty(dropdown.name)) continue;

                dropdowns.Add(new
                {
                    name = dropdown.name,
                    label = dropdown.label,
                    value = dropdown.value,
                    choices = dropdown.choices,
                    enabled = dropdown.enabledSelf
                });
            }
            return dropdowns;
        }
    }
}
