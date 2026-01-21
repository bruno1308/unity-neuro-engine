#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NeuroEngine.Editor
{
    /// <summary>
    /// Detects which Input System the Unity project is configured to use.
    /// Solves Problem #8: Code generated using legacy UnityEngine.Input when
    /// the project has Input System Package enabled causes runtime errors.
    ///
    /// This is a Layer 3 (Interaction Polecat) utility that helps ensure
    /// generated code uses the correct input API.
    /// </summary>
    public static class InputSystemDetector
    {
        /// <summary>
        /// The input handling mode configured in Player Settings.
        /// </summary>
        public enum InputMode
        {
            /// <summary>Legacy Input Manager (UnityEngine.Input)</summary>
            Legacy = 0,

            /// <summary>New Input System Package (UnityEngine.InputSystem)</summary>
            InputSystem = 1,

            /// <summary>Both systems active simultaneously</summary>
            Both = 2,

            /// <summary>Could not determine (error state)</summary>
            Unknown = -1
        }

        /// <summary>
        /// Result of scanning a script for input API usage.
        /// </summary>
        public class InputUsageScanResult
        {
            public string ScriptPath;
            public bool UsesLegacyInput;
            public bool UsesNewInputSystem;
            public List<InputUsageLocation> LegacyUsages = new List<InputUsageLocation>();
            public List<InputUsageLocation> NewSystemUsages = new List<InputUsageLocation>();
            public List<string> Warnings = new List<string>();
        }

        /// <summary>
        /// Location of an input API usage in code.
        /// </summary>
        public class InputUsageLocation
        {
            public int LineNumber;
            public string LineContent;
            public string ApiCall;
        }

        /// <summary>
        /// Report summarizing input system configuration and code compatibility.
        /// </summary>
        public class InputSystemReport
        {
            public InputMode ProjectMode;
            public string ModeDescription;
            public string RecommendedApi;
            public List<string> CodeGuidance = new List<string>();
            public List<InputUsageScanResult> IncompatibleScripts = new List<InputUsageScanResult>();
            public bool HasIssues => IncompatibleScripts.Count > 0;
        }

        // Regex patterns for detecting input API usage
        private static readonly Regex LegacyInputPattern = new Regex(
            @"\bInput\s*\.\s*(GetKey|GetKeyDown|GetKeyUp|GetButton|GetButtonDown|GetButtonUp|GetAxis|GetAxisRaw|GetMouseButton|GetMouseButtonDown|GetMouseButtonUp|mousePosition|mouseScrollDelta|GetTouch|touchCount|touches|anyKey|anyKeyDown)\b",
            RegexOptions.Compiled);

        private static readonly Regex NewInputSystemPattern = new Regex(
            @"\b(Keyboard|Mouse|Gamepad|Touchscreen|Pointer|Pen|InputAction|InputActionAsset|InputActionMap|InputActionReference|PlayerInput)\s*\.\s*(current|all|wasPressedThisFrame|wasReleasedThisFrame|isPressed|ReadValue|performed|started|canceled|Enable|Disable)\b|\bInputSystem\s*\.|\.ReadValue\s*<|InputAction\s+\w+|action\s*\.\s*ReadValue",
            RegexOptions.Compiled);

        private static readonly Regex InputSystemUsingPattern = new Regex(
            @"using\s+UnityEngine\.InputSystem\s*;",
            RegexOptions.Compiled);

        /// <summary>
        /// Gets the input handling mode configured in Player Settings.
        /// </summary>
        /// <returns>The current InputMode.</returns>
        public static InputMode GetProjectInputMode()
        {
            try
            {
                // PlayerSettings.activeInputHandler is internal, access via reflection
                // Values: 0 = Input Manager (Old), 1 = Input System Package (New), 2 = Both
                var playerSettingsType = typeof(PlayerSettings);
                var property = playerSettingsType.GetProperty(
                    "activeInputHandler",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                if (property != null)
                {
                    var value = property.GetValue(null);
                    if (value != null)
                    {
                        int intValue = Convert.ToInt32(value);
                        return intValue switch
                        {
                            0 => InputMode.Legacy,
                            1 => InputMode.InputSystem,
                            2 => InputMode.Both,
                            _ => InputMode.Unknown
                        };
                    }
                }

                // Fallback: Check if Input System package is installed
                if (IsInputSystemPackageInstalled())
                {
                    // Package is installed but we couldn't read the setting
                    // Default to assuming Both for safety
                    Debug.LogWarning("[InputSystemDetector] Could not read activeInputHandler, " +
                        "but Input System package is installed. Assuming Both mode.");
                    return InputMode.Both;
                }

                return InputMode.Legacy;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InputSystemDetector] Error detecting input mode: {e.Message}");
                return InputMode.Unknown;
            }
        }

        /// <summary>
        /// Checks if the Input System package is installed in the project.
        /// </summary>
        public static bool IsInputSystemPackageInstalled()
        {
            // Check if the InputSystem namespace exists
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.StartsWith("Unity.InputSystem,"))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets human-readable description of the current input mode.
        /// </summary>
        public static string GetInputModeDescription(InputMode mode)
        {
            return mode switch
            {
                InputMode.Legacy => "Legacy Input Manager (UnityEngine.Input)",
                InputMode.InputSystem => "New Input System Package (UnityEngine.InputSystem)",
                InputMode.Both => "Both Input Systems Active",
                InputMode.Unknown => "Unknown Configuration",
                _ => "Invalid"
            };
        }

        /// <summary>
        /// Gets the recommended input code API based on project configuration.
        /// </summary>
        public static string GetRecommendedInputCode()
        {
            var mode = GetProjectInputMode();
            return GetRecommendedInputCode(mode);
        }

        /// <summary>
        /// Gets the recommended input code API for a specific mode.
        /// </summary>
        public static string GetRecommendedInputCode(InputMode mode)
        {
            return mode switch
            {
                InputMode.Legacy => "Use UnityEngine.Input (e.g., Input.GetMouseButtonDown(0), Input.GetAxis(\"Horizontal\"))",
                InputMode.InputSystem => "Use UnityEngine.InputSystem (e.g., Mouse.current.leftButton.wasPressedThisFrame, Keyboard.current.spaceKey.wasPressedThisFrame)",
                InputMode.Both => "Either API works. Prefer InputSystem for new code as Legacy is deprecated.",
                InputMode.Unknown => "Unable to determine. Check Player Settings > Active Input Handling.",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Gets detailed code guidance for the current project configuration.
        /// </summary>
        public static List<string> GetCodeGuidance()
        {
            var mode = GetProjectInputMode();
            return GetCodeGuidance(mode);
        }

        /// <summary>
        /// Gets detailed code guidance for a specific input mode.
        /// </summary>
        public static List<string> GetCodeGuidance(InputMode mode)
        {
            var guidance = new List<string>();

            switch (mode)
            {
                case InputMode.Legacy:
                    guidance.Add("Project uses Legacy Input Manager only.");
                    guidance.Add("REQUIRED: Use UnityEngine.Input API");
                    guidance.Add("Examples:");
                    guidance.Add("  - Input.GetMouseButtonDown(0) for left click");
                    guidance.Add("  - Input.GetKeyDown(KeyCode.Space) for space key");
                    guidance.Add("  - Input.GetAxis(\"Horizontal\") for movement");
                    guidance.Add("  - Input.mousePosition for cursor position");
                    guidance.Add("WARNING: Do NOT use UnityEngine.InputSystem - it will cause compile errors!");
                    break;

                case InputMode.InputSystem:
                    guidance.Add("Project uses New Input System Package only.");
                    guidance.Add("REQUIRED: Use UnityEngine.InputSystem API");
                    guidance.Add("Examples:");
                    guidance.Add("  - Mouse.current.leftButton.wasPressedThisFrame for left click");
                    guidance.Add("  - Keyboard.current.spaceKey.wasPressedThisFrame for space key");
                    guidance.Add("  - Gamepad.current?.leftStick.ReadValue() for gamepad input");
                    guidance.Add("  - Mouse.current.position.ReadValue() for cursor position");
                    guidance.Add("REQUIRED using statement: using UnityEngine.InputSystem;");
                    guidance.Add("WARNING: Do NOT use UnityEngine.Input - it will cause runtime errors!");
                    guidance.Add("NOTE: Always null-check devices (e.g., Mouse.current != null)");
                    break;

                case InputMode.Both:
                    guidance.Add("Project supports both input systems.");
                    guidance.Add("RECOMMENDED: Use UnityEngine.InputSystem for new code (future-proof)");
                    guidance.Add("ALLOWED: UnityEngine.Input still works but is deprecated");
                    guidance.Add("For new code, prefer:");
                    guidance.Add("  - Mouse.current.leftButton.wasPressedThisFrame over Input.GetMouseButtonDown(0)");
                    guidance.Add("  - Keyboard.current.spaceKey.wasPressedThisFrame over Input.GetKeyDown(KeyCode.Space)");
                    guidance.Add("REQUIRED using for new system: using UnityEngine.InputSystem;");
                    break;

                case InputMode.Unknown:
                    guidance.Add("Could not determine input configuration.");
                    guidance.Add("Check: Edit > Project Settings > Player > Other Settings > Active Input Handling");
                    guidance.Add("If using Input System Package, ensure it's properly installed via Package Manager");
                    break;
            }

            return guidance;
        }

        /// <summary>
        /// Scans a C# script file for input API usage and checks compatibility.
        /// </summary>
        /// <param name="scriptPath">Path to the C# script file.</param>
        /// <returns>Scan result with usage locations and warnings.</returns>
        public static InputUsageScanResult ScanScript(string scriptPath)
        {
            var result = new InputUsageScanResult { ScriptPath = scriptPath };

            if (!File.Exists(scriptPath))
            {
                result.Warnings.Add($"Script not found: {scriptPath}");
                return result;
            }

            try
            {
                var lines = File.ReadAllLines(scriptPath);
                var mode = GetProjectInputMode();
                bool hasInputSystemUsing = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    int lineNumber = i + 1;

                    // Check for InputSystem using statement
                    if (InputSystemUsingPattern.IsMatch(line))
                    {
                        hasInputSystemUsing = true;
                    }

                    // Check for legacy Input usage
                    var legacyMatches = LegacyInputPattern.Matches(line);
                    foreach (Match match in legacyMatches)
                    {
                        result.UsesLegacyInput = true;
                        result.LegacyUsages.Add(new InputUsageLocation
                        {
                            LineNumber = lineNumber,
                            LineContent = line.Trim(),
                            ApiCall = match.Value
                        });
                    }

                    // Check for new Input System usage
                    var newSystemMatches = NewInputSystemPattern.Matches(line);
                    foreach (Match match in newSystemMatches)
                    {
                        result.UsesNewInputSystem = true;
                        result.NewSystemUsages.Add(new InputUsageLocation
                        {
                            LineNumber = lineNumber,
                            LineContent = line.Trim(),
                            ApiCall = match.Value
                        });
                    }
                }

                // Generate warnings based on project mode
                switch (mode)
                {
                    case InputMode.Legacy:
                        if (result.UsesNewInputSystem)
                        {
                            result.Warnings.Add("INCOMPATIBLE: Script uses Input System API but project only has Legacy Input enabled.");
                            result.Warnings.Add("FIX: Change Player Settings > Active Input Handling to 'Both' or 'Input System Package'");
                            result.Warnings.Add("OR: Refactor code to use UnityEngine.Input API");
                        }
                        break;

                    case InputMode.InputSystem:
                        if (result.UsesLegacyInput)
                        {
                            result.Warnings.Add("INCOMPATIBLE: Script uses Legacy Input API but project only has Input System enabled.");
                            result.Warnings.Add("FIX: Refactor code to use UnityEngine.InputSystem API");
                            foreach (var usage in result.LegacyUsages)
                            {
                                var suggestion = GetInputApiMigrationSuggestion(usage.ApiCall);
                                if (!string.IsNullOrEmpty(suggestion))
                                {
                                    result.Warnings.Add($"  Line {usage.LineNumber}: Replace '{usage.ApiCall}' with '{suggestion}'");
                                }
                            }
                        }
                        if (result.UsesNewInputSystem && !hasInputSystemUsing)
                        {
                            result.Warnings.Add("MISSING: Add 'using UnityEngine.InputSystem;' to use Input System API");
                        }
                        break;

                    case InputMode.Both:
                        // Both APIs work, but warn about mixing
                        if (result.UsesLegacyInput && result.UsesNewInputSystem)
                        {
                            result.Warnings.Add("STYLE: Script mixes Legacy and New Input System APIs. Consider standardizing on one.");
                        }
                        if (result.UsesLegacyInput)
                        {
                            result.Warnings.Add("NOTE: Legacy Input API is deprecated. Consider migrating to InputSystem.");
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                result.Warnings.Add($"Error scanning script: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Scans all C# scripts in a directory for input API compatibility issues.
        /// </summary>
        /// <param name="directory">Directory to scan (defaults to Assets/Scripts).</param>
        /// <returns>List of scripts with compatibility issues.</returns>
        public static List<InputUsageScanResult> ScanDirectory(string directory = null)
        {
            if (string.IsNullOrEmpty(directory))
            {
                directory = Path.Combine(Application.dataPath, "Scripts");
            }

            var results = new List<InputUsageScanResult>();

            if (!Directory.Exists(directory))
            {
                Debug.LogWarning($"[InputSystemDetector] Directory not found: {directory}");
                return results;
            }

            var scripts = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            var mode = GetProjectInputMode();

            foreach (var script in scripts)
            {
                var scanResult = ScanScript(script);

                // Only include scripts with potential issues
                bool hasIssue = mode switch
                {
                    InputMode.Legacy => scanResult.UsesNewInputSystem,
                    InputMode.InputSystem => scanResult.UsesLegacyInput,
                    _ => false // Both mode - no hard incompatibilities
                };

                if (hasIssue || scanResult.Warnings.Count > 0)
                {
                    results.Add(scanResult);
                }
            }

            return results;
        }

        /// <summary>
        /// Generates a full report of input system configuration and code compatibility.
        /// </summary>
        public static InputSystemReport GenerateReport()
        {
            var mode = GetProjectInputMode();
            var report = new InputSystemReport
            {
                ProjectMode = mode,
                ModeDescription = GetInputModeDescription(mode),
                RecommendedApi = GetRecommendedInputCode(mode),
                CodeGuidance = GetCodeGuidance(mode)
            };

            // Scan for incompatible scripts
            var scriptsDir = Path.Combine(Application.dataPath, "Scripts");
            if (Directory.Exists(scriptsDir))
            {
                report.IncompatibleScripts = ScanDirectory(scriptsDir);
            }

            return report;
        }

        /// <summary>
        /// Gets a migration suggestion for converting legacy API to new Input System.
        /// </summary>
        private static string GetInputApiMigrationSuggestion(string legacyApi)
        {
            // Common migrations
            if (legacyApi.Contains("GetMouseButtonDown(0)") || (legacyApi.Contains("GetMouseButton") && legacyApi.Contains("0")))
                return "Mouse.current.leftButton.wasPressedThisFrame";
            if (legacyApi.Contains("GetMouseButtonDown(1)") || (legacyApi.Contains("GetMouseButton") && legacyApi.Contains("1")))
                return "Mouse.current.rightButton.wasPressedThisFrame";
            if (legacyApi.Contains("GetMouseButtonDown(2)") || (legacyApi.Contains("GetMouseButton") && legacyApi.Contains("2")))
                return "Mouse.current.middleButton.wasPressedThisFrame";
            if (legacyApi.Contains("mousePosition"))
                return "Mouse.current.position.ReadValue()";
            if (legacyApi.Contains("mouseScrollDelta"))
                return "Mouse.current.scroll.ReadValue()";
            if (legacyApi.Contains("GetKeyDown"))
                return "Keyboard.current.[key].wasPressedThisFrame";
            if (legacyApi.Contains("GetKey"))
                return "Keyboard.current.[key].isPressed";
            if (legacyApi.Contains("GetKeyUp"))
                return "Keyboard.current.[key].wasReleasedThisFrame";
            if (legacyApi.Contains("GetAxis"))
                return "Use InputAction with Vector2 or float binding";
            if (legacyApi.Contains("anyKey"))
                return "Keyboard.current.anyKey.isPressed";
            if (legacyApi.Contains("GetTouch") || legacyApi.Contains("touchCount"))
                return "Touchscreen.current.touches";

            return null;
        }

        /// <summary>
        /// Logs the current input system configuration to the console.
        /// Useful for debugging and validation.
        /// </summary>
        [MenuItem("Neuro-Engine/Debug/Log Input System Configuration")]
        public static void LogInputSystemConfiguration()
        {
            var mode = GetProjectInputMode();
            Debug.Log($"[InputSystemDetector] Project Input Mode: {GetInputModeDescription(mode)}");
            Debug.Log($"[InputSystemDetector] Input System Package Installed: {IsInputSystemPackageInstalled()}");
            Debug.Log($"[InputSystemDetector] Recommended API: {GetRecommendedInputCode(mode)}");

            var guidance = GetCodeGuidance(mode);
            foreach (var line in guidance)
            {
                Debug.Log($"[InputSystemDetector] {line}");
            }
        }
    }
}
#endif
