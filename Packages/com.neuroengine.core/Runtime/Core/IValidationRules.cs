using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for the validation rules engine.
    /// This is Layer 2 (Observation) - configurable rules with auto-fix support.
    /// </summary>
    public interface IValidationRules
    {
        /// <summary>
        /// Run all registered rules against the current scene.
        /// </summary>
        ValidationReport ValidateScene();

        /// <summary>
        /// Validate a single GameObject.
        /// </summary>
        ValidationReport ValidateObject(GameObject target);

        /// <summary>
        /// Register a new validation rule programmatically.
        /// </summary>
        void RegisterRule(ValidationRule rule);

        /// <summary>
        /// Load rules from a YAML file.
        /// </summary>
        void LoadRulesFromYaml(string yamlPath);

        /// <summary>
        /// Get all registered rules.
        /// </summary>
        List<ValidationRule> GetRegisteredRules();
    }

    /// <summary>
    /// Severity levels for validation rules.
    /// </summary>
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// A validation rule definition.
    /// Note: Not marked [Serializable] - we use Newtonsoft.Json for serialization.
    /// </summary>
    public class ValidationRule
    {
        /// <summary>
        /// Unique identifier (e.g., "event_system_required").
        /// </summary>
        public string Id;

        /// <summary>
        /// Human-readable description.
        /// </summary>
        public string Description;

        /// <summary>
        /// Severity level.
        /// </summary>
        public ValidationSeverity Severity;

        /// <summary>
        /// The validation function (null for YAML-loaded rules).
        /// </summary>
        [JsonIgnore]
        public Func<GameObject, ValidationResult> Validator;

        /// <summary>
        /// Optional method name for auto-fix.
        /// </summary>
        public string AutoFixMethod;

        /// <summary>
        /// Whether this rule is enabled.
        /// </summary>
        public bool IsEnabled = true;

        // For YAML-loaded rules
        public string ConditionType;
        public string Component;
        public string RequiresComponent;
        public string DependsOn;
        public int MinCount;
        public int MaxCount;
    }

    /// <summary>
    /// Result of a single validation check.
    /// </summary>
    public class ValidationResult
    {
        public bool Passed;
        public string RuleId;
        public string Message;
        public string ObjectPath;
        public string AutoFixSuggestion;
        public ValidationSeverity Severity;
    }

    /// <summary>
    /// Complete validation report.
    /// </summary>
    public class ValidationReport
    {
        public List<ValidationResult> Results = new List<ValidationResult>();
        public string Timestamp;
        public int ErrorCount;
        public int WarningCount;
        public int InfoCount;
        public bool HasErrors => ErrorCount > 0;

        public string Summary => $"{ErrorCount} errors, {WarningCount} warnings, {InfoCount} info";
    }
}
