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
    /// MCP tool to click UI Toolkit buttons by name.
    /// Enables Claude to interact with any game's UI during Play Mode.
    /// </summary>
    [McpForUnityTool("click_ui_button", Description = "Clicks a UI Toolkit button by name. Use during Play Mode to interact with the game's UI. Returns available buttons if target not found.")]
    public static class ClickUIButton
    {
        public static object HandleCommand(JObject @params)
        {
            string buttonName = @params["button_name"]?.ToString() ?? @params["buttonName"]?.ToString();

            if (string.IsNullOrWhiteSpace(buttonName))
            {
                return new ErrorResponse("Required parameter 'button_name' is missing or empty.");
            }

            if (!EditorApplication.isPlaying)
            {
                return new ErrorResponse("Cannot click UI buttons outside of Play Mode. Use manage_editor(action='play') first.");
            }

            try
            {
                var uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);

                if (uiDocuments.Length == 0)
                {
                    return new ErrorResponse("No UIDocument found in scene. Ensure the UI system is initialized.");
                }

                foreach (var doc in uiDocuments)
                {
                    if (doc.rootVisualElement == null) continue;

                    // Skip invisible UI documents
                    if (doc.rootVisualElement.style.display == DisplayStyle.None) continue;
                    if (!doc.gameObject.activeInHierarchy) continue;

                    var button = doc.rootVisualElement.Q<Button>(buttonName);
                    if (button != null && button.enabledSelf && button.visible)
                    {
                        // Dispatch click event through the UI Toolkit event system
                        using (var clickEvt = ClickEvent.GetPooled())
                        {
                            clickEvt.target = button;
                            button.panel?.visualTree.SendEvent(clickEvt);
                        }

                        // Also dispatch NavigationSubmitEvent for accessibility
                        using (var submitEvt = NavigationSubmitEvent.GetPooled())
                        {
                            submitEvt.target = button;
                            button.SendEvent(submitEvt);
                        }

                        return new SuccessResponse($"Successfully clicked button '{buttonName}'", new
                        {
                            button_name = buttonName,
                            button_text = button.text,
                            ui_document = doc.gameObject.name
                        });
                    }
                }

                // Button not found - provide helpful info about what IS available
                var visibleButtons = GetVisibleButtons(uiDocuments);
                return new ErrorResponse($"Button '{buttonName}' not found or not interactable.", new
                {
                    available_buttons = visibleButtons,
                    hint = "Use get_ui_state to see all UI elements"
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error clicking button '{buttonName}': {e.Message}");
            }
        }

        private static List<object> GetVisibleButtons(UIDocument[] uiDocuments)
        {
            var buttons = new List<object>();
            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement == null) continue;
                if (doc.rootVisualElement.style.display == DisplayStyle.None) continue;
                if (!doc.gameObject.activeInHierarchy) continue;

                var allButtons = doc.rootVisualElement.Query<Button>().ToList();
                foreach (var btn in allButtons)
                {
                    if (btn.enabledSelf && btn.visible && !string.IsNullOrEmpty(btn.name))
                    {
                        buttons.Add(new
                        {
                            name = btn.name,
                            text = btn.text,
                            screen = doc.gameObject.name
                        });
                    }
                }
            }
            return buttons;
        }
    }
}
