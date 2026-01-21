using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to set the value of UI Toolkit text fields.
    /// Enables Claude to enter text into forms during Play Mode.
    /// </summary>
    [McpForUnityTool("set_text_field", Description = "Sets the value of a UI Toolkit TextField by name. Use during Play Mode to enter text into forms.")]
    public static class SetTextField
    {
        public static object HandleCommand(JObject @params)
        {
            string fieldName = @params["field_name"]?.ToString() ?? @params["fieldName"]?.ToString();
            string value = @params["value"]?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return new ErrorResponse("Required parameter 'field_name' is missing or empty.");
            }

            if (!EditorApplication.isPlaying)
            {
                return new ErrorResponse("Cannot set text fields outside of Play Mode. Use manage_editor(action='play') first.");
            }

            try
            {
                var uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);

                if (uiDocuments.Length == 0)
                {
                    return new ErrorResponse("No UIDocument found in scene.");
                }

                foreach (var doc in uiDocuments)
                {
                    if (doc.rootVisualElement == null) continue;
                    if (doc.rootVisualElement.style.display == DisplayStyle.None) continue;
                    if (!doc.gameObject.activeInHierarchy) continue;

                    var field = doc.rootVisualElement.Q<TextField>(fieldName);
                    if (field != null && field.enabledSelf)
                    {
                        string previousValue = field.value;
                        field.value = value;

                        // Dispatch change event so UI can react
                        using (var changeEvt = ChangeEvent<string>.GetPooled(previousValue, value))
                        {
                            changeEvt.target = field;
                            field.SendEvent(changeEvt);
                        }

                        return new SuccessResponse($"Successfully set '{fieldName}' to '{value}'", new
                        {
                            field_name = fieldName,
                            previous_value = previousValue,
                            new_value = value,
                            ui_document = doc.gameObject.name
                        });
                    }
                }

                // Field not found - list available fields
                var availableFields = GetAvailableFields(uiDocuments);
                return new ErrorResponse($"TextField '{fieldName}' not found or not enabled.", new
                {
                    available_fields = availableFields,
                    hint = "Use get_ui_state to see all UI elements"
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error setting text field '{fieldName}': {e.Message}");
            }
        }

        private static System.Collections.Generic.List<object> GetAvailableFields(UIDocument[] uiDocuments)
        {
            var fields = new System.Collections.Generic.List<object>();
            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement == null) continue;
                if (doc.rootVisualElement.style.display == DisplayStyle.None) continue;
                if (!doc.gameObject.activeInHierarchy) continue;

                var allFields = doc.rootVisualElement.Query<TextField>().ToList();
                foreach (var field in allFields)
                {
                    if (field.enabledSelf && !string.IsNullOrEmpty(field.name))
                    {
                        fields.Add(new
                        {
                            name = field.name,
                            label = field.label,
                            screen = doc.gameObject.name
                        });
                    }
                }
            }
            return fields;
        }
    }
}
