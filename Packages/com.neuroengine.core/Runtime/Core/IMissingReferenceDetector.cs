using System.Collections.Generic;
using UnityEngine;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for detecting null/missing serialized field references.
    /// This is Layer 2 (Observation) - catches Inspector drag-drop references that were never assigned.
    /// </summary>
    public interface IMissingReferenceDetector
    {
        /// <summary>
        /// Scan a single GameObject and all its components for missing references.
        /// </summary>
        /// <param name="target">The GameObject to scan</param>
        /// <param name="includeChildren">If true, recursively scan child GameObjects</param>
        /// <returns>Report containing all found missing references</returns>
        MissingReferenceReport Scan(GameObject target, bool includeChildren = true);

        /// <summary>
        /// Scan the entire active scene for missing references.
        /// </summary>
        /// <returns>Report containing all found missing references in the scene</returns>
        MissingReferenceReport ScanScene();

        /// <summary>
        /// Scan a prefab asset for missing references.
        /// </summary>
        /// <param name="prefabPath">Asset path to the prefab (e.g., "Assets/Prefabs/Player.prefab")</param>
        /// <returns>Report containing all found missing references in the prefab</returns>
        MissingReferenceReport ScanPrefab(string prefabPath);
    }

    /// <summary>
    /// Report containing the results of a missing reference scan.
    /// </summary>
    [System.Serializable]
    public class MissingReferenceReport
    {
        /// <summary>
        /// List of all missing references found during the scan.
        /// </summary>
        public List<MissingReference> References = new List<MissingReference>();

        /// <summary>
        /// Description of what was scanned (GameObject name, scene name, or prefab path).
        /// </summary>
        public string ScannedTarget;

        /// <summary>
        /// ISO 8601 timestamp of when the scan was performed.
        /// </summary>
        public string Timestamp;

        /// <summary>
        /// Total number of serialized fields that were checked.
        /// </summary>
        public int TotalFieldsScanned;

        /// <summary>
        /// Number of null/missing references found.
        /// </summary>
        public int NullCount;

        /// <summary>
        /// Number of errors encountered (severity = "error").
        /// </summary>
        public int ErrorCount => References.FindAll(r => r.Severity == "error").Count;

        /// <summary>
        /// Number of warnings encountered (severity = "warning").
        /// </summary>
        public int WarningCount => References.FindAll(r => r.Severity == "warning").Count;

        /// <summary>
        /// Whether the scan passed (no errors found).
        /// Warnings do not cause failure.
        /// </summary>
        public bool Passed => ErrorCount == 0;

        /// <summary>
        /// Human-readable summary of the scan results.
        /// </summary>
        public string Summary => $"Scanned {TotalFieldsScanned} fields, found {NullCount} missing ({ErrorCount} errors, {WarningCount} warnings)";
    }

    /// <summary>
    /// Represents a single missing/null serialized field reference.
    /// </summary>
    [System.Serializable]
    public class MissingReference
    {
        /// <summary>
        /// Full hierarchy path to the GameObject containing the missing reference.
        /// Example: "Canvas/Panel/Button"
        /// </summary>
        public string ObjectPath;

        /// <summary>
        /// The type name of the component that has the missing reference.
        /// Example: "PlayerController"
        /// </summary>
        public string ComponentType;

        /// <summary>
        /// The name of the serialized field that is null.
        /// Example: "_healthBar"
        /// </summary>
        public string FieldName;

        /// <summary>
        /// The expected type of the field.
        /// Example: "UnityEngine.UI.Image"
        /// </summary>
        public string ExpectedType;

        /// <summary>
        /// Severity level: "error" for required fields, "warning" for optional fields.
        /// Fields marked with [Optional] attribute are treated as warnings.
        /// </summary>
        public string Severity;

        /// <summary>
        /// Index in an array/list if the missing reference is an element.
        /// -1 if not an array element.
        /// </summary>
        public int ArrayIndex = -1;

        /// <summary>
        /// Human-readable description of the issue.
        /// </summary>
        public string Description => ArrayIndex >= 0
            ? $"{ObjectPath}: {ComponentType}.{FieldName}[{ArrayIndex}] is null (expected {ExpectedType}) [{Severity}]"
            : $"{ObjectPath}: {ComponentType}.{FieldName} is null (expected {ExpectedType}) [{Severity}]";
    }

    /// <summary>
    /// Attribute to mark serialized fields as optional.
    /// Fields marked with this attribute will generate warnings instead of errors when null.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class OptionalAttribute : System.Attribute
    {
        /// <summary>
        /// Optional reason why the field is allowed to be null.
        /// </summary>
        public string Reason { get; set; }

        public OptionalAttribute() { }

        public OptionalAttribute(string reason)
        {
            Reason = reason;
        }
    }
}
