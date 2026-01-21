using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeuroEngine.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Validates scenes against configurable rules.
    /// This is Layer 2 (Observation) - supports YAML-loaded and code-defined rules.
    /// </summary>
    public class ValidationRulesEngine : IValidationRules
    {
        private readonly List<ValidationRule> _rules = new List<ValidationRule>();

        public ValidationRulesEngine()
        {
            RegisterBuiltInRules();
        }

        public ValidationReport ValidateScene()
        {
            var report = new ValidationReport
            {
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            // Run scene-level rules
            foreach (var rule in _rules.Where(r => r.IsEnabled))
            {
                if (rule.ConditionType == "component_exists")
                {
                    RunComponentExistsRule(rule, report);
                }
                else if (rule.ConditionType == "component_dependency")
                {
                    RunComponentDependencyRule(rule, report);
                }
                else if (rule.ConditionType == "missing_scripts")
                {
                    RunMissingScriptsRule(roots, rule, report);
                }
                else if (rule.Validator != null)
                {
                    // Custom validator runs on each root
                    foreach (var root in roots)
                    {
                        RunValidatorRecursive(root.transform, rule, report);
                    }
                }
            }

            // Count by severity
            report.ErrorCount = report.Results.Count(r => r.Severity == ValidationSeverity.Error);
            report.WarningCount = report.Results.Count(r => r.Severity == ValidationSeverity.Warning);
            report.InfoCount = report.Results.Count(r => r.Severity == ValidationSeverity.Info);

            return report;
        }

        public ValidationReport ValidateObject(GameObject target)
        {
            var report = new ValidationReport
            {
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            foreach (var rule in _rules.Where(r => r.IsEnabled && r.Validator != null))
            {
                RunValidatorRecursive(target.transform, rule, report);
            }

            report.ErrorCount = report.Results.Count(r => r.Severity == ValidationSeverity.Error);
            report.WarningCount = report.Results.Count(r => r.Severity == ValidationSeverity.Warning);
            report.InfoCount = report.Results.Count(r => r.Severity == ValidationSeverity.Info);

            return report;
        }

        public void RegisterRule(ValidationRule rule)
        {
            if (_rules.Any(r => r.Id == rule.Id))
            {
                Debug.LogWarning($"[ValidationRules] Rule '{rule.Id}' already registered, skipping.");
                return;
            }
            _rules.Add(rule);
        }

        public void LoadRulesFromYaml(string yamlPath)
        {
            if (!File.Exists(yamlPath))
            {
                Debug.LogWarning($"[ValidationRules] YAML file not found: {yamlPath}");
                return;
            }

            try
            {
                var yaml = File.ReadAllText(yamlPath);
                ParseYamlRules(yaml);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ValidationRules] Failed to load YAML: {e.Message}");
            }
        }

        public List<ValidationRule> GetRegisteredRules()
        {
            return new List<ValidationRule>(_rules);
        }

        private void RegisterBuiltInRules()
        {
            // Camera required
            _rules.Add(new ValidationRule
            {
                Id = "camera_required",
                Description = "Scene should have at least one Camera",
                Severity = ValidationSeverity.Error,
                ConditionType = "component_exists",
                Component = "Camera",
                MinCount = 1
            });

            // Light required
            _rules.Add(new ValidationRule
            {
                Id = "light_required",
                Description = "Scene should have at least one Light source",
                Severity = ValidationSeverity.Warning,
                ConditionType = "component_exists",
                Component = "Light",
                MinCount = 1
            });

            // Single AudioListener
            _rules.Add(new ValidationRule
            {
                Id = "audio_listener_single",
                Description = "Only one AudioListener should exist",
                Severity = ValidationSeverity.Error,
                ConditionType = "component_exists",
                Component = "AudioListener",
                MaxCount = 1
            });

            // EventSystem required for Canvas
            _rules.Add(new ValidationRule
            {
                Id = "event_system_required",
                Description = "Canvas requires EventSystem",
                Severity = ValidationSeverity.Error,
                ConditionType = "component_dependency",
                RequiresComponent = "Canvas",
                DependsOn = "EventSystem",
                AutoFixMethod = "CreateEventSystem"
            });

            // Missing scripts
            _rules.Add(new ValidationRule
            {
                Id = "missing_scripts",
                Description = "No missing script references",
                Severity = ValidationSeverity.Error,
                ConditionType = "missing_scripts"
            });
        }

        private void RunComponentExistsRule(ValidationRule rule, ValidationReport report)
        {
            var type = FindComponentType(rule.Component);
            if (type == null)
            {
                Debug.LogWarning($"[ValidationRules] Component type not found: {rule.Component}");
                return;
            }

            var components = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);
            int count = components.Length;

            bool passed = true;
            string message = "";

            if (rule.MinCount > 0 && count < rule.MinCount)
            {
                passed = false;
                message = $"Expected at least {rule.MinCount} {rule.Component}, found {count}";
            }
            else if (rule.MaxCount > 0 && count > rule.MaxCount)
            {
                passed = false;
                message = $"Expected at most {rule.MaxCount} {rule.Component}, found {count}";
            }

            if (!passed)
            {
                report.Results.Add(new ValidationResult
                {
                    Passed = false,
                    RuleId = rule.Id,
                    Message = message,
                    Severity = rule.Severity,
                    AutoFixSuggestion = rule.AutoFixMethod
                });
            }
        }

        private void RunComponentDependencyRule(ValidationRule rule, ValidationReport report)
        {
            var requiringType = FindComponentType(rule.RequiresComponent);
            var dependentType = FindComponentType(rule.DependsOn);

            if (requiringType == null || dependentType == null) return;

            var requiringComponents = UnityEngine.Object.FindObjectsByType(requiringType, FindObjectsSortMode.None);
            var dependentComponents = UnityEngine.Object.FindObjectsByType(dependentType, FindObjectsSortMode.None);

            if (requiringComponents.Length > 0 && dependentComponents.Length == 0)
            {
                report.Results.Add(new ValidationResult
                {
                    Passed = false,
                    RuleId = rule.Id,
                    Message = $"{rule.RequiresComponent} found but {rule.DependsOn} is missing",
                    Severity = rule.Severity,
                    AutoFixSuggestion = $"Create {rule.DependsOn}"
                });
            }
        }

        private void RunMissingScriptsRule(GameObject[] roots, ValidationRule rule, ValidationReport report)
        {
            foreach (var root in roots)
            {
                CheckMissingScriptsRecursive(root.transform, rule, report);
            }
        }

        private void CheckMissingScriptsRecursive(Transform transform, ValidationRule rule, ValidationReport report)
        {
            var components = transform.gameObject.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null)
                {
                    report.Results.Add(new ValidationResult
                    {
                        Passed = false,
                        RuleId = rule.Id,
                        Message = "Missing script reference",
                        ObjectPath = GetObjectPath(transform.gameObject),
                        Severity = rule.Severity,
                        AutoFixSuggestion = "Remove missing script component"
                    });
                }
            }

            foreach (Transform child in transform)
            {
                CheckMissingScriptsRecursive(child, rule, report);
            }
        }

        private void RunValidatorRecursive(Transform transform, ValidationRule rule, ValidationReport report)
        {
            var result = rule.Validator?.Invoke(transform.gameObject);
            if (result != null && !result.Passed)
            {
                result.RuleId = rule.Id;
                result.Severity = rule.Severity;
                result.ObjectPath = GetObjectPath(transform.gameObject);
                report.Results.Add(result);
            }

            foreach (Transform child in transform)
            {
                RunValidatorRecursive(child, rule, report);
            }
        }

        private void ParseYamlRules(string yaml)
        {
            // Simple YAML parser for our rule format
            // Format: validators: - id: x, description: y, severity: z, component_required: w
            var lines = yaml.Split('\n');
            ValidationRule currentRule = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                if (trimmed.StartsWith("- id:"))
                {
                    if (currentRule != null) RegisterRule(currentRule);
                    currentRule = new ValidationRule
                    {
                        Id = trimmed.Substring(5).Trim()
                    };
                }
                else if (currentRule != null)
                {
                    if (trimmed.StartsWith("description:"))
                        currentRule.Description = trimmed.Substring(12).Trim().Trim('"');
                    else if (trimmed.StartsWith("severity:"))
                    {
                        var sev = trimmed.Substring(9).Trim().ToLower();
                        currentRule.Severity = sev switch
                        {
                            "error" => ValidationSeverity.Error,
                            "warning" => ValidationSeverity.Warning,
                            _ => ValidationSeverity.Info
                        };
                    }
                    else if (trimmed.StartsWith("condition_type:"))
                        currentRule.ConditionType = trimmed.Substring(15).Trim();
                    else if (trimmed.StartsWith("component:") || trimmed.StartsWith("component_required:"))
                    {
                        currentRule.Component = trimmed.Contains(":") ?
                            trimmed.Substring(trimmed.IndexOf(':') + 1).Trim() : "";
                        currentRule.ConditionType = "component_exists";
                    }
                    else if (trimmed.StartsWith("requires_component:"))
                        currentRule.RequiresComponent = trimmed.Substring(19).Trim();
                    else if (trimmed.StartsWith("depends_on:"))
                        currentRule.DependsOn = trimmed.Substring(11).Trim();
                    else if (trimmed.StartsWith("min_count:"))
                        int.TryParse(trimmed.Substring(10).Trim(), out currentRule.MinCount);
                    else if (trimmed.StartsWith("max_count:"))
                        int.TryParse(trimmed.Substring(10).Trim(), out currentRule.MaxCount);
                    else if (trimmed.StartsWith("auto_fix:"))
                        currentRule.AutoFixMethod = trimmed.Substring(9).Trim().Trim('"');
                }
            }

            if (currentRule != null) RegisterRule(currentRule);
        }

        private Type FindComponentType(string typeName)
        {
            // Try Unity built-in types first
            var type = Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule");
            if (type != null) return type;

            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null) return type;

            type = Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI");
            if (type != null) return type;

            type = Type.GetType($"UnityEngine.EventSystems.{typeName}, UnityEngine.UI");
            if (type != null) return type;

            // Search all assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;

                type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                if (type != null) return type;
            }

            return null;
        }

        private string GetObjectPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
