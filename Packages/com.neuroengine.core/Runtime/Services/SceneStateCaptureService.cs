using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NeuroEngine.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Captures scene hierarchy as JSON-serializable snapshots.
    /// Layer 1 (Code-First Foundation) - makes Unity state machine-readable.
    /// Now includes component field values for full state observability.
    /// </summary>
    public class SceneStateCaptureService : ISceneStateCapture
    {
        private readonly IHooksWriter _hooksWriter;

        // Types to skip when serializing field values (Unity internals that cause issues)
        private static readonly HashSet<string> SkipFieldTypes = new HashSet<string>
        {
            "UnityEngine.Mesh",
            "UnityEngine.Material",
            "UnityEngine.Shader",
            "UnityEngine.Texture",
            "UnityEngine.Texture2D",
            "UnityEngine.RenderTexture",
            "UnityEngine.ComputeShader",
            "UnityEngine.AnimationClip",
            "UnityEngine.RuntimeAnimatorController"
        };

        // Properties that should never be accessed during serialization (cause instantiation/warnings in edit mode)
        private static readonly Dictionary<string, HashSet<string>> SkipProperties = new Dictionary<string, HashSet<string>>
        {
            { "UnityEngine.MeshFilter", new HashSet<string> { "mesh" } }, // Accessing .mesh in edit mode creates instance
            { "UnityEngine.MeshCollider", new HashSet<string> { "sharedMesh" } }, // Can cause issues
            { "UnityEngine.SkinnedMeshRenderer", new HashSet<string> { "sharedMesh" } }
        };

        // Properties to skip on any Renderer-derived type (material/materials cause instantiation warnings)
        private static readonly HashSet<string> RendererSkipProperties = new HashSet<string>
        {
            "material", "materials", "sharedMaterial", "sharedMaterials"
        };

        // Components to exclude by default (too verbose, rarely useful for AI, or cause runtime warnings)
        private static readonly HashSet<string> DefaultExcludeComponents = new HashSet<string>
        {
            "Transform", "RectTransform", "CanvasRenderer",
            "AudioSource", "AudioListener" // AudioSource.time/timeSamples throw warnings when no clip is playing
        };

        public SceneStateCaptureService(IHooksWriter hooksWriter)
        {
            _hooksWriter = hooksWriter;
        }

        /// <summary>
        /// Parameterless constructor for editor/standalone use.
        /// CaptureAndSaveAsync will throw if called without IHooksWriter.
        /// </summary>
        public SceneStateCaptureService()
        {
            _hooksWriter = null;
        }

        public SceneSnapshot CaptureScene()
        {
            return CaptureScene(new SceneCaptureOptions());
        }

        public SceneSnapshot CaptureScene(SceneCaptureOptions options)
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            int totalObjects = 0;
            int totalComponentsWithData = 0;

            var snapshot = new SceneSnapshot
            {
                SceneName = scene.name,
                Timestamp = DateTime.UtcNow.ToString("o"),
                RootObjects = new GameObjectSnapshot[rootObjects.Length]
            };

            for (int i = 0; i < rootObjects.Length; i++)
            {
                snapshot.RootObjects[i] = CaptureGameObject(rootObjects[i], options, 0, ref totalObjects, ref totalComponentsWithData);
            }

            snapshot.TotalObjectCount = totalObjects;
            snapshot.TotalComponentsWithData = totalComponentsWithData;

            return snapshot;
        }

        public async Task CaptureAndSaveAsync(string sceneName)
        {
            if (_hooksWriter == null)
                throw new InvalidOperationException("CaptureAndSaveAsync requires IHooksWriter. Use constructor with IHooksWriter parameter.");

            var snapshot = CaptureScene();
            var filename = $"{sceneName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            await _hooksWriter.WriteAsync($"scenes/{sceneName}", filename, snapshot);
        }

        private GameObjectSnapshot CaptureGameObject(GameObject go, SceneCaptureOptions options, int depth, ref int totalObjects, ref int totalComponentsWithData)
        {
            totalObjects++;

            var transform = go.transform;
            var components = go.GetComponents<Component>();
            var componentNames = new List<string>();
            var componentSnapshots = new List<ComponentSnapshot>();

            foreach (var comp in components)
            {
                if (comp == null) continue;

                var typeName = comp.GetType().Name;
                componentNames.Add(typeName);

                // Capture component data if enabled
                if (options.IncludeComponentData)
                {
                    // Check exclusion list
                    var excludeList = options.ExcludeComponents.Count > 0
                        ? options.ExcludeComponents
                        : DefaultExcludeComponents.ToList();

                    if (excludeList.Contains(typeName))
                        continue;

                    // Check namespace filter
                    if (options.IncludeNamespaces.Count > 0)
                    {
                        var ns = comp.GetType().Namespace ?? "";
                        if (!options.IncludeNamespaces.Any(n => ns.StartsWith(n)))
                            continue;
                    }

                    var compSnapshot = CaptureComponent(comp);
                    if (compSnapshot != null && compSnapshot.Fields.Count > 0)
                    {
                        componentSnapshots.Add(compSnapshot);
                        totalComponentsWithData++;
                    }
                }
            }

            var snapshot = new GameObjectSnapshot
            {
                Name = go.name,
                Active = go.activeSelf,
                Tag = go.tag,
                Layer = go.layer,
                Position = new[] { transform.position.x, transform.position.y, transform.position.z },
                Rotation = new[] { transform.eulerAngles.x, transform.eulerAngles.y, transform.eulerAngles.z },
                Scale = new[] { transform.localScale.x, transform.localScale.y, transform.localScale.z },
                Components = componentNames.ToArray(),
                ComponentData = componentSnapshots.Count > 0 ? componentSnapshots.ToArray() : null,
                Children = new GameObjectSnapshot[0] // Will be populated below
            };

            // Capture children if within depth limit
            if (options.MaxDepth < 0 || depth < options.MaxDepth)
            {
                var children = new List<GameObjectSnapshot>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    children.Add(CaptureGameObject(transform.GetChild(i).gameObject, options, depth + 1, ref totalObjects, ref totalComponentsWithData));
                }
                snapshot.Children = children.ToArray();
            }

            return snapshot;
        }

        private ComponentSnapshot CaptureComponent(Component comp)
        {
            if (comp == null) return null;

            var type = comp.GetType();
            var snapshot = new ComponentSnapshot
            {
                Type = type.Name,
                FullType = type.FullName,
                Enabled = comp is Behaviour behaviour ? behaviour.enabled : true,
                Fields = new Dictionary<string, object>()
            };

            try
            {
                // Get serialized fields (public fields and [SerializeField] private fields)
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    // Skip non-serialized fields
                    if (field.IsPrivate && !field.IsDefined(typeof(SerializeField), true))
                        continue;

                    // Skip fields with NonSerialized attribute
                    if (field.IsDefined(typeof(NonSerializedAttribute), true))
                        continue;

                    // Skip fields with HideInInspector (usually internal Unity stuff)
                    if (field.IsDefined(typeof(HideInInspector), true))
                        continue;

                    var fieldType = field.FieldType;

                    // Skip problematic types
                    if (SkipFieldTypes.Contains(fieldType.FullName))
                        continue;

                    try
                    {
                        var value = field.GetValue(comp);
                        var serializedValue = SerializeValue(value, fieldType, 0);
                        if (serializedValue != null)
                        {
                            snapshot.Fields[field.Name] = serializedValue;
                        }
                    }
                    catch
                    {
                        // Skip fields that fail to serialize
                    }
                }

                // Also capture public properties with getters (common for Unity components)
                // Limit to MaxFieldsPerComponent to prevent explosion
                const int MaxFieldsPerComponent = 50;
                if (snapshot.Fields.Count >= MaxFieldsPerComponent)
                    return snapshot;

                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in properties)
                {
                    if (snapshot.Fields.Count >= MaxFieldsPerComponent) break;
                    if (!prop.CanRead) continue;
                    if (prop.GetIndexParameters().Length > 0) continue; // Skip indexed properties

                    // Skip properties that cause instantiation/warnings in edit mode
                    if (SkipProperties.TryGetValue(type.FullName, out var skipProps) && skipProps.Contains(prop.Name))
                        continue;

                    // Skip material-related properties on any Renderer-derived type
                    if (typeof(Renderer).IsAssignableFrom(type) && RendererSkipProperties.Contains(prop.Name))
                        continue;

                    // Only capture simple value types and strings from properties
                    var propType = prop.PropertyType;
                    if (!IsSimpleType(propType)) continue;

                    // Skip properties that are also fields
                    if (snapshot.Fields.ContainsKey(prop.Name)) continue;

                    try
                    {
                        var value = prop.GetValue(comp);
                        var serializedValue = SerializeValue(value, propType, 0);
                        if (serializedValue != null)
                        {
                            snapshot.Fields[prop.Name] = serializedValue;
                        }
                    }
                    catch
                    {
                        // Skip properties that fail to read
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SceneCapture] Failed to capture component {type.Name}: {e.Message}");
            }

            return snapshot;
        }

        private const int MaxSerializationDepth = 5;

        private object SerializeValue(object value, Type type, int depth = 0)
        {
            if (value == null) return null;
            if (depth > MaxSerializationDepth) return "[max depth]";

            // Primitives and strings
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return value;

            // Enums as strings
            if (type.IsEnum)
                return value.ToString();

            // Unity Object references - capture name and type
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var unityObj = value as UnityEngine.Object;
                if (unityObj == null) return null;

                return new Dictionary<string, object>
                {
                    { "_type", type.Name },
                    { "_name", unityObj.name },
                    { "_instanceId", unityObj.GetInstanceID() }
                };
            }

            // Vectors
            if (type == typeof(Vector2))
            {
                var v = (Vector2)value;
                return new float[] { v.x, v.y };
            }
            if (type == typeof(Vector3))
            {
                var v = (Vector3)value;
                return new float[] { v.x, v.y, v.z };
            }
            if (type == typeof(Vector4))
            {
                var v = (Vector4)value;
                return new float[] { v.x, v.y, v.z, v.w };
            }
            if (type == typeof(Vector2Int))
            {
                var v = (Vector2Int)value;
                return new int[] { v.x, v.y };
            }
            if (type == typeof(Vector3Int))
            {
                var v = (Vector3Int)value;
                return new int[] { v.x, v.y, v.z };
            }

            // Quaternion
            if (type == typeof(Quaternion))
            {
                var q = (Quaternion)value;
                return new float[] { q.x, q.y, q.z, q.w };
            }

            // Color
            if (type == typeof(Color))
            {
                var c = (Color)value;
                return new float[] { c.r, c.g, c.b, c.a };
            }
            if (type == typeof(Color32))
            {
                var c = (Color32)value;
                return new int[] { c.r, c.g, c.b, c.a };
            }

            // Bounds
            if (type == typeof(Bounds))
            {
                var b = (Bounds)value;
                return new Dictionary<string, object>
                {
                    { "center", new float[] { b.center.x, b.center.y, b.center.z } },
                    { "size", new float[] { b.size.x, b.size.y, b.size.z } }
                };
            }

            // Rect
            if (type == typeof(Rect))
            {
                var r = (Rect)value;
                return new float[] { r.x, r.y, r.width, r.height };
            }

            // LayerMask
            if (type == typeof(LayerMask))
            {
                return ((LayerMask)value).value;
            }

            // AnimationCurve - capture key points
            if (type == typeof(AnimationCurve))
            {
                var curve = (AnimationCurve)value;
                var keys = new List<Dictionary<string, float>>();
                foreach (var key in curve.keys)
                {
                    keys.Add(new Dictionary<string, float>
                    {
                        { "time", key.time },
                        { "value", key.value }
                    });
                }
                return keys;
            }

            // Arrays and Lists of simple types (with size limit)
            const int MaxCollectionSize = 100;

            if (type.IsArray && IsSimpleType(type.GetElementType()))
            {
                var array = value as Array;
                if (array.Length > MaxCollectionSize)
                    return $"[array length {array.Length} exceeds limit]";

                var result = new List<object>();
                foreach (var item in array)
                {
                    var serialized = SerializeValue(item, type.GetElementType(), depth + 1);
                    if (serialized != null)
                        result.Add(serialized);
                }
                return result;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = type.GetGenericArguments()[0];
                if (IsSimpleType(elementType))
                {
                    var list = value as IList;
                    if (list.Count > MaxCollectionSize)
                        return $"[list count {list.Count} exceeds limit]";

                    var result = new List<object>();
                    foreach (var item in list)
                    {
                        var serialized = SerializeValue(item, elementType, depth + 1);
                        if (serialized != null)
                            result.Add(serialized);
                    }
                    return result;
                }
            }

            // Skip complex types we can't serialize well
            return null;
        }

        private bool IsSimpleType(Type type)
        {
            if (type == null) return false;

            return type.IsPrimitive
                   || type == typeof(string)
                   || type == typeof(decimal)
                   || type.IsEnum
                   || type == typeof(Vector2)
                   || type == typeof(Vector3)
                   || type == typeof(Vector4)
                   || type == typeof(Vector2Int)
                   || type == typeof(Vector3Int)
                   || type == typeof(Quaternion)
                   || type == typeof(Color)
                   || type == typeof(Color32)
                   || type == typeof(Rect)
                   || type == typeof(Bounds)
                   || type == typeof(LayerMask)
                   || typeof(UnityEngine.Object).IsAssignableFrom(type);
        }
    }
}
