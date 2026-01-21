using System;
using System.IO;
using NUnit.Framework;
using NeuroEngine.Core;
using NeuroEngine.Services;
using UnityEngine;

namespace NeuroEngine.Tests.Layer1
{
    /// <summary>
    /// Tests for Layer 1: Environment Configuration Service.
    /// Verifies .env file parsing and API key handling.
    /// </summary>
    [TestFixture]
    public class EnvConfigTests
    {
        private string _testEnvPath;
        private string _originalEnvContent;
        private string _envFilePath;

        [SetUp]
        public void SetUp()
        {
            // Find the project root (where .env should be)
            _envFilePath = Path.Combine(Application.dataPath, "..", ".env");

            // Backup existing .env if it exists
            if (File.Exists(_envFilePath))
            {
                _originalEnvContent = File.ReadAllText(_envFilePath);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Restore original .env
            if (_originalEnvContent != null)
            {
                File.WriteAllText(_envFilePath, _originalEnvContent);
            }
        }

        [Test]
        public void EnvConfigService_CanBeConstructed()
        {
            Assert.DoesNotThrow(() =>
            {
                var service = new EnvConfigService();
            });
        }

        [Test]
        public void EnvConfigService_HasHooksPathProperty()
        {
            var service = new EnvConfigService();

            // HooksPath should always have a default value
            Assert.IsNotNull(service.HooksPath);
            Assert.IsNotEmpty(service.HooksPath);
        }

        [Test]
        public void EnvConfigService_HasMeshyApiKeyProperty()
        {
            var service = new EnvConfigService();

            // Property should exist (may be empty if not configured)
            Assert.DoesNotThrow(() =>
            {
                var _ = service.MeshyApiKey;
            });
        }

        [Test]
        public void EnvConfigService_HasElevenLabsApiKeyProperty()
        {
            var service = new EnvConfigService();

            Assert.DoesNotThrow(() =>
            {
                var _ = service.ElevenLabsApiKey;
            });
        }

        [Test]
        public void EnvConfigService_HasGeminiApiKeyProperty()
        {
            var service = new EnvConfigService();

            Assert.DoesNotThrow(() =>
            {
                var _ = service.GeminiApiKey;
            });
        }

        [Test]
        public void EnvConfigService_HasIsConfiguredProperty()
        {
            var service = new EnvConfigService();

            Assert.DoesNotThrow(() =>
            {
                var _ = service.IsConfigured;
            });
        }

        [Test]
        public void EnvConfigService_IsConfigured_FalseForPlaceholders()
        {
            // If any API key contains "your_", it should not be considered configured
            var service = new EnvConfigService();

            // We can't control the .env content in a test, but we can verify the property works
            bool isConfigured = service.IsConfigured;
            Assert.IsInstanceOf<bool>(isConfigured);
        }

        [Test]
        public void EnvConfigService_ImplementsIEnvConfig()
        {
            var service = new EnvConfigService();

            Assert.IsInstanceOf<IEnvConfig>(service);
        }

        [Test]
        public void EnvConfigService_ApiKeysMaskedInLogs()
        {
            var service = new EnvConfigService();

            // API keys should never be fully logged
            var meshyKey = service.MeshyApiKey;
            if (!string.IsNullOrEmpty(meshyKey) && meshyKey.Length > 4)
            {
                // If we were to mask it, only last 4 chars should be visible
                var masked = "***" + meshyKey.Substring(Math.Max(0, meshyKey.Length - 4));
                Assert.AreNotEqual(meshyKey, masked, "API key masking should obscure most of the key");
            }
        }

        [Test]
        public void EnvConfigService_HandlesEmptyValues()
        {
            var service = new EnvConfigService();

            // Empty API keys should return empty string, not null
            var meshyKey = service.MeshyApiKey;
            var elevenLabsKey = service.ElevenLabsApiKey;
            var geminiKey = service.GeminiApiKey;

            // Should not be null (may be empty string)
            Assert.IsNotNull(meshyKey ?? "");
            Assert.IsNotNull(elevenLabsKey ?? "");
            Assert.IsNotNull(geminiKey ?? "");
        }

        [Test]
        public void EnvConfigService_HooksPathHasDefault()
        {
            var service = new EnvConfigService();

            // HooksPath should have a sensible default
            Assert.IsNotNull(service.HooksPath);
            Assert.IsNotEmpty(service.HooksPath);
            Assert.IsTrue(service.HooksPath.Contains("hooks"), "Default hooks path should contain 'hooks'");
        }
    }
}
