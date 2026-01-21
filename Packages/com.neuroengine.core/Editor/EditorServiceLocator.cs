#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NeuroEngine.Core;
using NeuroEngine.Services;
using UnityEngine;
using VContainer;

namespace NeuroEngine.Editor
{
    /// <summary>
    /// Provides unified service access for editor code and MCP tools.
    ///
    /// This solves the "two parallel worlds" problem where MCP tools
    /// (which use static HandleCommand methods) cannot use constructor injection.
    ///
    /// Resolution strategy:
    /// 1. If VContainer LifetimeScope exists → resolve from container
    /// 2. Otherwise → create standalone instances (editor-only fallback)
    ///
    /// Services are cached for the lifetime of the editor session.
    /// Call Reset() on domain reload if needed.
    /// </summary>
    public static class EditorServiceLocator
    {
        // Cached service instances (fallback when no DI container)
        private static readonly Dictionary<Type, object> _cache = new();

        // Lock for thread safety
        private static readonly object _lock = new();

        /// <summary>
        /// Get a service instance. Tries VContainer first, falls back to manual creation.
        /// </summary>
        public static T Get<T>() where T : class
        {
            lock (_lock)
            {
                var type = typeof(T);

                // Check cache first
                if (_cache.TryGetValue(type, out var cached))
                {
                    return (T)cached;
                }

                // Try to resolve from VContainer if available
                var resolved = TryResolveFromContainer<T>();
                if (resolved != null)
                {
                    _cache[type] = resolved;
                    return resolved;
                }

                // Fall back to manual creation
                var instance = CreateInstance<T>();
                if (instance != null)
                {
                    _cache[type] = instance;
                }
                return instance;
            }
        }

        /// <summary>
        /// Try to resolve from VContainer LifetimeScope if one exists.
        /// </summary>
        private static T TryResolveFromContainer<T>() where T : class
        {
            try
            {
                // Find LifetimeScope in scene
                var lifetimeScope = UnityEngine.Object.FindFirstObjectByType<NeuroEngineLifetimeScope>();
                if (lifetimeScope != null && lifetimeScope.Container != null)
                {
                    return lifetimeScope.Container.Resolve<T>();
                }
            }
            catch (Exception e)
            {
                // Container doesn't have this type registered, or other error
                Debug.LogWarning($"[EditorServiceLocator] Could not resolve {typeof(T).Name} from container: {e.Message}");
            }
            return null;
        }

        /// <summary>
        /// Create standalone instance for editor-only use.
        /// </summary>
        private static T CreateInstance<T>() where T : class
        {
            var type = typeof(T);

            // Map interfaces to implementations
            if (type == typeof(ISceneStateCapture))
                return new SceneStateCaptureService() as T;

            if (type == typeof(IMissingReferenceDetector))
                return new MissingReferenceDetector() as T;

            if (type == typeof(IUIAccessibility))
                return new UIAccessibilityService() as T;

            if (type == typeof(ISpatialAnalysis))
                return new SpatialAnalysisService() as T;

            if (type == typeof(IValidationRules))
                return new ValidationRulesEngine() as T;

            if (type == typeof(IInputSimulation))
                return new InputSimulationService() as T;

            // For concrete types, try parameterless constructor
            if (!type.IsInterface && !type.IsAbstract)
            {
                try
                {
                    return Activator.CreateInstance<T>();
                }
                catch
                {
                    // Type doesn't have parameterless constructor
                }
            }

            Debug.LogError($"[EditorServiceLocator] Cannot create instance of {type.Name}. Add mapping in CreateInstance<T>().");
            return null;
        }

        /// <summary>
        /// Clear all cached instances. Call on domain reload or when container changes.
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }

        /// <summary>
        /// Register a specific instance (useful for testing or custom setup).
        /// </summary>
        public static void Register<T>(T instance) where T : class
        {
            lock (_lock)
            {
                _cache[typeof(T)] = instance;
            }
        }
    }
}
#endif
