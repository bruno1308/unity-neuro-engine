using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for capturing scene state as JSON.
    /// This is Layer 1 (Code-First Foundation) - makes Unity state machine-readable.
    /// </summary>
    public interface ISceneStateCapture
    {
        /// <summary>
        /// Capture the current scene hierarchy as a JSON-serializable object.
        /// Includes component field values for full state observability.
        /// </summary>
        SceneSnapshot CaptureScene();

        /// <summary>
        /// Capture with options for controlling depth and detail.
        /// </summary>
        SceneSnapshot CaptureScene(SceneCaptureOptions options);

        /// <summary>
        /// Capture and save to hooks/scenes/{sceneName}/
        /// </summary>
        Task CaptureAndSaveAsync(string sceneName);
    }

    /// <summary>
    /// Options for controlling scene capture behavior.
    /// </summary>
    [System.Serializable]
    public class SceneCaptureOptions
    {
        /// <summary>
        /// Include component field values (default: true).
        /// Set to false for structure-only capture.
        /// </summary>
        public bool IncludeComponentData = true;

        /// <summary>
        /// Maximum hierarchy depth to capture (-1 = unlimited).
        /// </summary>
        public int MaxDepth = -1;

        /// <summary>
        /// Component types to exclude from data capture (e.g., "Transform", "MeshRenderer").
        /// </summary>
        public List<string> ExcludeComponents = new List<string>();

        /// <summary>
        /// Only capture components from these namespaces (empty = all).
        /// Useful for focusing on game logic vs Unity internals.
        /// </summary>
        public List<string> IncludeNamespaces = new List<string>();
    }

    /// <summary>
    /// Represents a snapshot of a scene's state.
    /// </summary>
    [System.Serializable]
    public class SceneSnapshot
    {
        public string SceneName;
        public string Timestamp;
        public GameObjectSnapshot[] RootObjects;

        /// <summary>
        /// Total number of GameObjects in the snapshot.
        /// </summary>
        public int TotalObjectCount;

        /// <summary>
        /// Total number of components captured with data.
        /// </summary>
        public int TotalComponentsWithData;
    }

    /// <summary>
    /// Represents a snapshot of a GameObject's state.
    /// </summary>
    [System.Serializable]
    public class GameObjectSnapshot
    {
        public string Name;
        public bool Active;
        public string Tag;
        public int Layer;
        public float[] Position;
        public float[] Rotation;
        public float[] Scale;

        /// <summary>
        /// Component names only (for backwards compatibility).
        /// </summary>
        public string[] Components;

        /// <summary>
        /// Full component data including field values.
        /// </summary>
        public ComponentSnapshot[] ComponentData;

        public GameObjectSnapshot[] Children;
    }

    /// <summary>
    /// Represents a snapshot of a component's state including field values.
    /// This enables AI to query actual game state, not just structure.
    /// </summary>
    [System.Serializable]
    public class ComponentSnapshot
    {
        /// <summary>
        /// Component type name (e.g., "Health", "PlayerController").
        /// </summary>
        public string Type;

        /// <summary>
        /// Full type name including namespace.
        /// </summary>
        public string FullType;

        /// <summary>
        /// Whether the component is enabled (for Behaviours).
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// Serialized field values as key-value pairs.
        /// Values are JSON-compatible types (string, number, bool, array, object).
        /// </summary>
        public Dictionary<string, object> Fields = new Dictionary<string, object>();
    }
}
