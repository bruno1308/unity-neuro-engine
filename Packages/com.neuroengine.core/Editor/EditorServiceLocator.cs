#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NeuroEngine.Core;
using NeuroEngine.Services;
using UnityEditor;
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
    /// Cache is automatically cleared on:
    /// - Domain reload (via InitializeOnLoad)
    /// - Play mode state changes (entering/exiting play mode)
    ///
    /// This ensures VContainer-registered services are preferred when available.
    ///
    /// IMPORTANT: Must be called from main thread only (Unity API constraint).
    /// </summary>
    [InitializeOnLoad]
    public static class EditorServiceLocator
    {
        // Cached service instances (fallback when no DI container)
        private static readonly Dictionary<Type, object> _cache = new();

        // Lock for thread safety
        private static readonly object _lock = new();

        // Track whether we've resolved from container (vs fallback)
        private static readonly HashSet<Type> _resolvedFromContainer = new();

        /// <summary>
        /// Static constructor - called on domain reload via [InitializeOnLoad]
        /// </summary>
        static EditorServiceLocator()
        {
            // Clear cache on domain reload
            Reset();

            // Subscribe to play mode changes to clear cache when VContainer becomes available/unavailable
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Clear cache on any play mode transition
            // This ensures:
            // 1. When entering play mode: VContainer services can be resolved fresh
            // 2. When exiting play mode: Fallback instances are created fresh (no stale references)
            if (state == PlayModeStateChange.EnteredPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                Reset();
            }
        }

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
                    // If we previously resolved from container, trust the cache
                    // If it was a fallback instance, check if container is now available
                    if (_resolvedFromContainer.Contains(type))
                    {
                        return (T)cached;
                    }

                    // Fallback instance cached - check if VContainer is now available
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        var containerResolved = TryResolveFromContainer<T>();
                        if (containerResolved != null)
                        {
                            // Upgrade to container-resolved instance
                            _cache[type] = containerResolved;
                            _resolvedFromContainer.Add(type);
                            return containerResolved;
                        }
                    }
                    return (T)cached;
                }

                // Try to resolve from VContainer if available
                var resolved = TryResolveFromContainer<T>();
                if (resolved != null)
                {
                    _cache[type] = resolved;
                    _resolvedFromContainer.Add(type);
                    return resolved;
                }

                // Fall back to manual creation
                var instance = CreateInstance<T>();
                if (instance != null)
                {
                    _cache[type] = instance;
                    // Don't add to _resolvedFromContainer - this is a fallback
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

            if (type == typeof(ITranscriptWriter))
                return new TranscriptWriterService() as T;

            if (type == typeof(ITaskManager))
                return new TaskManagerService() as T;

            // Layer 5: Evaluation
            if (type == typeof(ISyntacticGrader))
                return new SyntacticGraderService() as T;

            if (type == typeof(IStateGrader))
                return new StateGraderService() as T;

            if (type == typeof(IPolishGrader))
                return new PolishGraderService() as T;

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
        /// Clear all cached instances. Called automatically on domain reload and play mode changes.
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _cache.Clear();
                _resolvedFromContainer.Clear();
            }
        }

        /// <summary>
        /// Register a specific instance (useful for testing or custom setup).
        /// Manually registered instances are marked as "from container" to prevent
        /// automatic upgrade/replacement.
        /// </summary>
        public static void Register<T>(T instance) where T : class
        {
            lock (_lock)
            {
                var type = typeof(T);
                _cache[type] = instance;
                // Mark as "resolved from container" to prevent automatic upgrade
                // This ensures manually registered instances are stable
                _resolvedFromContainer.Add(type);
            }
        }
    }
}
#endif
