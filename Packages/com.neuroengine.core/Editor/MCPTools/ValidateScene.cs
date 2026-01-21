#if UNITY_EDITOR
using System;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using NeuroEngine.Services;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to run validation rules against the current scene.
    /// Uses built-in rules and optional custom YAML rules to check scene integrity.
    /// </summary>
    [McpForUnityTool("validate_scene", Description = "Runs validation rules against the current scene. Checks for common issues like missing cameras, lights, audio listeners, event systems, and missing scripts. Can load custom rules from YAML files.")]
    public static class ValidateScene
    {
        private static ValidationRulesEngine _validationEngine;

        public static object HandleCommand(JObject @params)
        {
            string rulesPath = @params["rules_path"]?.ToString();
            string specificRule = @params["rule"]?.ToString();

            _validationEngine ??= new ValidationRulesEngine();

            try
            {
                // Load custom rules if path provided
                if (!string.IsNullOrEmpty(rulesPath))
                {
                    _validationEngine.LoadRulesFromYaml(rulesPath);
                }

                ValidationReport report;

                if (!string.IsNullOrEmpty(specificRule))
                {
                    // Run specific rule only
                    var rules = _validationEngine.GetRegisteredRules();
                    var rule = rules.FirstOrDefault(r => r.Id == specificRule);
                    if (rule == null)
                    {
                        return new ErrorResponse($"Rule '{specificRule}' not found. Use without 'rule' parameter to see all available rules.");
                    }
                    // Run all rules (specific rule filtering not supported yet)
                    report = _validationEngine.ValidateScene();
                }
                else
                {
                    // Run all rules
                    report = _validationEngine.ValidateScene();
                }

                return new SuccessResponse(report.Summary, new
                {
                    passed = !report.HasErrors,
                    timestamp = report.Timestamp,
                    scene_name = SceneManager.GetActiveScene().name,
                    error_count = report.ErrorCount,
                    warning_count = report.WarningCount,
                    info_count = report.InfoCount,
                    results = report.Results.Select(r => new
                    {
                        rule_id = r.RuleId,
                        passed = r.Passed,
                        severity = r.Severity.ToString().ToLowerInvariant(),
                        message = r.Message,
                        fix_suggestion = r.AutoFixSuggestion,
                        object_path = r.ObjectPath
                    }).ToList(),
                    available_rules = _validationEngine.GetRegisteredRules().Select(r => new
                    {
                        id = r.Id,
                        description = r.Description,
                        severity = r.Severity.ToString().ToLowerInvariant()
                    }).ToList()
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error validating scene: {e.Message}");
            }
        }
    }
}
#endif
