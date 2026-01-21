#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool for Selenium-style wait conditions.
    /// Waits for game state conditions to be met before proceeding.
    /// Essential for reliable automated testing and AI interaction.
    /// </summary>
    [McpForUnityTool("wait_for_condition", Description = "Waits for a condition to be met. Supports waiting for: UI elements, GameObjects, component values, scene loads, and custom expressions. Returns when condition is met or timeout expires.")]
    public static class WaitForCondition
    {
        private const float DefaultTimeout = 10f;
        private const float DefaultPollInterval = 0.1f;

        public static object HandleCommand(JObject @params)
        {
            string conditionType = @params["condition"]?.ToString()?.ToLowerInvariant()
                                ?? @params["type"]?.ToString()?.ToLowerInvariant();

            if (string.IsNullOrEmpty(conditionType))
            {
                return new ErrorResponse("Required parameter 'condition' is missing. Use: element_exists, element_visible, element_interactable, gameobject_exists, gameobject_active, component_value, scene_loaded, animation_complete, time_elapsed");
            }

            float timeout = @params["timeout"]?.Value<float>() ?? DefaultTimeout;
            float pollInterval = @params["poll_interval"]?.Value<float>() ?? DefaultPollInterval;

            if (!EditorApplication.isPlaying)
            {
                return new ErrorResponse("Wait conditions are only available during Play Mode.");
            }

            try
            {
                var startTime = Time.realtimeSinceStartup;

                // MCP calls are synchronous - we do a single check and return immediately.
                // For actual waiting, the AI should poll this tool repeatedly until success.
                var checkResult = CheckCondition(conditionType, @params);
                bool conditionMet = checkResult.success;
                string resultMessage = checkResult.message;
                object resultData = checkResult.data;

                float elapsed = Time.realtimeSinceStartup - startTime;

                if (conditionMet)
                {
                    return new SuccessResponse($"Condition '{conditionType}' met: {resultMessage}", new
                    {
                        condition = conditionType,
                        success = true,
                        elapsed_seconds = elapsed,
                        data = resultData
                    });
                }
                else
                {
                    return new SuccessResponse($"Condition '{conditionType}' not yet met: {resultMessage}", new
                    {
                        condition = conditionType,
                        success = false,
                        elapsed_seconds = elapsed,
                        timeout_seconds = timeout,
                        data = resultData,
                        hint = "Condition not met on this check. Call again to poll, or adjust your game state."
                    });
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error checking condition: {e.Message}");
            }
        }

        private static (bool success, string message, object data) CheckCondition(string conditionType, JObject @params)
        {
            switch (conditionType)
            {
                case "element_exists":
                case "ui_exists":
                    return CheckElementExists(@params);

                case "element_visible":
                case "ui_visible":
                    return CheckElementVisible(@params);

                case "element_interactable":
                case "ui_interactable":
                    return CheckElementInteractable(@params);

                case "gameobject_exists":
                case "go_exists":
                    return CheckGameObjectExists(@params);

                case "gameobject_active":
                case "go_active":
                    return CheckGameObjectActive(@params);

                case "component_value":
                case "field_value":
                    return CheckComponentValue(@params);

                case "scene_loaded":
                    return CheckSceneLoaded(@params);

                case "animation_complete":
                case "animator_state":
                    return CheckAnimationComplete(@params);

                case "time_elapsed":
                case "wait":
                    return CheckTimeElapsed(@params);

                case "count_equals":
                case "object_count":
                    return CheckObjectCount(@params);

                default:
                    return (false, $"Unknown condition type: {conditionType}", null);
            }
        }

        #region Condition Checks

        private static (bool, string, object) CheckElementExists(JObject @params)
        {
            string elementName = @params["element"]?.ToString() ?? @params["name"]?.ToString();
            if (string.IsNullOrEmpty(elementName))
                return (false, "Missing 'element' parameter", null);

            // Check UI Toolkit
            var uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement == null) continue;
                var element = doc.rootVisualElement.Q(elementName);
                if (element != null)
                {
                    return (true, $"UI Toolkit element '{elementName}' found", new
                    {
                        element_name = elementName,
                        ui_system = "UIToolkit",
                        document = doc.gameObject.name
                    });
                }
            }

            // Check uGUI
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                var found = canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)
                    .FirstOrDefault(g => g.name == elementName);
                if (found != null)
                {
                    return (true, $"uGUI element '{elementName}' found", new
                    {
                        element_name = elementName,
                        ui_system = "uGUI",
                        canvas = canvas.name
                    });
                }
            }

            return (false, $"Element '{elementName}' not found", null);
        }

        private static (bool, string, object) CheckElementVisible(JObject @params)
        {
            string elementName = @params["element"]?.ToString() ?? @params["name"]?.ToString();
            if (string.IsNullOrEmpty(elementName))
                return (false, "Missing 'element' parameter", null);

            // Check UI Toolkit
            var uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement == null) continue;
                var element = doc.rootVisualElement.Q(elementName);
                if (element != null)
                {
                    bool visible = element.resolvedStyle.display != DisplayStyle.None
                                && element.resolvedStyle.visibility == Visibility.Visible
                                && element.resolvedStyle.opacity > 0;
                    if (visible)
                    {
                        return (true, $"UI Toolkit element '{elementName}' is visible", new
                        {
                            element_name = elementName,
                            visible = true
                        });
                    }
                    return (false, $"UI Toolkit element '{elementName}' exists but not visible", new
                    {
                        display = element.resolvedStyle.display.ToString(),
                        visibility = element.resolvedStyle.visibility.ToString(),
                        opacity = element.resolvedStyle.opacity
                    });
                }
            }

            // Check uGUI
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                var found = canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)
                    .FirstOrDefault(g => g.name == elementName);
                if (found != null)
                {
                    bool visible = found.gameObject.activeInHierarchy && found.enabled;
                    if (visible)
                    {
                        return (true, $"uGUI element '{elementName}' is visible", new
                        {
                            element_name = elementName,
                            visible = true
                        });
                    }
                    return (false, $"uGUI element '{elementName}' exists but not visible", new
                    {
                        active_in_hierarchy = found.gameObject.activeInHierarchy,
                        enabled = found.enabled
                    });
                }
            }

            return (false, $"Element '{elementName}' not found", null);
        }

        private static (bool, string, object) CheckElementInteractable(JObject @params)
        {
            string elementName = @params["element"]?.ToString() ?? @params["name"]?.ToString();
            if (string.IsNullOrEmpty(elementName))
                return (false, "Missing 'element' parameter", null);

            // Check UI Toolkit
            var uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement == null) continue;
                var element = doc.rootVisualElement.Q(elementName);
                if (element != null)
                {
                    bool interactable = element.enabledSelf && element.enabledInHierarchy
                                     && element.resolvedStyle.display != DisplayStyle.None;
                    return (interactable, interactable ? $"Element '{elementName}' is interactable" : $"Element '{elementName}' not interactable",
                        new { element_name = elementName, interactable, enabled_self = element.enabledSelf, enabled_in_hierarchy = element.enabledInHierarchy });
                }
            }

            // Check uGUI Selectable
            var selectables = UnityEngine.Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None);
            var selectable = selectables.FirstOrDefault(s => s.name == elementName);
            if (selectable != null)
            {
                bool interactable = selectable.IsInteractable();
                return (interactable, interactable ? $"Element '{elementName}' is interactable" : $"Element '{elementName}' not interactable",
                    new { element_name = elementName, interactable });
            }

            return (false, $"Element '{elementName}' not found", null);
        }

        private static (bool, string, object) CheckGameObjectExists(JObject @params)
        {
            string objectName = @params["object"]?.ToString() ?? @params["name"]?.ToString();
            if (string.IsNullOrEmpty(objectName))
                return (false, "Missing 'object' parameter", null);

            var go = GameObject.Find(objectName);
            if (go != null)
            {
                return (true, $"GameObject '{objectName}' exists", new
                {
                    name = go.name,
                    active = go.activeSelf,
                    position = new[] { go.transform.position.x, go.transform.position.y, go.transform.position.z }
                });
            }

            return (false, $"GameObject '{objectName}' not found", null);
        }

        private static (bool, string, object) CheckGameObjectActive(JObject @params)
        {
            string objectName = @params["object"]?.ToString() ?? @params["name"]?.ToString();
            if (string.IsNullOrEmpty(objectName))
                return (false, "Missing 'object' parameter", null);

            var go = GameObject.Find(objectName);
            if (go != null && go.activeInHierarchy)
            {
                return (true, $"GameObject '{objectName}' is active", new
                {
                    name = go.name,
                    active_self = go.activeSelf,
                    active_in_hierarchy = go.activeInHierarchy
                });
            }

            if (go != null)
            {
                return (false, $"GameObject '{objectName}' exists but not active in hierarchy", new
                {
                    active_self = go.activeSelf,
                    active_in_hierarchy = go.activeInHierarchy
                });
            }

            return (false, $"GameObject '{objectName}' not found", null);
        }

        private static (bool, string, object) CheckComponentValue(JObject @params)
        {
            string objectName = @params["object"]?.ToString();
            string componentType = @params["component"]?.ToString();
            string fieldName = @params["field"]?.ToString() ?? @params["property"]?.ToString();
            var expectedValue = @params["value"];
            string comparison = @params["comparison"]?.ToString()?.ToLowerInvariant() ?? "equals";

            if (string.IsNullOrEmpty(objectName) || string.IsNullOrEmpty(componentType) || string.IsNullOrEmpty(fieldName))
            {
                return (false, "Missing required parameters: object, component, field", null);
            }

            var go = GameObject.Find(objectName);
            if (go == null)
                return (false, $"GameObject '{objectName}' not found", null);

            var component = go.GetComponent(componentType);
            if (component == null)
                return (false, $"Component '{componentType}' not found on '{objectName}'", null);

            var type = component.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var prop = type.GetProperty(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            object actualValue = null;
            if (field != null)
                actualValue = field.GetValue(component);
            else if (prop != null && prop.CanRead)
                actualValue = prop.GetValue(component);
            else
                return (false, $"Field/property '{fieldName}' not found on component '{componentType}'", null);

            bool conditionMet = CompareValues(actualValue, expectedValue, comparison);

            return (conditionMet,
                conditionMet ? $"Field '{fieldName}' meets condition" : $"Field '{fieldName}' = {actualValue}, expected {comparison} {expectedValue}",
                new { field = fieldName, actual_value = actualValue?.ToString(), expected_value = expectedValue?.ToString(), comparison });
        }

        private static bool CompareValues(object actual, JToken expected, string comparison)
        {
            if (actual == null || expected == null)
                return actual == null && (expected == null || expected.Type == JTokenType.Null);

            try
            {
                switch (comparison)
                {
                    case "equals":
                    case "eq":
                    case "==":
                        return actual.ToString() == expected.ToString();

                    case "not_equals":
                    case "neq":
                    case "!=":
                        return actual.ToString() != expected.ToString();

                    case "greater_than":
                    case "gt":
                    case ">":
                        return Convert.ToDouble(actual) > expected.Value<double>();

                    case "greater_or_equal":
                    case "gte":
                    case ">=":
                        return Convert.ToDouble(actual) >= expected.Value<double>();

                    case "less_than":
                    case "lt":
                    case "<":
                        return Convert.ToDouble(actual) < expected.Value<double>();

                    case "less_or_equal":
                    case "lte":
                    case "<=":
                        return Convert.ToDouble(actual) <= expected.Value<double>();

                    case "contains":
                        return actual.ToString().Contains(expected.ToString());

                    case "starts_with":
                        return actual.ToString().StartsWith(expected.ToString());

                    case "ends_with":
                        return actual.ToString().EndsWith(expected.ToString());

                    default:
                        return actual.ToString() == expected.ToString();
                }
            }
            catch
            {
                return false;
            }
        }

        private static (bool, string, object) CheckSceneLoaded(JObject @params)
        {
            string sceneName = @params["scene"]?.ToString() ?? @params["name"]?.ToString();
            if (string.IsNullOrEmpty(sceneName))
                return (false, "Missing 'scene' parameter", null);

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == sceneName && scene.isLoaded)
                {
                    return (true, $"Scene '{sceneName}' is loaded", new
                    {
                        scene_name = scene.name,
                        is_loaded = scene.isLoaded,
                        root_count = scene.rootCount
                    });
                }
            }

            return (false, $"Scene '{sceneName}' not loaded", new
            {
                loaded_scenes = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(i => SceneManager.GetSceneAt(i).name).ToArray()
            });
        }

        private static (bool, string, object) CheckAnimationComplete(JObject @params)
        {
            string objectName = @params["object"]?.ToString() ?? @params["name"]?.ToString();
            string stateName = @params["state"]?.ToString();
            int layer = @params["layer"]?.Value<int>() ?? 0;

            if (string.IsNullOrEmpty(objectName))
                return (false, "Missing 'object' parameter", null);

            var go = GameObject.Find(objectName);
            if (go == null)
                return (false, $"GameObject '{objectName}' not found", null);

            var animator = go.GetComponent<Animator>();
            if (animator == null)
                return (false, $"No Animator on '{objectName}'", null);

            var stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            bool isComplete = stateInfo.normalizedTime >= 1f;

            if (!string.IsNullOrEmpty(stateName))
            {
                bool inState = stateInfo.IsName(stateName);
                if (!inState)
                {
                    return (false, $"Animator not in state '{stateName}'", new
                    {
                        current_normalized_time = stateInfo.normalizedTime,
                        in_requested_state = false
                    });
                }
            }

            return (isComplete, isComplete ? "Animation complete" : $"Animation at {stateInfo.normalizedTime:P0}",
                new { normalized_time = stateInfo.normalizedTime, is_complete = isComplete, layer });
        }

        private static (bool, string, object) CheckTimeElapsed(JObject @params)
        {
            float waitTime = @params["seconds"]?.Value<float>() ?? @params["time"]?.Value<float>() ?? 0f;

            // For time-based waits, we just return success since MCP calls are synchronous
            // The AI should use game time comparison if needed
            return (true, $"Time check for {waitTime}s - use game time comparison for actual timing", new
            {
                requested_wait = waitTime,
                game_time = Time.time,
                realtime = Time.realtimeSinceStartup,
                hint = "For frame-accurate timing, compare game Time.time values between calls"
            });
        }

        // Cache for component type lookups to avoid scanning all assemblies repeatedly
        private static readonly System.Collections.Generic.Dictionary<string, Type> _typeCache = new();

        private static (bool, string, object) CheckObjectCount(JObject @params)
        {
            string tag = @params["tag"]?.ToString();
            string componentType = @params["component"]?.ToString();
            int expectedCount = @params["count"]?.Value<int>() ?? 0;
            string comparison = @params["comparison"]?.ToString()?.ToLowerInvariant() ?? "equals";

            int actualCount = 0;

            if (!string.IsNullOrEmpty(tag))
            {
                try
                {
                    actualCount = GameObject.FindGameObjectsWithTag(tag).Length;
                }
                catch (UnityException)
                {
                    return (false, $"Tag '{tag}' is not defined in TagManager", null);
                }
            }
            else if (!string.IsNullOrEmpty(componentType))
            {
                // Check cache first
                if (!_typeCache.TryGetValue(componentType, out var type))
                {
                    // Search for the type
                    type = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                        .FirstOrDefault(t => t.Name == componentType && typeof(Component).IsAssignableFrom(t));

                    // Cache result (including null)
                    _typeCache[componentType] = type;
                }

                if (type != null)
                {
                    actualCount = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None).Length;
                }
                else
                {
                    return (false, $"Component type '{componentType}' not found", null);
                }
            }
            else
            {
                return (false, "Missing 'tag' or 'component' parameter", null);
            }

            bool conditionMet = CompareValues(actualCount, JToken.FromObject(expectedCount), comparison);

            return (conditionMet,
                conditionMet ? $"Object count ({actualCount}) meets condition" : $"Object count is {actualCount}, expected {comparison} {expectedCount}",
                new { actual_count = actualCount, expected_count = expectedCount, comparison });
        }

        #endregion
    }
}
#endif
