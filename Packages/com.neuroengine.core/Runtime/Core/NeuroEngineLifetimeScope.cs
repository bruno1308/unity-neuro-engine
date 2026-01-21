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

            // Layer 2: Observation (Eyes)
            builder.Register<SceneStateCaptureService>(Lifetime.Singleton).As<ISceneStateCapture>();
            builder.Register<MissingReferenceDetector>(Lifetime.Singleton).As<IMissingReferenceDetector>();
            builder.Register<UIAccessibilityService>(Lifetime.Singleton).As<IUIAccessibility>();
            builder.Register<SpatialAnalysisService>(Lifetime.Singleton).As<ISpatialAnalysis>();
            builder.Register<ValidationRulesEngine>(Lifetime.Singleton).As<IValidationRules>();

            // Layer 3: Interaction (Hands)
            builder.Register<InputSimulationService>(Lifetime.Singleton).As<IInputSimulation>();

            // Layer 4: Persistence (Memory)
            builder.Register<HooksWriterService>(Lifetime.Singleton).As<IHooksWriter>();
            builder.Register<TranscriptWriterService>(Lifetime.Singleton).As<ITranscriptWriter>();
            builder.Register<TaskManagerService>(Lifetime.Singleton).As<ITaskManager>();

            // Layer 5: Evaluation (Judgment)
            builder.Register<SyntacticGraderService>(Lifetime.Singleton).As<ISyntacticGrader>();
            builder.Register<StateGraderService>(Lifetime.Singleton).As<IStateGrader>();
            builder.Register<PolishGraderService>(Lifetime.Singleton).As<IPolishGrader>();
            // TODO: Register additional graders as they are implemented
            // builder.Register<BehavioralGraderService>(Lifetime.Singleton).As<IBehavioralGrader>();
            // builder.Register<VisualGraderService>(Lifetime.Singleton).As<IVisualGrader>();
            // builder.Register<QualityGraderService>(Lifetime.Singleton).As<IQualityGrader>();

            // Layer 6: Agent Orchestration (Governance)
            // TODO: Register orchestration services when ready
            // builder.Register<OrchestrationService>(Lifetime.Singleton).As<IOrchestration>();
            // builder.Register<SafetyControlService>(Lifetime.Singleton).As<ISafetyControl>();
            // builder.Register<ConvoyService>(Lifetime.Singleton).As<IConvoyService>();

            // Layer 7: Generative Assets (Creation)
            // Style enforcement and asset tracking services
            builder.Register<StyleGuideService>(Lifetime.Singleton).As<IStyleGuide>();
            builder.Register<AssetRegistryService>(Lifetime.Singleton).As<IAssetRegistry>();
            // NOTE: Meshy and ElevenLabs HTTP calls are handled by Claude skills/agents,
            // not Unity services. This enables parallelization across multiple agents.
            // See .claude/skills/meshy-generation.md and .claude/skills/elevenlabs.md

            if (_logRegistrations)
            {
                Debug.Log("[NeuroEngine] Core services registered (Layers 1-7)");
            }
        }
    }
}
