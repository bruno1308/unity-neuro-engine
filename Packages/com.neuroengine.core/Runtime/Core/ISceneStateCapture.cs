using System.Threading.Tasks;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for capturing scene state as JSON.
    /// This is Layer 2 (Observation) - the "Eyes" of the engine.
    /// </summary>
    public interface ISceneStateCapture
    {
        /// <summary>
        /// Capture the current scene hierarchy as a JSON-serializable object.
        /// </summary>
        SceneSnapshot CaptureScene();

        /// <summary>
        /// Capture and save to hooks/scenes/{sceneName}/
        /// </summary>
        Task CaptureAndSaveAsync(string sceneName);
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
        public string[] Components;
        public GameObjectSnapshot[] Children;
    }
}
