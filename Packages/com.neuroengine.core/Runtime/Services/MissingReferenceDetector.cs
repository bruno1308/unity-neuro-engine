using System;
using System.Collections.Generic;
using System.Reflection;
using NeuroEngine.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Detects null/missing serialized field references at runtime.
    /// This is Layer 2 (Observation) - catches Inspector drag-drop references that were never assigned.
    /// </summary>
    public class MissingReferenceDetector : IMissingReferenceDetector
    {
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public MissingReferenceReport Scan(GameObject target, bool includeChildren = true)
        {
            var report = new MissingReferenceReport
            {
                ScannedTarget = GetObjectPath(target),
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            if (includeChildren)
            {
                ScanRecursive(target.transform, report);
            }
            else
            {
                ScanGameObject(target, report);
            }

            report.NullCount = report.References.Count;
            return report;
        }

        public MissingReferenceReport ScanScene()
        {
            var scene = SceneManager.GetActiveScene();
            var report = new MissingReferenceReport
            {
                ScannedTarget = $"Scene: {scene.name}",
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                ScanRecursive(root.transform, report);
            }

            report.NullCount = report.References.Count;
            return report;
        }

        public MissingReferenceReport ScanPrefab(string prefabPath)
        {
            var report = new MissingReferenceReport
            {
                ScannedTarget = $"Prefab: {prefabPath}",
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            // Note: Full prefab scanning requires Editor APIs
            // This runtime version can only scan instantiated prefabs
            Debug.LogWarning($"[MissingReferenceDetector] Runtime prefab scanning requires Editor APIs. Path: {prefabPath}");

            return report;
        }

        private void ScanRecursive(Transform transform, MissingReferenceReport report)
        {
            ScanGameObject(transform.gameObject, report);

            foreach (Transform child in transform)
            {
                ScanRecursive(child, report);
            }
        }

        private void ScanGameObject(GameObject go, MissingReferenceReport report)
        {
            var components = go.GetComponents<Component>();
            string objectPath = GetObjectPath(go);

            foreach (var component in components)
            {
                if (component == null)
                {
                    // Missing script component
                    report.References.Add(new MissingReference
                    {
                        ObjectPath = objectPath,
                        ComponentType = "Missing Script",
                        FieldName = "(Script Reference)",
                        ExpectedType = "MonoBehaviour",
                        Severity = "error"
                    });
                    continue;
                }

                ScanComponent(component, objectPath, report);
            }
        }

        private void ScanComponent(Component component, string objectPath, MissingReferenceReport report)
        {
            var type = component.GetType();
            var fields = type.GetFields(FieldFlags);

            foreach (var field in fields)
            {
                // Check if it's a Unity Object type (serializable reference)
                if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) &&
                    !IsListOrArrayOfUnityObject(field.FieldType))
                {
                    continue;
                }

                // Check if it has SerializeField attribute or is public
                if (!field.IsPublic && !Attribute.IsDefined(field, typeof(SerializeField)))
                {
                    continue;
                }

                report.TotalFieldsScanned++;

                // Check for Optional attribute
                bool isOptional = Attribute.IsDefined(field, typeof(OptionalAttribute));
                string severity = isOptional ? "warning" : "error";

                var value = field.GetValue(component);

                if (IsListOrArrayOfUnityObject(field.FieldType))
                {
                    // Handle arrays and lists
                    ScanCollectionField(field, value, objectPath, type.Name, severity, report);
                }
                else if (value == null || (value is UnityEngine.Object obj && obj == null))
                {
                    // Single null reference
                    report.References.Add(new MissingReference
                    {
                        ObjectPath = objectPath,
                        ComponentType = type.Name,
                        FieldName = field.Name,
                        ExpectedType = field.FieldType.Name,
                        Severity = severity
                    });
                }
            }
        }

        private void ScanCollectionField(FieldInfo field, object value, string objectPath,
            string componentType, string severity, MissingReferenceReport report)
        {
            if (value == null) return;

            if (value is Array array)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    report.TotalFieldsScanned++;
                    var element = array.GetValue(i);
                    if (element == null || (element is UnityEngine.Object obj && obj == null))
                    {
                        report.References.Add(new MissingReference
                        {
                            ObjectPath = objectPath,
                            ComponentType = componentType,
                            FieldName = field.Name,
                            ExpectedType = field.FieldType.GetElementType()?.Name ?? "Unknown",
                            Severity = severity,
                            ArrayIndex = i
                        });
                    }
                }
            }
            else if (value is System.Collections.IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    report.TotalFieldsScanned++;
                    var element = list[i];
                    if (element == null || (element is UnityEngine.Object obj && obj == null))
                    {
                        var genericType = field.FieldType.GetGenericArguments();
                        string elementType = genericType.Length > 0 ? genericType[0].Name : "Unknown";

                        report.References.Add(new MissingReference
                        {
                            ObjectPath = objectPath,
                            ComponentType = componentType,
                            FieldName = field.Name,
                            ExpectedType = elementType,
                            Severity = severity,
                            ArrayIndex = i
                        });
                    }
                }
            }
        }

        private bool IsListOrArrayOfUnityObject(Type type)
        {
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return elementType != null && typeof(UnityEngine.Object).IsAssignableFrom(elementType);
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var genericArgs = type.GetGenericArguments();
                return genericArgs.Length > 0 && typeof(UnityEngine.Object).IsAssignableFrom(genericArgs[0]);
            }

            return false;
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
