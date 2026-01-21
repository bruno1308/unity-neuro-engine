using System;
using System.Collections.Generic;
using System.Linq;
using NeuroEngine.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Captures UI state as a queryable graph - the "DOM for games".
    /// This is Layer 2 (Observation) - enables AI to understand what UI is on screen.
    /// Supports both UI Toolkit (primary) and uGUI (legacy).
    /// </summary>
    public class UIAccessibilityService : IUIAccessibility
    {
        public UIGraph CaptureUIState()
        {
            var graph = new UIGraph
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                Elements = new List<UIElement>()
            };

            bool hasUIToolkit = false;
            bool hasUGUI = false;

            // Capture UI Toolkit elements (primary)
            var uiToolkitElements = CaptureUIToolkitElements();
            if (uiToolkitElements.Count > 0)
            {
                hasUIToolkit = true;
                graph.Elements.AddRange(uiToolkitElements);
            }

            // Capture uGUI elements (secondary)
            var uguiElements = CaptureUGUIElements();
            if (uguiElements.Count > 0)
            {
                hasUGUI = true;
                graph.Elements.AddRange(uguiElements);
            }

            // Detect occlusion
            DetectOcclusion(graph.Elements);

            // Calculate stats
            graph.TotalElements = graph.Elements.Count;
            graph.InteractableCount = graph.Elements.Count(e => e.Interactable && string.IsNullOrEmpty(e.BlockedBy));
            graph.BlockedCount = graph.Elements.Count(e => !string.IsNullOrEmpty(e.BlockedBy));

            if (hasUIToolkit && hasUGUI)
                graph.UISystem = "Both";
            else if (hasUIToolkit)
                graph.UISystem = "UIToolkit";
            else if (hasUGUI)
                graph.UISystem = "uGUI";
            else
                graph.UISystem = "None";

            return graph;
        }

        public UIElement FindElementByName(string name)
        {
            var graph = CaptureUIState();
            return graph.Elements.FirstOrDefault(e =>
                string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public List<UIElement> FindInteractableElements()
        {
            var graph = CaptureUIState();
            return graph.Elements
                .Where(e => e.Interactable && e.Visible && string.IsNullOrEmpty(e.BlockedBy))
                .ToList();
        }

        public List<UIElement> FindBlockedElements()
        {
            var graph = CaptureUIState();
            return graph.Elements
                .Where(e => !string.IsNullOrEmpty(e.BlockedBy))
                .ToList();
        }

        #region UI Toolkit

        private List<UIElement> CaptureUIToolkitElements()
        {
            var elements = new List<UIElement>();
            var uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);

            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement == null) continue;
                if (!doc.gameObject.activeInHierarchy) continue;

                TraverseVisualElement(doc.rootVisualElement, "", elements);
            }

            return elements;
        }

        private void TraverseVisualElement(VisualElement element, string parentPath, List<UIElement> elements)
        {
            string name = string.IsNullOrEmpty(element.name) ? element.GetType().Name : element.name;
            string path = string.IsNullOrEmpty(parentPath) ? name : parentPath + "/" + name;

            bool visible = element.visible &&
                          element.resolvedStyle.display != DisplayStyle.None &&
                          element.resolvedStyle.visibility != Visibility.Hidden;

            string elementType = GetUIToolkitType(element);
            bool interactable = IsUIToolkitInteractable(element);

            var worldBound = element.worldBound;
            float[] screenPos = new float[] { worldBound.center.x, Screen.height - worldBound.center.y };
            float[] size = new float[] { worldBound.width, worldBound.height };

            if (!string.IsNullOrEmpty(element.name) || interactable)
            {
                var uiElement = new UIElement
                {
                    Name = name,
                    Type = elementType,
                    Path = path,
                    ScreenPosition = screenPos,
                    Size = size,
                    Visible = visible,
                    Interactable = interactable && visible,
                    Source = "UIToolkit",
                    Properties = GetUIToolkitProperties(element)
                };

                elements.Add(uiElement);
            }

            foreach (var child in element.Children())
            {
                TraverseVisualElement(child, path, elements);
            }
        }

        private string GetUIToolkitType(VisualElement element)
        {
            return element switch
            {
                UnityEngine.UIElements.Button => "Button",
                TextField => "TextField",
                Label => "Label",
                UnityEngine.UIElements.Toggle => "Toggle",
                DropdownField => "Dropdown",
                UnityEngine.UIElements.Slider => "Slider",
                SliderInt => "Slider",
                ScrollView => "ScrollView",
                UnityEngine.UIElements.Image => "Image",
                _ => element.GetType().Name
            };
        }

        private bool IsUIToolkitInteractable(VisualElement element)
        {
            var current = element;
            while (current != null)
            {
                if (!current.enabledSelf) return false;
                current = current.parent;
            }

            return element switch
            {
                UnityEngine.UIElements.Button => true,
                TextField => true,
                UnityEngine.UIElements.Toggle => true,
                DropdownField => true,
                UnityEngine.UIElements.Slider => true,
                SliderInt => true,
                _ => element.pickingMode == PickingMode.Position && element.focusable
            };
        }

        private Dictionary<string, object> GetUIToolkitProperties(VisualElement element)
        {
            var props = new Dictionary<string, object>();

            switch (element)
            {
                case UnityEngine.UIElements.Button btn:
                    props["text"] = btn.text;
                    break;
                case TextField tf:
                    props["text"] = tf.value;
                    props["placeholder"] = tf.label;
                    props["isPassword"] = tf.isPasswordField;
                    break;
                case Label lbl:
                    props["text"] = lbl.text;
                    break;
                case UnityEngine.UIElements.Toggle toggle:
                    props["value"] = toggle.value;
                    props["text"] = toggle.label;
                    break;
                case DropdownField dropdown:
                    props["value"] = dropdown.value;
                    props["choices"] = dropdown.choices?.ToArray();
                    break;
                case UnityEngine.UIElements.Slider slider:
                    props["value"] = slider.value;
                    props["min"] = slider.lowValue;
                    props["max"] = slider.highValue;
                    break;
            }

            return props;
        }

        #endregion

        #region uGUI

        private List<UIElement> CaptureUGUIElements()
        {
            var elements = new List<UIElement>();
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);

            foreach (var canvas in canvases)
            {
                if (!canvas.gameObject.activeInHierarchy) continue;
                if (!canvas.enabled) continue;
                if (canvas.isRootCanvas)
                {
                    TraverseUGUIHierarchy(canvas.transform, "", elements, canvas);
                }
            }

            return elements;
        }

        private void TraverseUGUIHierarchy(Transform transform, string parentPath, List<UIElement> elements, Canvas rootCanvas)
        {
            var go = transform.gameObject;
            string path = string.IsNullOrEmpty(parentPath) ? go.name : parentPath + "/" + go.name;

            var graphic = go.GetComponent<Graphic>();
            var selectable = go.GetComponent<Selectable>();
            var canvasGroup = go.GetComponentInParent<CanvasGroup>();

            bool visible = go.activeInHierarchy;
            if (graphic != null) visible = visible && graphic.enabled && graphic.color.a > 0;
            if (canvasGroup != null) visible = visible && canvasGroup.alpha > 0;

            string elementType = GetUGUIType(go);
            bool interactable = IsUGUIInteractable(go, canvasGroup);

            var rectTransform = go.GetComponent<RectTransform>();
            float[] screenPos = new float[2];
            float[] size = new float[2];

            if (rectTransform != null)
            {
                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);

                Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : (rootCanvas.worldCamera ?? Camera.main);

                Vector2 screenMin, screenMax;
                if (cam != null)
                {
                    screenMin = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                    screenMax = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
                }
                else
                {
                    screenMin = corners[0];
                    screenMax = corners[2];
                }

                screenPos[0] = (screenMin.x + screenMax.x) / 2f;
                screenPos[1] = (screenMin.y + screenMax.y) / 2f;
                size[0] = Mathf.Abs(screenMax.x - screenMin.x);
                size[1] = Mathf.Abs(screenMax.y - screenMin.y);
            }

            if (graphic != null || selectable != null || elementType != "Unknown")
            {
                var uiElement = new UIElement
                {
                    Name = go.name,
                    Type = elementType,
                    Path = path,
                    ScreenPosition = screenPos,
                    Size = size,
                    Visible = visible,
                    Interactable = interactable && visible,
                    Source = "uGUI",
                    Properties = GetUGUIProperties(go)
                };

                elements.Add(uiElement);
            }

            foreach (Transform child in transform)
            {
                TraverseUGUIHierarchy(child, path, elements, rootCanvas);
            }
        }

        private string GetUGUIType(GameObject go)
        {
            if (go.GetComponent<UnityEngine.UI.Button>() != null) return "Button";
            if (go.GetComponent<InputField>() != null) return "TextField";
            if (go.GetComponent<UnityEngine.UI.Toggle>() != null) return "Toggle";
            if (go.GetComponent<UnityEngine.UI.Dropdown>() != null) return "Dropdown";
            if (go.GetComponent<UnityEngine.UI.Slider>() != null) return "Slider";
            if (go.GetComponent<ScrollRect>() != null) return "ScrollView";
            if (go.GetComponent<Text>() != null) return "Label";
            if (go.GetComponent<UnityEngine.UI.Image>() != null) return "Image";
            if (go.GetComponent<RawImage>() != null) return "Image";
            return "Unknown";
        }

        private bool IsUGUIInteractable(GameObject go, CanvasGroup canvasGroup)
        {
            if (canvasGroup != null && !canvasGroup.interactable) return false;

            var selectable = go.GetComponent<Selectable>();
            if (selectable != null) return selectable.IsInteractable();

            var graphic = go.GetComponent<Graphic>();
            return graphic != null && graphic.raycastTarget;
        }

        private Dictionary<string, object> GetUGUIProperties(GameObject go)
        {
            var props = new Dictionary<string, object>();

            var button = go.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
            {
                var text = go.GetComponentInChildren<Text>();
                props["text"] = text?.text ?? "";
            }

            var inputField = go.GetComponent<InputField>();
            if (inputField != null)
            {
                props["text"] = inputField.text;
                props["placeholder"] = inputField.placeholder?.GetComponent<Text>()?.text ?? "";
                props["isPassword"] = inputField.contentType == InputField.ContentType.Password;
            }

            var textComp = go.GetComponent<Text>();
            if (textComp != null) props["text"] = textComp.text;

            var toggle = go.GetComponent<UnityEngine.UI.Toggle>();
            if (toggle != null) props["value"] = toggle.isOn;

            var dropdown = go.GetComponent<UnityEngine.UI.Dropdown>();
            if (dropdown != null)
            {
                props["value"] = dropdown.captionText?.text ?? "";
                props["choices"] = dropdown.options?.Select(o => o.text).ToArray();
            }

            var slider = go.GetComponent<UnityEngine.UI.Slider>();
            if (slider != null)
            {
                props["value"] = slider.value;
                props["min"] = slider.minValue;
                props["max"] = slider.maxValue;
            }

            return props;
        }

        #endregion

        #region Occlusion

        private void DetectOcclusion(List<UIElement> elements)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            foreach (var element in elements.Where(e => e.Visible && e.Interactable))
            {
                var pointerData = new PointerEventData(eventSystem)
                {
                    position = new Vector2(element.ScreenPosition[0], element.ScreenPosition[1])
                };

                var raycastResults = new List<RaycastResult>();
                eventSystem.RaycastAll(pointerData, raycastResults);

                foreach (var result in raycastResults)
                {
                    if (result.gameObject == null) continue;
                    if (result.gameObject.name == element.Name) continue;

                    // Something else was hit first - this element is blocked
                    element.BlockedBy = result.gameObject.name;
                    element.Interactable = false;
                    break;
                }
            }
        }

        #endregion
    }
}
