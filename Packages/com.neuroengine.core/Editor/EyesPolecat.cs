#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NeuroEngine.Core;
using NeuroEngine.Services;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeuroEngine.Editor
{
    /// <summary>
    /// EyesPolecat - Continuously-running observer that captures game state.
    /// This is the central "Eyes" of the Neuro-Engine observation system.
    /// </summary>
    public class EyesPolecat : EditorWindow
    {
        // State
        private static bool _autoCaptureEnabled = true;
        private static WorldState _lastWorldState;
        private static List<string> _recentSnapshots = new List<string>();
        private const int MaxRecentSnapshots = 10;

        // Services (created on demand since we're in Editor)
        // Note: We don't use DI here since this is editor-only and needs to work standalone
        private static UIAccessibilityService _uiAccessibility;
        private static SpatialAnalysisService _spatialAnalysis;
        private static ValidationRulesEngine _validationRules;

        // Hooks root path (editor-only, no DI)
        private static string _hooksRoot;

        // UI
        private Vector2 _scrollPosition;

        [MenuItem("Neuro-Engine/Eyes Polecat %#e")]
        public static void ShowWindow()
        {
            var window = GetWindow<EyesPolecat>("Eyes Polecat");
            window.minSize = new Vector2(300, 400);
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Subscribe to events
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            Debug.Log("[EyesPolecat] Observer initialized");
        }

        private void OnEnable()
        {
            InitializeServices();
        }

        private static void InitializeServices()
        {
            _uiAccessibility ??= new UIAccessibilityService();
            _spatialAnalysis ??= new SpatialAnalysisService();
            _validationRules ??= new ValidationRulesEngine();

            if (string.IsNullOrEmpty(_hooksRoot))
            {
                _hooksRoot = Path.Combine(Application.dataPath, "..", "hooks");
                Directory.CreateDirectory(_hooksRoot);
            }
        }

        #region Event Handlers

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!_autoCaptureEnabled) return;

            if (state == PlayModeStateChange.EnteredEditMode ||
                state == PlayModeStateChange.EnteredPlayMode)
            {
                CaptureAndSave($"PlayMode_{state}");
            }
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (!_autoCaptureEnabled) return;
            CaptureAndSave($"SceneSaved_{scene.name}");
        }

        private static void OnCompilationFinished(object obj)
        {
            if (!_autoCaptureEnabled) return;
            CaptureAndSave("CompilationFinished");
        }

        #endregion

        #region Capture Methods

        public static WorldState CaptureWorldState()
        {
            InitializeServices();

            var state = new WorldState
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                SceneName = SceneManager.GetActiveScene().name
            };

            try
            {
                // Scene state (captured directly without service dependency)
                state.Scene = CaptureSceneSnapshot();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EyesPolecat] Scene capture failed: {e.Message}");
            }

            try
            {
                // UI graph (may fail if no UI in scene)
                state.UI = _uiAccessibility.CaptureUIState();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EyesPolecat] UI capture failed: {e.Message}");
            }

            try
            {
                // Spatial analysis
                state.Spatial = _spatialAnalysis.AnalyzeScene();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EyesPolecat] Spatial analysis failed: {e.Message}");
            }

            try
            {
                // Validation
                state.Validation = _validationRules.ValidateScene();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EyesPolecat] Validation failed: {e.Message}");
            }

            _lastWorldState = state;
            return state;
        }

        public static string SaveSnapshot(WorldState state)
        {
            InitializeServices();

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var filename = $"{timestamp}.json";
            var dir = Path.Combine(_hooksRoot, "snapshots");
            Directory.CreateDirectory(dir);
            var fullPath = Path.Combine(dir, filename);

            try
            {
                var json = JsonUtility.ToJson(state, true);
                File.WriteAllText(fullPath, json);

                // Track recent snapshots
                var relativePath = $"snapshots/{filename}";
                _recentSnapshots.Insert(0, relativePath);
                if (_recentSnapshots.Count > MaxRecentSnapshots)
                {
                    _recentSnapshots.RemoveAt(_recentSnapshots.Count - 1);
                }

                Debug.Log($"[EyesPolecat] Snapshot saved: {relativePath}");
                return relativePath;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EyesPolecat] Failed to save snapshot: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Captures scene snapshot directly (editor-only, no DI).
        /// </summary>
        private static SceneSnapshot CaptureSceneSnapshot()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            var snapshot = new SceneSnapshot
            {
                SceneName = scene.name,
                Timestamp = DateTime.UtcNow.ToString("o"),
                RootObjects = new GameObjectSnapshot[roots.Length]
            };

            for (int i = 0; i < roots.Length; i++)
            {
                snapshot.RootObjects[i] = CaptureGameObjectSnapshot(roots[i]);
            }

            return snapshot;
        }

        private static GameObjectSnapshot CaptureGameObjectSnapshot(GameObject go)
        {
            var transform = go.transform;
            var components = go.GetComponents<Component>();
            var componentNames = new List<string>();

            foreach (var comp in components)
            {
                if (comp != null)
                    componentNames.Add(comp.GetType().Name);
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
                Children = new GameObjectSnapshot[transform.childCount]
            };

            for (int i = 0; i < transform.childCount; i++)
            {
                snapshot.Children[i] = CaptureGameObjectSnapshot(transform.GetChild(i).gameObject);
            }

            return snapshot;
        }

        public static string CaptureScreenshot(string sceneName)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var dir = Path.Combine(Application.dataPath, "..", "hooks", "scenes", sceneName);
            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, $"screenshot_{timestamp}.png");
            ScreenCapture.CaptureScreenshot(path);

            Debug.Log($"[EyesPolecat] Screenshot saved: {path}");
            return path;
        }

        private static void CaptureAndSave(string trigger)
        {
            var state = CaptureWorldState();
            state.Trigger = trigger;
            SaveSnapshot(state);
        }

        #endregion

        #region Editor Window UI

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Eyes Polecat - World Observer", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Auto-capture toggle
            _autoCaptureEnabled = EditorGUILayout.Toggle("Auto-capture on events", _autoCaptureEnabled);
            EditorGUILayout.Space(5);

            // Manual capture button
            if (GUILayout.Button("Capture Now", GUILayout.Height(30)))
            {
                CaptureAndSave("Manual");
            }

            EditorGUILayout.Space(10);

            // Last capture info
            EditorGUILayout.LabelField("Last Capture", EditorStyles.boldLabel);
            if (_lastWorldState != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Time:", _lastWorldState.Timestamp);
                EditorGUILayout.LabelField("Scene:", _lastWorldState.SceneName);

                if (_lastWorldState.Scene != null)
                {
                    EditorGUILayout.LabelField("Root Objects:", (_lastWorldState.Scene.RootObjects?.Length ?? 0).ToString());
                }

                if (_lastWorldState.Validation != null)
                {
                    var v = _lastWorldState.Validation;
                    var color = v.HasErrors ? "red" : (v.WarningCount > 0 ? "yellow" : "green");
                    EditorGUILayout.LabelField("Validation:", $"<color={color}>{v.Summary}</color>", new GUIStyle(EditorStyles.label) { richText = true });
                }

                if (_lastWorldState.Spatial != null)
                {
                    EditorGUILayout.LabelField("Spatial Issues:", _lastWorldState.Spatial.IssuesFound.ToString());
                }

                if (_lastWorldState.UI != null)
                {
                    EditorGUILayout.LabelField("UI Elements:", _lastWorldState.UI.TotalElements.ToString());
                }
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.LabelField("No capture yet", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(10);

            // Recent snapshots
            EditorGUILayout.LabelField("Recent Snapshots", EditorStyles.boldLabel);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            if (_recentSnapshots.Count > 0)
            {
                foreach (var snapshot in _recentSnapshots)
                {
                    EditorGUILayout.LabelField(snapshot, EditorStyles.miniLabel);
                }
            }
            else
            {
                EditorGUILayout.LabelField("No snapshots yet", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Open hooks folder
            if (GUILayout.Button("Open Hooks Folder"))
            {
                var path = Path.Combine(Application.dataPath, "..", "hooks");
                EditorUtility.RevealInFinder(path);
            }
        }

        #endregion
    }

    /// <summary>
    /// Aggregated world state snapshot.
    /// </summary>
    [Serializable]
    public class WorldState
    {
        public string Timestamp;
        public string SceneName;
        public string Trigger;
        public SceneSnapshot Scene;
        public UIGraph UI;
        public SpatialReport Spatial;
        public ValidationReport Validation;
        public string[] Screenshots;
    }
}
#endif
