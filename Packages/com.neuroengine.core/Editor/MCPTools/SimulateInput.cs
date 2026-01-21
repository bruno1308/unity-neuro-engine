#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to simulate player input (keyboard, mouse).
    /// Enables AI to "play" the game by injecting inputs.
    /// </summary>
    [McpForUnityTool("simulate_input", Description = "Simulates player input (keyboard, mouse). Use during Play Mode to control the game. Supports key presses, mouse clicks, and mouse movement.")]
    public static class SimulateInput
    {
        // Cache the service reference for held key queries
        private static IInputSimulation _inputService;

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();

            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse("Required parameter 'action' is missing. Use: press_key, key_down, key_up, mouse_click, mouse_move, release_all");
            }

            if (!EditorApplication.isPlaying)
            {
                return new ErrorResponse("Cannot simulate input outside of Play Mode. Use manage_editor(action='play') first.");
            }

            _inputService = EditorServiceLocator.Get<IInputSimulation>();

            try
            {
                switch (action)
                {
                    case "press_key":
                        return HandlePressKey(@params);

                    case "key_down":
                        return HandleKeyDown(@params);

                    case "key_up":
                        return HandleKeyUp(@params);

                    case "mouse_click":
                        return HandleMouseClick(@params);

                    case "mouse_move":
                        return HandleMouseMove(@params);

                    case "release_all":
                        return HandleReleaseAll();

                    case "get_held_keys":
                        return HandleGetHeldKeys();

                    default:
                        return new ErrorResponse($"Unknown action '{action}'. Use: press_key, key_down, key_up, mouse_click, mouse_move, release_all, get_held_keys");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error simulating input: {e.Message}");
            }
        }

        private static object HandlePressKey(JObject @params)
        {
            string keyName = @params["key"]?.ToString();
            if (string.IsNullOrEmpty(keyName))
            {
                return new ErrorResponse("Required parameter 'key' is missing. Use Unity KeyCode names (e.g., 'Space', 'W', 'LeftArrow').");
            }

            if (!TryParseKeyCode(keyName, out KeyCode keyCode))
            {
                return new ErrorResponse($"Unknown key '{keyName}'. Use Unity KeyCode names.", new
                {
                    common_keys = GetCommonKeyNames()
                });
            }

            _inputService.PressKey(keyCode);

            return new SuccessResponse($"Pressed key '{keyCode}'", new
            {
                action = "press_key",
                key = keyCode.ToString(),
                held_keys = GetHeldKeyNames()
            });
        }

        private static object HandleKeyDown(JObject @params)
        {
            string keyName = @params["key"]?.ToString();
            if (string.IsNullOrEmpty(keyName))
            {
                return new ErrorResponse("Required parameter 'key' is missing.");
            }

            if (!TryParseKeyCode(keyName, out KeyCode keyCode))
            {
                return new ErrorResponse($"Unknown key '{keyName}'.");
            }

            _inputService.KeyDown(keyCode);

            return new SuccessResponse($"Key down '{keyCode}'", new
            {
                action = "key_down",
                key = keyCode.ToString(),
                held_keys = GetHeldKeyNames()
            });
        }

        private static object HandleKeyUp(JObject @params)
        {
            string keyName = @params["key"]?.ToString();
            if (string.IsNullOrEmpty(keyName))
            {
                return new ErrorResponse("Required parameter 'key' is missing.");
            }

            if (!TryParseKeyCode(keyName, out KeyCode keyCode))
            {
                return new ErrorResponse($"Unknown key '{keyName}'.");
            }

            _inputService.KeyUp(keyCode);

            return new SuccessResponse($"Key up '{keyCode}'", new
            {
                action = "key_up",
                key = keyCode.ToString(),
                held_keys = GetHeldKeyNames()
            });
        }

        private static object HandleMouseClick(JObject @params)
        {
            float x = @params["x"]?.Value<float>() ?? Screen.width / 2f;
            float y = @params["y"]?.Value<float>() ?? Screen.height / 2f;
            int button = @params["button"]?.Value<int>() ?? 0;

            // Also support named positions
            string position = @params["position"]?.ToString();
            if (!string.IsNullOrEmpty(position))
            {
                var pos = ParseNamedPosition(position);
                x = pos.x;
                y = pos.y;
            }

            var screenPos = new Vector2(x, y);
            _inputService.MouseClick(screenPos, button);

            // Check what was clicked
            string hitObject = GetClickTarget(screenPos);

            return new SuccessResponse($"Mouse click at ({x}, {y})", new
            {
                action = "mouse_click",
                x = x,
                y = y,
                button = button,
                button_name = button == 0 ? "left" : button == 1 ? "right" : "middle",
                hit_object = hitObject
            });
        }

        private static object HandleMouseMove(JObject @params)
        {
            float x = @params["x"]?.Value<float>() ?? Screen.width / 2f;
            float y = @params["y"]?.Value<float>() ?? Screen.height / 2f;

            var screenPos = new Vector2(x, y);
            _inputService.MouseMove(screenPos);

            return new SuccessResponse($"Mouse moved to ({x}, {y})", new
            {
                action = "mouse_move",
                x = x,
                y = y
            });
        }

        private static object HandleReleaseAll()
        {
            var previouslyHeld = GetHeldKeyNames();
            _inputService.ReleaseAll();

            return new SuccessResponse("All keys released", new
            {
                action = "release_all",
                released_keys = previouslyHeld
            });
        }

        private static object HandleGetHeldKeys()
        {
            return new SuccessResponse("Current held keys", new
            {
                held_keys = GetHeldKeyNames(),
                mouse_position = new { x = _inputService.GetMousePosition().x, y = _inputService.GetMousePosition().y }
            });
        }

        private static bool TryParseKeyCode(string name, out KeyCode keyCode)
        {
            // Handle common aliases
            name = name.ToLowerInvariant() switch
            {
                "space" => "Space",
                "enter" => "Return",
                "return" => "Return",
                "esc" => "Escape",
                "escape" => "Escape",
                "up" => "UpArrow",
                "down" => "DownArrow",
                "left" => "LeftArrow",
                "right" => "RightArrow",
                "lshift" => "LeftShift",
                "rshift" => "RightShift",
                "lctrl" => "LeftControl",
                "rctrl" => "RightControl",
                "lalt" => "LeftAlt",
                "ralt" => "RightAlt",
                "tab" => "Tab",
                "backspace" => "Backspace",
                "delete" => "Delete",
                _ => name
            };

            return Enum.TryParse(name, true, out keyCode);
        }

        private static List<string> GetHeldKeyNames()
        {
            var held = _inputService?.GetHeldKeys();
            if (held == null) return new List<string>();

            var names = new List<string>();
            foreach (var key in held)
            {
                names.Add(key.ToString());
            }
            return names;
        }

        private static List<string> GetCommonKeyNames()
        {
            return new List<string>
            {
                "Space", "Return", "Escape", "Tab",
                "W", "A", "S", "D", "E", "Q", "R", "F",
                "UpArrow", "DownArrow", "LeftArrow", "RightArrow",
                "LeftShift", "LeftControl", "LeftAlt",
                "Mouse0", "Mouse1", "Mouse2",
                "Alpha0-9", "F1-F12"
            };
        }

        private static Vector2 ParseNamedPosition(string position)
        {
            return position.ToLowerInvariant() switch
            {
                "center" => new Vector2(Screen.width / 2f, Screen.height / 2f),
                "top" => new Vector2(Screen.width / 2f, Screen.height * 0.9f),
                "bottom" => new Vector2(Screen.width / 2f, Screen.height * 0.1f),
                "left" => new Vector2(Screen.width * 0.1f, Screen.height / 2f),
                "right" => new Vector2(Screen.width * 0.9f, Screen.height / 2f),
                "topleft" => new Vector2(Screen.width * 0.1f, Screen.height * 0.9f),
                "topright" => new Vector2(Screen.width * 0.9f, Screen.height * 0.9f),
                "bottomleft" => new Vector2(Screen.width * 0.1f, Screen.height * 0.1f),
                "bottomright" => new Vector2(Screen.width * 0.9f, Screen.height * 0.1f),
                _ => new Vector2(Screen.width / 2f, Screen.height / 2f)
            };
        }

        private static string GetClickTarget(Vector2 screenPos)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return null;

            var pointerData = new PointerEventData(eventSystem) { position = screenPos };
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, results);

            return results.Count > 0 ? results[0].gameObject.name : null;
        }
    }
}
#endif
