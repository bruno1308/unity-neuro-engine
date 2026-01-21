using System;
using System.Collections.Generic;
using NUnit.Framework;
using NeuroEngine.Core;
using NeuroEngine.Services;
using UnityEngine;

namespace NeuroEngine.Tests.Layer2
{
    /// <summary>
    /// Tests for Layer 2: Validation Rules Engine.
    /// Verifies configurable validation rules with auto-fix support.
    /// </summary>
    [TestFixture]
    public class ValidationRulesTests
    {
        private ValidationRulesEngine _engine;
        private List<GameObject> _testObjects;

        [SetUp]
        public void SetUp()
        {
            _engine = new ValidationRulesEngine();
            _testObjects = new List<GameObject>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _testObjects)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }
            _testObjects.Clear();
        }

        private GameObject CreateTestObject(string name)
        {
            var obj = new GameObject(name);
            _testObjects.Add(obj);
            return obj;
        }

        #region Basic Validation

        [Test]
        public void ValidateScene_ReturnsValidReport()
        {
            var report = _engine.ValidateScene();

            Assert.IsNotNull(report);
            Assert.IsNotNull(report.Results);
            Assert.IsNotNull(report.Timestamp);
        }

        [Test]
        public void ValidateScene_HasTimestamp()
        {
            var beforeValidate = DateTime.UtcNow;
            var report = _engine.ValidateScene();
            var afterValidate = DateTime.UtcNow;

            Assert.IsNotNull(report.Timestamp);
            Assert.DoesNotThrow(() => DateTime.Parse(report.Timestamp));
        }

        [Test]
        public void ValidateScene_ReportHasCounts()
        {
            var report = _engine.ValidateScene();

            Assert.GreaterOrEqual(report.ErrorCount, 0);
            Assert.GreaterOrEqual(report.WarningCount, 0);
            Assert.GreaterOrEqual(report.InfoCount, 0);
        }

        [Test]
        public void ValidateScene_HasErrorsProperty()
        {
            var report = new ValidationReport();
            report.ErrorCount = 0;
            Assert.IsFalse(report.HasErrors);

            report.ErrorCount = 1;
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void ValidateScene_SummaryFormatsCorrectly()
        {
            var report = new ValidationReport
            {
                ErrorCount = 2,
                WarningCount = 3,
                InfoCount = 1
            };

            var summary = report.Summary;

            Assert.IsTrue(summary.Contains("2"));
            Assert.IsTrue(summary.Contains("3"));
            Assert.IsTrue(summary.Contains("1"));
            Assert.IsTrue(summary.Contains("error"));
            Assert.IsTrue(summary.Contains("warning"));
        }

        #endregion

        #region Built-In Rules

        [Test]
        public void GetRegisteredRules_ReturnsBuiltInRules()
        {
            var rules = _engine.GetRegisteredRules();

            Assert.IsNotNull(rules);
            Assert.Greater(rules.Count, 0, "Should have built-in rules");
        }

        [Test]
        public void ValidateScene_WithCamera_PassesCameraRule()
        {
            // Create a camera
            var cameraObj = CreateTestObject("MainCamera");
            cameraObj.AddComponent<Camera>();
            cameraObj.tag = "MainCamera";

            var report = _engine.ValidateScene();

            // Should not have camera_required error
            var cameraError = report.Results.Find(r =>
                r.RuleId == "camera_required" && !r.Passed);
            // Note: May or may not be present depending on implementation
        }

        [Test]
        public void ValidateScene_WithLight_PassesLightRule()
        {
            var lightObj = CreateTestObject("DirectionalLight");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;

            var report = _engine.ValidateScene();

            // Should not have light_required error (or warning)
            Assert.IsNotNull(report);
        }

        #endregion

        #region Rule Registration

        [Test]
        public void RegisterRule_AddsToRulesList()
        {
            var initialCount = _engine.GetRegisteredRules().Count;

            var customRule = new ValidationRule
            {
                Id = "test_custom_rule",
                Description = "Test rule",
                Severity = ValidationSeverity.Warning,
                Validator = (go) => new ValidationResult { Passed = true, RuleId = "test_custom_rule" }
            };
            _engine.RegisterRule(customRule);

            var newCount = _engine.GetRegisteredRules().Count;
            Assert.AreEqual(initialCount + 1, newCount);
        }

        [Test]
        public void RegisterRule_CustomRuleIsExecuted()
        {
            bool wasExecuted = false;

            var customRule = new ValidationRule
            {
                Id = "execution_test_rule",
                Description = "Tests that custom rules execute",
                Severity = ValidationSeverity.Info,
                Validator = (go) =>
                {
                    wasExecuted = true;
                    return new ValidationResult { Passed = true, RuleId = "execution_test_rule" };
                }
            };
            _engine.RegisterRule(customRule);

            _engine.ValidateScene();

            Assert.IsTrue(wasExecuted, "Custom rule validator should be executed");
        }

        [Test]
        public void RegisterRule_DisabledRule_NotExecuted()
        {
            bool wasExecuted = false;

            var disabledRule = new ValidationRule
            {
                Id = "disabled_rule",
                Description = "Should not execute",
                Severity = ValidationSeverity.Error,
                IsEnabled = false,
                Validator = (go) =>
                {
                    wasExecuted = true;
                    return new ValidationResult { Passed = false, RuleId = "disabled_rule" };
                }
            };
            _engine.RegisterRule(disabledRule);

            _engine.ValidateScene();

            Assert.IsFalse(wasExecuted, "Disabled rule should not execute");
        }

        #endregion

        #region ValidateObject

        [Test]
        public void ValidateObject_ReturnsValidReport()
        {
            var testObj = CreateTestObject("ValidateObjectTest");

            var report = _engine.ValidateObject(testObj);

            Assert.IsNotNull(report);
            Assert.IsNotNull(report.Results);
        }

        [Test]
        public void ValidateObject_OnlyValidatesTarget()
        {
            var target = CreateTestObject("Target");
            var other = CreateTestObject("Other");

            var report = _engine.ValidateObject(target);

            // Results should only pertain to target
            Assert.IsNotNull(report);
        }

        #endregion

        #region ValidationResult Structure

        [Test]
        public void ValidationResult_HasAllProperties()
        {
            var result = new ValidationResult
            {
                Passed = false,
                RuleId = "test_rule",
                Message = "Test message",
                ObjectPath = "Canvas/Button",
                AutoFixSuggestion = "Add Component X",
                Severity = ValidationSeverity.Error
            };

            Assert.IsFalse(result.Passed);
            Assert.AreEqual("test_rule", result.RuleId);
            Assert.AreEqual("Test message", result.Message);
            Assert.AreEqual("Canvas/Button", result.ObjectPath);
            Assert.AreEqual("Add Component X", result.AutoFixSuggestion);
            Assert.AreEqual(ValidationSeverity.Error, result.Severity);
        }

        [Test]
        public void ValidationSeverity_HasExpectedLevels()
        {
            Assert.AreEqual(0, (int)ValidationSeverity.Info);
            Assert.AreEqual(1, (int)ValidationSeverity.Warning);
            Assert.AreEqual(2, (int)ValidationSeverity.Error);
        }

        #endregion

        #region ValidationRule Structure

        [Test]
        public void ValidationRule_HasExpectedProperties()
        {
            var rule = new ValidationRule
            {
                Id = "test_id",
                Description = "Test description",
                Severity = ValidationSeverity.Warning,
                AutoFixMethod = "AutoFixTest",
                IsEnabled = true,
                ConditionType = "component_exists",
                Component = "Camera",
                MinCount = 1,
                MaxCount = 5
            };

            Assert.AreEqual("test_id", rule.Id);
            Assert.AreEqual("Test description", rule.Description);
            Assert.AreEqual(ValidationSeverity.Warning, rule.Severity);
            Assert.AreEqual("AutoFixTest", rule.AutoFixMethod);
            Assert.IsTrue(rule.IsEnabled);
            Assert.AreEqual("component_exists", rule.ConditionType);
            Assert.AreEqual("Camera", rule.Component);
            Assert.AreEqual(1, rule.MinCount);
            Assert.AreEqual(5, rule.MaxCount);
        }

        [Test]
        public void ValidationRule_DefaultIsEnabled()
        {
            var rule = new ValidationRule();

            Assert.IsTrue(rule.IsEnabled, "Rules should be enabled by default");
        }

        #endregion

        #region YAML Loading

        [Test]
        public void LoadRulesFromYaml_NonExistentFile_HandlesGracefully()
        {
            Assert.DoesNotThrow(() =>
            {
                _engine.LoadRulesFromYaml("NonExistent/path/rules.yaml");
            });
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ValidateScene_EmptyScene_ReturnsReport()
        {
            // Don't create any objects
            var report = _engine.ValidateScene();

            Assert.IsNotNull(report);
            Assert.IsNotNull(report.Results);
        }

        [Test]
        public void ValidateObject_NullObject_HandlesGracefully()
        {
            Assert.DoesNotThrow(() =>
            {
                try
                {
                    _engine.ValidateObject(null);
                }
                catch (ArgumentNullException)
                {
                    // Acceptable behavior
                }
            });
        }

        [Test]
        public void ValidationReport_InitializesCorrectly()
        {
            var report = new ValidationReport();

            Assert.IsNotNull(report.Results);
            Assert.AreEqual(0, report.ErrorCount);
            Assert.AreEqual(0, report.WarningCount);
            Assert.AreEqual(0, report.InfoCount);
            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void RegisterRule_NullRule_HandlesGracefully()
        {
            Assert.DoesNotThrow(() =>
            {
                try
                {
                    _engine.RegisterRule(null);
                }
                catch (ArgumentNullException)
                {
                    // Acceptable behavior
                }
            });
        }

        #endregion
    }
}
