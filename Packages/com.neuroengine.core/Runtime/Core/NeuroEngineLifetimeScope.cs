using NeuroEngine.Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace NeuroEngine.Core
{
    public class NeuroEngineLifetimeScope : LifetimeScope
    {
        [SerializeField] private bool _logRegistrations = true;

        protected override void Configure(IContainerBuilder builder)
        {
            // Layer 1: Configuration
            builder.Register<EnvConfigService>(Lifetime.Singleton).As<IEnvConfig>();

            // Layer 2: Observation
            builder.Register<SceneStateCaptureService>(Lifetime.Singleton).As<ISceneStateCapture>();

            // Layer 4: Persistence
            builder.Register<HooksWriterService>(Lifetime.Singleton).As<IHooksWriter>();

            // Layer 7: Generative Assets
            // NOTE: Meshy and ElevenLabs HTTP calls are handled by Claude skills/agents,
            // not Unity services. This enables parallelization across multiple agents.
            // See .claude/skills/meshy-generation.md and .claude/skills/elevenlabs.md

            if (_logRegistrations)
            {
                Debug.Log("[NeuroEngine] Core services registered (Layers 1, 2, 4)");
            }
        }
    }
}
