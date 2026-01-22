using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

namespace NeuroEngine.Editor
{
    /// <summary>
    /// Custom scene tools that work around MCP path doubling bug.
    /// MCP bug: Creates "Scene.unity/Scene.unity" instead of "Scene.unity"
    /// These tools properly handle scene paths.
    /// </summary>
    public static class SceneTools
    {
        [MenuItem("Neuro-Engine/Scene/Save Current Scene")]
        public static void SaveCurrentScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[SceneTools] Scene has no path. Use 'Save Scene As' first.");
                return;
            }

            // Fix doubled path if present
            string fixedPath = FixScenePath(scene.path);

            if (fixedPath != scene.path)
            {
                Debug.Log($"[SceneTools] Fixing corrupted path: {scene.path} -> {fixedPath}");
            }

            bool success = EditorSceneManager.SaveScene(scene, fixedPath);
            if (success)
            {
                Debug.Log($"[SceneTools] Scene saved to: {fixedPath}");
            }
            else
            {
                Debug.LogError($"[SceneTools] Failed to save scene to: {fixedPath}");
            }
        }

        [MenuItem("Neuro-Engine/Scene/Save Scene As Iteration2Game")]
        public static void SaveSceneAsIteration2Game()
        {
            SaveSceneAs("Assets/Iteration2/Scenes/Iteration2Game.unity");
        }

        public static void SaveSceneAs(string path)
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            var scene = SceneManager.GetActiveScene();
            bool success = EditorSceneManager.SaveScene(scene, path);

            if (success)
            {
                Debug.Log($"[SceneTools] Scene saved to: {path}");
            }
            else
            {
                Debug.LogError($"[SceneTools] Failed to save scene to: {path}");
            }
        }

        [MenuItem("Neuro-Engine/Scene/Create New Scene")]
        public static void CreateNewScene()
        {
            // Create a new empty scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log($"[SceneTools] Created new empty scene: {newScene.name}");
        }

        [MenuItem("Neuro-Engine/Scene/Fix Scene Path")]
        public static void FixCurrentScenePath()
        {
            var scene = SceneManager.GetActiveScene();
            string currentPath = scene.path;

            if (string.IsNullOrEmpty(currentPath))
            {
                Debug.LogWarning("[SceneTools] Scene has no path set.");
                return;
            }

            string fixedPath = FixScenePath(currentPath);

            if (fixedPath == currentPath)
            {
                Debug.Log("[SceneTools] Scene path is already correct.");
                return;
            }

            Debug.Log($"[SceneTools] Path was corrupted: {currentPath}");
            Debug.Log($"[SceneTools] Saving to fixed path: {fixedPath}");

            // Save to the correct path
            bool success = EditorSceneManager.SaveScene(scene, fixedPath);
            if (success)
            {
                Debug.Log($"[SceneTools] Scene saved with corrected path: {fixedPath}");
            }
        }

        /// <summary>
        /// Fix MCP's path doubling bug: "Scene.unity/Scene.unity" -> "Scene.unity"
        /// </summary>
        private static string FixScenePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            // Check for doubled .unity extension pattern
            // e.g., "Assets/Scenes/MyScene.unity/MyScene.unity"
            if (path.Contains(".unity/"))
            {
                int firstUnity = path.IndexOf(".unity/");
                return path.Substring(0, firstUnity + 6); // Include ".unity"
            }

            return path;
        }

        [MenuItem("Neuro-Engine/Scene/Log Scene Info")]
        public static void LogSceneInfo()
        {
            var scene = SceneManager.GetActiveScene();
            Debug.Log($"[SceneTools] Scene Name: {scene.name}");
            Debug.Log($"[SceneTools] Scene Path: {scene.path}");
            Debug.Log($"[SceneTools] Is Dirty: {scene.isDirty}");
            Debug.Log($"[SceneTools] Is Loaded: {scene.isLoaded}");
            Debug.Log($"[SceneTools] Root Count: {scene.rootCount}");

            string fixedPath = FixScenePath(scene.path);
            if (fixedPath != scene.path)
            {
                Debug.LogWarning($"[SceneTools] PATH IS CORRUPTED! Fixed would be: {fixedPath}");
            }
        }
    }
}
