using System;
using System.Collections;
using NUnit.Framework;
using NeuroEngine.Core;
using NeuroEngine.Services;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace NeuroEngine.Tests.Layer1
{
    /// <summary>
    /// Tests for Layer 1: Dependency Injection (VContainer integration).
    /// Verifies that all services are properly registered and resolvable.
    /// </summary>
    [TestFixture]
    public class DependencyInjectionTests
    {
        private NeuroEngineLifetimeScope _lifetimeScope;
        private GameObject _scopeGameObject;

        [SetUp]
        public void SetUp()
        {
            _scopeGameObject = new GameObject("TestLifetimeScope");
            _lifetimeScope = _scopeGameObject.AddComponent<NeuroEngineLifetimeScope>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scopeGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_scopeGameObject);
            }
        }

        [UnityTest]
        public IEnumerator LifetimeScope_RegistersAllLayer1Services()
        {
            // Wait for Awake to complete
            yield return null;

            var container = _lifetimeScope.Container;
            Assert.IsNotNull(container, "Container should be initialized after Awake");

            // Layer 1: Configuration
            var envConfig = container.Resolve<IEnvConfig>();
            Assert.IsNotNull(envConfig, "IEnvConfig should be registered");
            Assert.IsInstanceOf<EnvConfigService>(envConfig);
        }

        [UnityTest]
        public IEnumerator LifetimeScope_RegistersAllLayer2Services()
        {
            yield return null;

            var container = _lifetimeScope.Container;

            // Layer 2: Observation (Eyes)
            var sceneCapture = container.Resolve<ISceneStateCapture>();
            Assert.IsNotNull(sceneCapture, "ISceneStateCapture should be registered");
            Assert.IsInstanceOf<SceneStateCaptureService>(sceneCapture);

            var missingRefDetector = container.Resolve<IMissingReferenceDetector>();
            Assert.IsNotNull(missingRefDetector, "IMissingReferenceDetector should be registered");

            var uiAccessibility = container.Resolve<IUIAccessibility>();
            Assert.IsNotNull(uiAccessibility, "IUIAccessibility should be registered");

            var spatialAnalysis = container.Resolve<ISpatialAnalysis>();
            Assert.IsNotNull(spatialAnalysis, "ISpatialAnalysis should be registered");

            var validationRules = container.Resolve<IValidationRules>();
            Assert.IsNotNull(validationRules, "IValidationRules should be registered");
        }

        [UnityTest]
        public IEnumerator LifetimeScope_RegistersAllLayer3Services()
        {
            yield return null;

            var container = _lifetimeScope.Container;

            // Layer 3: Interaction (Hands)
            var inputSimulation = container.Resolve<IInputSimulation>();
            Assert.IsNotNull(inputSimulation, "IInputSimulation should be registered");
            Assert.IsInstanceOf<InputSimulationService>(inputSimulation);
        }

        [UnityTest]
        public IEnumerator LifetimeScope_RegistersAllLayer4Services()
        {
            yield return null;

            var container = _lifetimeScope.Container;

            // Layer 4: Persistence (Memory)
            var hooksWriter = container.Resolve<IHooksWriter>();
            Assert.IsNotNull(hooksWriter, "IHooksWriter should be registered");

            var transcriptWriter = container.Resolve<ITranscriptWriter>();
            Assert.IsNotNull(transcriptWriter, "ITranscriptWriter should be registered");

            var taskManager = container.Resolve<ITaskManager>();
            Assert.IsNotNull(taskManager, "ITaskManager should be registered");
        }

        [UnityTest]
        public IEnumerator SingletonServices_ReturnSameInstance()
        {
            yield return null;

            var container = _lifetimeScope.Container;

            // Resolve twice and verify same instance (singleton)
            var config1 = container.Resolve<IEnvConfig>();
            var config2 = container.Resolve<IEnvConfig>();
            Assert.AreSame(config1, config2, "IEnvConfig should be singleton");

            var capture1 = container.Resolve<ISceneStateCapture>();
            var capture2 = container.Resolve<ISceneStateCapture>();
            Assert.AreSame(capture1, capture2, "ISceneStateCapture should be singleton");

            var input1 = container.Resolve<IInputSimulation>();
            var input2 = container.Resolve<IInputSimulation>();
            Assert.AreSame(input1, input2, "IInputSimulation should be singleton");
        }

        [UnityTest]
        public IEnumerator ServiceDependencies_ResolveCorrectly()
        {
            yield return null;

            var container = _lifetimeScope.Container;

            // SceneStateCaptureService depends on IHooksWriter
            // If the dependency injection is correct, resolving ISceneStateCapture should work
            var sceneCapture = container.Resolve<ISceneStateCapture>();
            Assert.DoesNotThrow(() =>
            {
                var snapshot = sceneCapture.CaptureScene();
                Assert.IsNotNull(snapshot);
            }, "SceneStateCapture should work with injected dependencies");
        }
    }
}
