using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.IO;

namespace NeuroEngine.Editor
{
    /// <summary>
    /// UI-related editor tools for creating and managing UI Toolkit assets.
    /// </summary>
    public static class UITools
    {
        private const string PanelSettingsPath = "Assets/Iteration2/UI/PanelSettings.asset";

        [MenuItem("Neuro-Engine/UI/Create PanelSettings")]
        public static void CreatePanelSettings()
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(PanelSettingsPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            // Check if asset already exists
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null)
            {
                Debug.Log($"[UITools] PanelSettings already exists at: {PanelSettingsPath}");
                Selection.activeObject = existing;
                return;
            }

            // Create new PanelSettings
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            
            // Configure sensible defaults
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            
            // Save the asset
            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[UITools] Created PanelSettings at: {PanelSettingsPath}");
            Selection.activeObject = panelSettings;
        }

        [MenuItem("Neuro-Engine/UI/Wire PanelSettings to UIDocument")]
        public static void WirePanelSettingsToUIDocument()
        {
            // Find the PanelSettings asset
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                Debug.LogError($"[UITools] PanelSettings not found at: {PanelSettingsPath}. Create it first.");
                return;
            }

            // Find UIDocument in scene
            var uiDocument = Object.FindFirstObjectByType<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogError("[UITools] No UIDocument found in scene.");
                return;
            }

            // Wire it up
            uiDocument.panelSettings = panelSettings;
            EditorUtility.SetDirty(uiDocument);
            
            Debug.Log($"[UITools] Wired PanelSettings to UIDocument on '{uiDocument.gameObject.name}'");
        }

        [MenuItem("Neuro-Engine/UI/Create And Wire PanelSettings")]
        public static void CreateAndWirePanelSettings()
        {
            CreatePanelSettings();
            WirePanelSettingsToUIDocument();
        }
    }
}
