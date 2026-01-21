using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeuroEngine.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Captures scene hierarchy as JSON-serializable snapshots.
    /// This is the "Eyes" of the engine - Layer 2 Observation.
    /// </summary>
    public class SceneStateCaptureService : ISceneStateCapture
    {
        private readonly IHooksWriter _hooksWriter;

        public SceneStateCaptureService(IHooksWriter hooksWriter)
        {
            _hooksWriter = hooksWriter;
        }

        public SceneSnapshot CaptureScene()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            var snapshot = new SceneSnapshot
            {
                SceneName = scene.name,
                Timestamp = DateTime.UtcNow.ToString("o"),
                RootObjects = new GameObjectSnapshot[rootObjects.Length]
            };

            for (int i = 0; i < rootObjects.Length; i++)
            {
                snapshot.RootObjects[i] = CaptureGameObject(rootObjects[i]);
            }

            return snapshot;
        }

        public async Task CaptureAndSaveAsync(string sceneName)
        {
            var snapshot = CaptureScene();
            var filename = $"{sceneName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            await _hooksWriter.WriteAsync($"scenes/{sceneName}", filename, snapshot);
        }

        private GameObjectSnapshot CaptureGameObject(GameObject go)
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
                snapshot.Children[i] = CaptureGameObject(transform.GetChild(i).gameObject);
            }

            return snapshot;
        }
    }
}
