#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to interact with UI elements (Toggle, Dropdown, Slider).
    /// Complements ClickUIButton and SetTextField for full UI coverage.
    /// Supports both UI Toolkit and uGUI.
    /// </summary>
    [McpForUnityTool("interact_ui_element", Description = "Interacts with UI elements: toggles, dropdowns, sliders. Supports both UI Toolkit and uGUI. Use during Play Mode.")]
    public static class InteractUIElement
    {
        public static object HandleCommand(JObject @params)
        {
            string elementName = @params["element_name"]?.ToString() ?? @params["name"]?.ToString();
            string action = @params["action"]?.ToString()?.ToLowerInvariant();

            if (string.IsNullOrEmpty(elementName))
            {
                return new ErrorResponse("Required parameter 'element_name' is missing.");
            }

            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse("Required parameter 'action' is missing. Use: toggle, set_value, select_option, get_value");
            }

            if (!EditorApplication.isPlaying)
            {
                return new ErrorResponse("Cannot interact with UI outside of Play Mode.");
            }

            try
            {
                // Try UI Toolkit first
                var result = TryUIToolkit(elementName, action, @params);
                if (result != null) return result;

                // Fall back to uGUI
                result = TryUGUI(elementName, action, @params);
                if (result != null) return result;

                // Element not found
                return new ErrorResponse($"UI element '{elementName}' not found.", new
                {
                    hint = "Use get_ui_accessibility_graph to see available elements",
                    searched_in = new[] { "UI Toolkit", "uGUI" }
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error interacting with UI: {e.Message}");
            }
        }

        #region UI Toolkit

        private static object TryUIToolkit(string elementName, string action, JObject @params)
        {
            var uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);

            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement == null) continue;
                if (!doc.gameObject.activeInHierarchy) continue;

                // Try Toggle
                var toggle = doc.rootVisualElement.Q<UnityEngine.UIElements.Toggle>(elementName);
                if (toggle != null && toggle.enabledSelf)
                {
                    return HandleUIToolkitToggle(toggle, action, @params, doc.gameObject.name);
                }

                // Try Slider
                var slider = doc.rootVisualElement.Q<UnityEngine.UIElements.Slider>(elementName);
                if (slider != null && slider.enabledSelf)
                {
                    return HandleUIToolkitSlider(slider, action, @params, doc.gameObject.name);
                }

                var sliderInt = doc.rootVisualElement.Q<SliderInt>(elementName);
                if (sliderInt != null && sliderInt.enabledSelf)
                {
                    return HandleUIToolkitSliderInt(sliderInt, action, @params, doc.gameObject.name);
                }

                // Try Dropdown
                var dropdown = doc.rootVisualElement.Q<DropdownField>(elementName);
                if (dropdown != null && dropdown.enabledSelf)
                {
                    return HandleUIToolkitDropdown(dropdown, action, @params, doc.gameObject.name);
                }
            }

            return null;
        }

        private static object HandleUIToolkitToggle(UnityEngine.UIElements.Toggle toggle, string action, JObject @params, string docName)
        {
            bool previousValue = toggle.value;

            switch (action)
            {
                case "toggle":
                    toggle.value = !toggle.value;
                    return new SuccessResponse($"Toggled '{toggle.name}'", new
                    {
                        element_name = toggle.name,
                        element_type = "Toggle",
                        previous_value = previousValue,
                        new_value = toggle.value,
                        ui_system = "UIToolkit",
                        ui_document = docName
                    });

                case "set_value":
                case "set":
                    bool setValue = @params["value"]?.Value<bool>() ?? true;
                    toggle.value = setValue;
                    return new SuccessResponse($"Set toggle '{toggle.name}' to {setValue}", new
                    {
                        element_name = toggle.name,
                        previous_value = previousValue,
                        new_value = toggle.value
                    });

                case "get_value":
                case "get":
                    return new SuccessResponse($"Toggle '{toggle.name}' value", new
                    {
                        element_name = toggle.name,
                        value = toggle.value,
                        label = toggle.label
                    });

                default:
                    return new ErrorResponse($"Invalid action '{action}' for Toggle. Use: toggle, set_value, get_value");
            }
        }

        private static object HandleUIToolkitSlider(UnityEngine.UIElements.Slider slider, string action, JObject @params, string docName)
        {
            float previousValue = slider.value;

            switch (action)
            {
                case "set_value":
                case "set":
                    float newValue = @params["value"]?.Value<float>() ?? slider.value;
                    newValue = Mathf.Clamp(newValue, slider.lowValue, slider.highValue);
                    slider.value = newValue;
                    return new SuccessResponse($"Set slider '{slider.name}' to {newValue}", new
                    {
                        element_name = slider.name,
                        element_type = "Slider",
                        previous_value = previousValue,
                        new_value = slider.value,
                        min = slider.lowValue,
                        max = slider.highValue,
                        ui_system = "UIToolkit"
                    });

                case "get_value":
                case "get":
                    return new SuccessResponse($"Slider '{slider.name}' value", new
                    {
                        element_name = slider.name,
                        value = slider.value,
                        min = slider.lowValue,
                        max = slider.highValue
                    });

                default:
                    return new ErrorResponse($"Invalid action '{action}' for Slider. Use: set_value, get_value");
            }
        }

        private static object HandleUIToolkitSliderInt(SliderInt slider, string action, JObject @params, string docName)
        {
            int previousValue = slider.value;

            switch (action)
            {
                case "set_value":
                case "set":
                    int newValue = @params["value"]?.Value<int>() ?? slider.value;
                    newValue = Mathf.Clamp(newValue, slider.lowValue, slider.highValue);
                    slider.value = newValue;
                    return new SuccessResponse($"Set slider '{slider.name}' to {newValue}", new
                    {
                        element_name = slider.name,
                        previous_value = previousValue,
                        new_value = slider.value
                    });

                case "get_value":
                case "get":
                    return new SuccessResponse($"Slider '{slider.name}' value", new
                    {
                        element_name = slider.name,
                        value = slider.value
                    });

                default:
                    return new ErrorResponse($"Invalid action '{action}' for Slider.");
            }
        }

        private static object HandleUIToolkitDropdown(DropdownField dropdown, string action, JObject @params, string docName)
        {
            string previousValue = dropdown.value;

            switch (action)
            {
                case "select_option":
                case "select":
                    string option = @params["option"]?.ToString() ?? @params["value"]?.ToString();
                    int? index = @params["index"]?.Value<int>();

                    if (index.HasValue && index >= 0 && index < dropdown.choices.Count)
                    {
                        dropdown.index = index.Value;
                    }
                    else if (!string.IsNullOrEmpty(option) && dropdown.choices.Contains(option))
                    {
                        dropdown.value = option;
                    }
                    else
                    {
                        return new ErrorResponse($"Invalid option. Available: {string.Join(", ", dropdown.choices)}");
                    }

                    return new SuccessResponse($"Selected '{dropdown.value}' in dropdown '{dropdown.name}'", new
                    {
                        element_name = dropdown.name,
                        element_type = "Dropdown",
                        previous_value = previousValue,
                        new_value = dropdown.value,
                        new_index = dropdown.index,
                        ui_system = "UIToolkit"
                    });

                case "get_value":
                case "get":
                    return new SuccessResponse($"Dropdown '{dropdown.name}' value", new
                    {
                        element_name = dropdown.name,
                        value = dropdown.value,
                        index = dropdown.index,
                        choices = dropdown.choices.ToList()
                    });

                default:
                    return new ErrorResponse($"Invalid action '{action}' for Dropdown. Use: select_option, get_value");
            }
        }

        #endregion

        #region uGUI

        private static object TryUGUI(string elementName, string action, JObject @params)
        {
            // Find uGUI Toggle
            var toggles = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Toggle>(FindObjectsSortMode.None);
            var toggle = toggles.FirstOrDefault(t => t.name == elementName && t.IsInteractable());
            if (toggle != null)
            {
                return HandleUGUIToggle(toggle, action, @params);
            }

            // Find uGUI Slider
            var sliders = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Slider>(FindObjectsSortMode.None);
            var slider = sliders.FirstOrDefault(s => s.name == elementName && s.IsInteractable());
            if (slider != null)
            {
                return HandleUGUISlider(slider, action, @params);
            }

            // Find uGUI Dropdown
            var dropdowns = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Dropdown>(FindObjectsSortMode.None);
            var dropdown = dropdowns.FirstOrDefault(d => d.name == elementName && d.IsInteractable());
            if (dropdown != null)
            {
                return HandleUGUIDropdown(dropdown, action, @params);
            }

            return null;
        }

        private static object HandleUGUIToggle(UnityEngine.UI.Toggle toggle, string action, JObject @params)
        {
            bool previousValue = toggle.isOn;

            switch (action)
            {
                case "toggle":
                    toggle.isOn = !toggle.isOn;
                    return new SuccessResponse($"Toggled '{toggle.name}'", new
                    {
                        element_name = toggle.name,
                        previous_value = previousValue,
                        new_value = toggle.isOn,
                        ui_system = "uGUI"
                    });

                case "set_value":
                case "set":
                    bool setValue = @params["value"]?.Value<bool>() ?? true;
                    toggle.isOn = setValue;
                    return new SuccessResponse($"Set toggle '{toggle.name}' to {setValue}", new
                    {
                        element_name = toggle.name,
                        previous_value = previousValue,
                        new_value = toggle.isOn
                    });

                case "get_value":
                case "get":
                    return new SuccessResponse($"Toggle '{toggle.name}' value", new
                    {
                        element_name = toggle.name,
                        value = toggle.isOn
                    });

                default:
                    return new ErrorResponse($"Invalid action '{action}' for Toggle.");
            }
        }

        private static object HandleUGUISlider(UnityEngine.UI.Slider slider, string action, JObject @params)
        {
            float previousValue = slider.value;

            switch (action)
            {
                case "set_value":
                case "set":
                    float newValue = @params["value"]?.Value<float>() ?? slider.value;
                    newValue = Mathf.Clamp(newValue, slider.minValue, slider.maxValue);
                    slider.value = newValue;
                    return new SuccessResponse($"Set slider '{slider.name}' to {newValue}", new
                    {
                        element_name = slider.name,
                        previous_value = previousValue,
                        new_value = slider.value,
                        min = slider.minValue,
                        max = slider.maxValue,
                        ui_system = "uGUI"
                    });

                case "get_value":
                case "get":
                    return new SuccessResponse($"Slider '{slider.name}' value", new
                    {
                        element_name = slider.name,
                        value = slider.value,
                        min = slider.minValue,
                        max = slider.maxValue
                    });

                default:
                    return new ErrorResponse($"Invalid action '{action}' for Slider.");
            }
        }

        private static object HandleUGUIDropdown(UnityEngine.UI.Dropdown dropdown, string action, JObject @params)
        {
            if (dropdown.options == null || dropdown.options.Count == 0)
            {
                return new ErrorResponse($"Dropdown '{dropdown.name}' has no options");
            }

            int previousIndex = dropdown.value;
            string previousValue = dropdown.options[previousIndex].text;

            switch (action)
            {
                case "select_option":
                case "select":
                    string option = @params["option"]?.ToString() ?? @params["value"]?.ToString();
                    int? index = @params["index"]?.Value<int>();

                    if (index.HasValue && index >= 0 && index < dropdown.options.Count)
                    {
                        dropdown.value = index.Value;
                    }
                    else if (!string.IsNullOrEmpty(option))
                    {
                        var optionIndex = dropdown.options.FindIndex(o => o.text == option);
                        if (optionIndex >= 0)
                        {
                            dropdown.value = optionIndex;
                        }
                        else
                        {
                            var availableOptions = dropdown.options.Select(o => o.text).ToList();
                            return new ErrorResponse($"Invalid option '{option}'. Available: {string.Join(", ", availableOptions)}");
                        }
                    }

                    return new SuccessResponse($"Selected '{dropdown.options[dropdown.value].text}'", new
                    {
                        element_name = dropdown.name,
                        previous_value = previousValue,
                        new_value = dropdown.options[dropdown.value].text,
                        new_index = dropdown.value,
                        ui_system = "uGUI"
                    });

                case "get_value":
                case "get":
                    return new SuccessResponse($"Dropdown '{dropdown.name}' value", new
                    {
                        element_name = dropdown.name,
                        value = dropdown.options[dropdown.value].text,
                        index = dropdown.value,
                        choices = dropdown.options.Select(o => o.text).ToList()
                    });

                default:
                    return new ErrorResponse($"Invalid action '{action}' for Dropdown.");
            }
        }

        #endregion
    }
}
#endif
