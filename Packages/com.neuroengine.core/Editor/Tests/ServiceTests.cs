using System;
using System.Threading.Tasks;
using NeuroEngine.Core;
using NeuroEngine.Services;
using UnityEditor;
using UnityEngine;

namespace NeuroEngine.Editor.Tests
{
    /// <summary>
    /// Editor-based tests for runtime services.
    /// Run via NeuroEngine > Run Service Tests menu.
    ///
    /// NOTE: Meshy and ElevenLabs are tested via skills/agents (direct HTTP calls),
    /// not Unity services. This enables parallelization.
    /// </summary>
    public static class ServiceTests
    {
        [MenuItem("NeuroEngine/Run Service Tests")]
        public static async void RunAllTests()
        {
            Debug.Log("=== NeuroEngine Service Tests ===");

            int passed = 0;
            int failed = 0;

            // Test 1: EnvConfigService
            if (TestEnvConfig())
                passed++;
            else
                failed++;

            // Test 2: HooksWriterService
            if (await TestHooksWriter())
                passed++;
            else
                failed++;

            // Test 3: SceneStateCaptureService
            if (await TestSceneCapture())
                passed++;
            else
                failed++;

            Debug.Log($"=== Tests Complete: {passed} passed, {failed} failed ===");
        }

        [MenuItem("NeuroEngine/Test EnvConfig Only")]
        public static void TestEnvConfigMenu()
        {
            TestEnvConfig();
        }

        [MenuItem("NeuroEngine/Test Hooks Writer Only")]
        public static async void TestHooksWriterMenu()
        {
            await TestHooksWriter();
        }

        [MenuItem("NeuroEngine/Test Scene Capture Only")]
        public static async void TestSceneCaptureMenu()
        {
            await TestSceneCapture();
        }

        private static bool TestEnvConfig()
        {
            Debug.Log("[Test] EnvConfigService...");
            try
            {
                var config = new EnvConfigService();

                Debug.Log($"  MeshyApiKey: {(string.IsNullOrEmpty(config.MeshyApiKey) ? "NOT SET" : "***" + config.MeshyApiKey.Substring(Math.Max(0, config.MeshyApiKey.Length - 4)))}");
                Debug.Log($"  ElevenLabsApiKey: {(string.IsNullOrEmpty(config.ElevenLabsApiKey) ? "NOT SET" : "***" + config.ElevenLabsApiKey.Substring(Math.Max(0, config.ElevenLabsApiKey.Length - 4)))}");
                Debug.Log($"  GeminiApiKey: {(string.IsNullOrEmpty(config.GeminiApiKey) ? "NOT SET" : "***" + config.GeminiApiKey.Substring(Math.Max(0, config.GeminiApiKey.Length - 4)))}");
                Debug.Log($"  HooksPath: {config.HooksPath}");
                Debug.Log($"  IsConfigured: {config.IsConfigured}");

                if (!config.IsConfigured)
                {
                    Debug.LogWarning("[Test] EnvConfigService: PARTIAL - Some keys not configured");
                    return true; // Still pass, just warn
                }

                Debug.Log("[Test] EnvConfigService: PASSED");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Test] EnvConfigService: FAILED - {e.Message}");
                return false;
            }
        }

        private static async Task<bool> TestHooksWriter()
        {
            Debug.Log("[Test] HooksWriterService...");
            try
            {
                var config = new EnvConfigService();
                var writer = new HooksWriterService(config);

                // Write a test file
                var testData = new TestPayload
                {
                    message = "Test from ServiceTests",
                    timestamp = DateTime.UtcNow.ToString("o"),
                    value = 42
                };

                await writer.WriteAsync("validation", "service_test.json", testData);

                // Verify it exists
                if (!writer.Exists("validation", "service_test.json"))
                {
                    Debug.LogError("[Test] HooksWriterService: FAILED - File not created");
                    return false;
                }

                // Read it back
                var readBack = await writer.ReadAsync<TestPayload>("validation", "service_test.json");
                if (readBack == null || readBack.value != 42)
                {
                    Debug.LogError("[Test] HooksWriterService: FAILED - Read back mismatch");
                    return false;
                }

                Debug.Log($"  Written to: hooks/validation/service_test.json");
                Debug.Log("[Test] HooksWriterService: PASSED");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Test] HooksWriterService: FAILED - {e.Message}");
                return false;
            }
        }

        private static async Task<bool> TestSceneCapture()
        {
            Debug.Log("[Test] SceneStateCaptureService...");
            try
            {
                var config = new EnvConfigService();
                var writer = new HooksWriterService(config);
                var capture = new SceneStateCaptureService(writer);

                // Capture current scene
                var snapshot = capture.CaptureScene();

                Debug.Log($"  Scene: {snapshot.SceneName}");
                Debug.Log($"  Root objects: {snapshot.RootObjects.Length}");
                Debug.Log($"  Timestamp: {snapshot.Timestamp}");

                // List root objects
                foreach (var obj in snapshot.RootObjects)
                {
                    Debug.Log($"    - {obj.Name} ({obj.Components.Length} components)");
                }

                // Save to hooks
                await capture.CaptureAndSaveAsync(snapshot.SceneName);
                Debug.Log($"  Saved to: hooks/scenes/{snapshot.SceneName}/");

                Debug.Log("[Test] SceneStateCaptureService: PASSED");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Test] SceneStateCaptureService: FAILED - {e.Message}");
                return false;
            }
        }

        [Serializable]
        private class TestPayload
        {
            public string message;
            public string timestamp;
            public int value;
        }
    }
}
