#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to check the project's Input System configuration.
    /// Helps prevent Problem #8: Generating code with wrong Input API.
    ///
    /// This is a Layer 3 (Interaction Polecat) tool that provides
    /// pre-generation validation for input-related code.
    /// </summary>
    [McpForUnityTool("check_input_system", Description = "Checks which Input System the project uses (Legacy, InputSystem, or Both) and provides guidance on which API to use. Can scan scripts for incompatible input API usage.")]
    public static class CheckInputSystem
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant() ?? "get_config";

            try
            {
                return action switch
                {
                    "get_config" => HandleGetConfig(),
                    "get_guidance" => HandleGetGuidance(),
                    "scan_script" => HandleScanScript(@params),
                    "scan_directory" => HandleScanDirectory(@params),
                    "full_report" => HandleFullReport(),
                    _ => new ErrorResponse($"Unknown action '{action}'. Use: get_config, get_guidance, scan_script, scan_directory, full_report")
                };
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error checking input system: {e.Message}");
            }
        }

        /// <summary>
        /// Gets the current input system configuration.
        /// </summary>
        private static object HandleGetConfig()
        {
            var mode = InputSystemDetector.GetProjectInputMode();
            var isPackageInstalled = InputSystemDetector.IsInputSystemPackageInstalled();

            return new SuccessResponse($"Project uses: {InputSystemDetector.GetInputModeDescription(mode)}", new
            {
                mode = mode.ToString(),
                mode_value = (int)mode,
                description = InputSystemDetector.GetInputModeDescription(mode),
                input_system_package_installed = isPackageInstalled,
                recommended_api = InputSystemDetector.GetRecommendedInputCode(mode),
                warnings = GetModeWarnings(mode)
            });
        }

        /// <summary>
        /// Gets detailed code guidance for the current configuration.
        /// </summary>
        private static object HandleGetGuidance()
        {
            var mode = InputSystemDetector.GetProjectInputMode();
            var guidance = InputSystemDetector.GetCodeGuidance(mode);

            // Build example code for each mode
            var examples = GetCodeExamples(mode);

            return new SuccessResponse("Input API guidance", new
            {
                mode = mode.ToString(),
                guidance = guidance,
                examples = examples,
                common_patterns = GetCommonPatterns(mode)
            });
        }

        /// <summary>
        /// Scans a single script for input API compatibility.
        /// </summary>
        private static object HandleScanScript(JObject @params)
        {
            string scriptPath = @params["path"]?.ToString();

            if (string.IsNullOrEmpty(scriptPath))
            {
                return new ErrorResponse("Required parameter 'path' is missing. Provide path to the C# script.");
            }

            // Resolve relative paths
            if (!Path.IsPathRooted(scriptPath))
            {
                scriptPath = Path.Combine(Application.dataPath, scriptPath);
            }

            if (!File.Exists(scriptPath))
            {
                return new ErrorResponse($"Script not found: {scriptPath}");
            }

            var result = InputSystemDetector.ScanScript(scriptPath);
            var mode = InputSystemDetector.GetProjectInputMode();

            return new SuccessResponse(
                result.Warnings.Count > 0 ? "Potential issues found" : "Script is compatible",
                new
                {
                    script_path = result.ScriptPath,
                    project_mode = mode.ToString(),
                    uses_legacy_input = result.UsesLegacyInput,
                    uses_new_input_system = result.UsesNewInputSystem,
                    legacy_usages = result.LegacyUsages.Select(u => new
                    {
                        line = u.LineNumber,
                        api_call = u.ApiCall,
                        content = u.LineContent
                    }).ToList(),
                    new_system_usages = result.NewSystemUsages.Select(u => new
                    {
                        line = u.LineNumber,
                        api_call = u.ApiCall,
                        content = u.LineContent
                    }).ToList(),
                    warnings = result.Warnings,
                    is_compatible = result.Warnings.Count == 0 || !result.Warnings.Any(w => w.StartsWith("INCOMPATIBLE"))
                });
        }

        /// <summary>
        /// Scans a directory for scripts with input API issues.
        /// </summary>
        private static object HandleScanDirectory(JObject @params)
        {
            string directory = @params["path"]?.ToString();

            if (string.IsNullOrEmpty(directory))
            {
                directory = Path.Combine(Application.dataPath, "Scripts");
            }
            else if (!Path.IsPathRooted(directory))
            {
                directory = Path.Combine(Application.dataPath, directory);
            }

            if (!Directory.Exists(directory))
            {
                return new ErrorResponse($"Directory not found: {directory}");
            }

            var results = InputSystemDetector.ScanDirectory(directory);
            var mode = InputSystemDetector.GetProjectInputMode();

            var incompatibleCount = results.Count(r => r.Warnings.Any(w => w.StartsWith("INCOMPATIBLE")));

            return new SuccessResponse(
                incompatibleCount > 0
                    ? $"Found {incompatibleCount} script(s) with incompatible input API"
                    : "All scripts are compatible",
                new
                {
                    directory = directory,
                    project_mode = mode.ToString(),
                    scripts_scanned = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).Length,
                    scripts_with_issues = results.Count,
                    incompatible_count = incompatibleCount,
                    issues = results.Select(r => new
                    {
                        script = Path.GetFileName(r.ScriptPath),
                        path = r.ScriptPath,
                        uses_legacy = r.UsesLegacyInput,
                        uses_new = r.UsesNewInputSystem,
                        warnings = r.Warnings
                    }).ToList()
                });
        }

        /// <summary>
        /// Generates a full report including configuration and script scan.
        /// </summary>
        private static object HandleFullReport()
        {
            var report = InputSystemDetector.GenerateReport();

            return new SuccessResponse(
                report.HasIssues
                    ? $"Input system report: {report.IncompatibleScripts.Count} issue(s) found"
                    : "Input system report: No issues",
                new
                {
                    project_mode = report.ProjectMode.ToString(),
                    mode_description = report.ModeDescription,
                    recommended_api = report.RecommendedApi,
                    guidance = report.CodeGuidance,
                    has_issues = report.HasIssues,
                    incompatible_scripts = report.IncompatibleScripts.Select(r => new
                    {
                        script = Path.GetFileName(r.ScriptPath),
                        path = r.ScriptPath,
                        legacy_usage_count = r.LegacyUsages.Count,
                        new_system_usage_count = r.NewSystemUsages.Count,
                        warnings = r.Warnings
                    }).ToList(),
                    pre_generation_check = GetPreGenerationCheck(report.ProjectMode)
                });
        }

        /// <summary>
        /// Gets warnings specific to the input mode.
        /// </summary>
        private static List<string> GetModeWarnings(InputSystemDetector.InputMode mode)
        {
            var warnings = new List<string>();

            switch (mode)
            {
                case InputSystemDetector.InputMode.Legacy:
                    warnings.Add("Legacy Input Manager is deprecated. Consider migrating to Input System Package.");
                    break;

                case InputSystemDetector.InputMode.InputSystem:
                    warnings.Add("IMPORTANT: Do NOT use UnityEngine.Input API - it will cause runtime errors!");
                    warnings.Add("Always add 'using UnityEngine.InputSystem;' to scripts using input.");
                    warnings.Add("Always null-check devices (e.g., if (Mouse.current != null))");
                    break;

                case InputSystemDetector.InputMode.Both:
                    warnings.Add("Both systems active. Prefer Input System for new code.");
                    break;

                case InputSystemDetector.InputMode.Unknown:
                    warnings.Add("Could not determine input configuration. Check Player Settings.");
                    break;
            }

            return warnings;
        }

        /// <summary>
        /// Gets code examples for the specified mode.
        /// </summary>
        private static object GetCodeExamples(InputSystemDetector.InputMode mode)
        {
            return mode switch
            {
                InputSystemDetector.InputMode.Legacy => new
                {
                    mouse_click = "if (Input.GetMouseButtonDown(0)) { /* left click */ }",
                    key_press = "if (Input.GetKeyDown(KeyCode.Space)) { /* space pressed */ }",
                    movement = "float h = Input.GetAxis(\"Horizontal\"); float v = Input.GetAxis(\"Vertical\");",
                    mouse_position = "Vector3 pos = Input.mousePosition;",
                    hold_key = "if (Input.GetKey(KeyCode.LeftShift)) { /* shift held */ }"
                },
                InputSystemDetector.InputMode.InputSystem => new
                {
                    required_using = "using UnityEngine.InputSystem;",
                    mouse_click = "if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) { /* left click */ }",
                    key_press = "if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) { /* space pressed */ }",
                    movement = "// Use InputAction with Vector2 binding for movement",
                    mouse_position = "Vector2 pos = Mouse.current?.position.ReadValue() ?? Vector2.zero;",
                    hold_key = "if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) { /* shift held */ }"
                },
                InputSystemDetector.InputMode.Both => new
                {
                    note = "Both APIs work. Examples show preferred Input System approach.",
                    required_using = "using UnityEngine.InputSystem;",
                    mouse_click = "if (Mouse.current?.leftButton.wasPressedThisFrame == true) { /* left click */ }",
                    key_press = "if (Keyboard.current?.spaceKey.wasPressedThisFrame == true) { /* space pressed */ }",
                    legacy_alternative = "// Legacy also works: Input.GetMouseButtonDown(0)"
                },
                _ => new { error = "Unknown mode" }
            };
        }

        /// <summary>
        /// Gets common input patterns for the specified mode.
        /// </summary>
        private static object GetCommonPatterns(InputSystemDetector.InputMode mode)
        {
            if (mode == InputSystemDetector.InputMode.InputSystem || mode == InputSystemDetector.InputMode.Both)
            {
                return new
                {
                    device_check = "Always check device != null before accessing",
                    click_detection = "Use wasPressedThisFrame for single-frame detection",
                    hold_detection = "Use isPressed for continuous hold detection",
                    release_detection = "Use wasReleasedThisFrame for release detection",
                    value_reading = "Use ReadValue<T>() to get input values",
                    action_based = "Consider using PlayerInput component for action-based input"
                };
            }
            else
            {
                return new
                {
                    click_detection = "Use GetMouseButtonDown(0/1/2) for single-frame detection",
                    hold_detection = "Use GetMouseButton(0/1/2) for continuous hold",
                    key_detection = "Use GetKeyDown/GetKey/GetKeyUp with KeyCode",
                    axis_input = "Use GetAxis/GetAxisRaw for movement",
                    touch_input = "Use Input.touches for mobile"
                };
            }
        }

        /// <summary>
        /// Gets a pre-generation check message for AI code generation.
        /// </summary>
        private static object GetPreGenerationCheck(InputSystemDetector.InputMode mode)
        {
            return mode switch
            {
                InputSystemDetector.InputMode.Legacy => new
                {
                    message = "BEFORE generating input code: Use UnityEngine.Input API only",
                    do_use = new[] { "Input.GetMouseButton", "Input.GetKey", "Input.GetAxis", "Input.mousePosition" },
                    do_not_use = new[] { "Mouse.current", "Keyboard.current", "InputAction", "PlayerInput" },
                    reason = "Project has only Legacy Input Manager enabled"
                },
                InputSystemDetector.InputMode.InputSystem => new
                {
                    message = "BEFORE generating input code: Use UnityEngine.InputSystem API only",
                    do_use = new[] { "Mouse.current", "Keyboard.current", "Gamepad.current", "InputAction" },
                    do_not_use = new[] { "Input.GetMouseButton", "Input.GetKey", "Input.GetAxis", "Input.mousePosition" },
                    required_using = "using UnityEngine.InputSystem;",
                    reason = "Project has only Input System Package enabled"
                },
                InputSystemDetector.InputMode.Both => new
                {
                    message = "Both input systems available. Prefer InputSystem for new code.",
                    preferred = new[] { "Mouse.current", "Keyboard.current", "InputAction" },
                    allowed = new[] { "Input.GetMouseButton", "Input.GetKey" },
                    required_using = "using UnityEngine.InputSystem; // if using new API",
                    reason = "Project supports both systems"
                },
                _ => new
                {
                    message = "Could not determine input configuration. Check manually.",
                    action = "Verify Player Settings > Active Input Handling"
                }
            };
        }
    }
}
#endif
