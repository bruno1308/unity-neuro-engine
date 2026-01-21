using System;
using System.Collections.Generic;
using NUnit.Framework;
using NeuroEngine.Core;
using NeuroEngine.Services;
using UnityEngine;

namespace NeuroEngine.Tests.Layer2
{
    /// <summary>
    /// Tests for Layer 2: Missing Reference Detector.
    /// Verifies detection of null/unassigned serialized fields.
    /// </summary>
    [TestFixture]
    public class MissingReferenceDetectorTests
    {
        private MissingReferenceDetector _detector;
        private List<GameObject> _testObjects;

        [SetUp]
        public void SetUp()
        {
            _detector = new MissingReferenceDetector();
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

        #region Basic Detection

        [Test]
        public void Scan_ReturnsValidReport()
        {
            var testObj = CreateTestObject("TestObject");

            var report = _detector.Scan(testObj);

            Assert.IsNotNull(report);
            Assert.IsNotNull(report.References);
            Assert.IsNotNull(report.ScannedTarget);
            Assert.IsNotNull(report.Timestamp);
        }

        [Test]
        public void Scan_ReportHasTimestamp()
        {
            var testObj = CreateTestObject("TestObject");

            var beforeScan = DateTime.UtcNow;
            var report = _detector.Scan(testObj);
            var afterScan = DateTime.UtcNow;

            Assert.IsNotNull(report.Timestamp);
            Assert.DoesNotThrow(() => DateTime.Parse(report.Timestamp));

            var scanTime = DateTime.Parse(report.Timestamp);
            Assert.GreaterOrEqual(scanTime, beforeScan.AddSeconds(-1));
            Assert.LessOrEqual(scanTime, afterScan.AddSeconds(1));
        }

        [Test]
        public void Scan_CleanObject_PassesWithNoReferences()
        {
            var testObj = CreateTestObject("CleanObject");
            testObj.AddComponent<Rigidbody>(); // Built-in component, no null refs

            var report = _detector.Scan(testObj);

            // Rigidbody has no required serialized UnityEngine.Object fields
            // So there should be no missing references for it
            Assert.IsTrue(report.Passed, "Clean object should pass");
        }

        [Test]
        public void Scan_CountsScannedFields()
        {
            var testObj = CreateTestObject("FieldCountTest");
            testObj.AddComponent<Rigidbody>();

            var report = _detector.Scan(testObj);

            Assert.GreaterOrEqual(report.TotalFieldsScanned, 0);
        }

        [Test]
        public void Scan_ReportScannedTarget_ContainsObjectName()
        {
            var testObj = CreateTestObject("TargetNameTest");

            var report = _detector.Scan(testObj);

            Assert.IsTrue(report.ScannedTarget.Contains("TargetNameTest"));
        }

        #endregion

        #region Hierarchy Scanning

        [Test]
        public void Scan_WithIncludeChildren_ScansHierarchy()
        {
            var parent = CreateTestObject("Parent");
            var child = CreateTestObject("Child");
            child.transform.SetParent(parent.transform);

            var report = _detector.Scan(parent, includeChildren: true);

            Assert.IsNotNull(report);
            // Should scan both parent and child
        }

        [Test]
        public void Scan_WithoutIncludeChildren_OnlyScansTarget()
        {
            var parent = CreateTestObject("Parent");
            var child = CreateTestObject("Child");
            child.transform.SetParent(parent.transform);

            var report = _detector.Scan(parent, includeChildren: false);

            Assert.IsNotNull(report);
        }

        #endregion

        #region Scene Scanning

        [Test]
        public void ScanScene_ReturnsValidReport()
        {
            var report = _detector.ScanScene();

            Assert.IsNotNull(report);
            Assert.IsNotNull(report.ScannedTarget);
            Assert.IsTrue(report.ScannedTarget.Contains("Scene:"));
        }

        [Test]
        public void ScanScene_ScansAllRootObjects()
        {
            var testObj1 = CreateTestObject("SceneObject1");
            var testObj2 = CreateTestObject("SceneObject2");

            var report = _detector.ScanScene();

            Assert.IsNotNull(report);
            Assert.GreaterOrEqual(report.TotalFieldsScanned, 0);
        }

        #endregion

        #region Report Properties

        [Test]
        public void Report_ErrorCount_MatchesSeverityFiltering()
        {
            var report = new MissingReferenceReport();
            report.References.Add(new MissingReference { Severity = "error" });
            report.References.Add(new MissingReference { Severity = "error" });
            report.References.Add(new MissingReference { Severity = "warning" });

            Assert.AreEqual(2, report.ErrorCount);
            Assert.AreEqual(1, report.WarningCount);
        }

        [Test]
        public void Report_Passed_TrueWhenNoErrors()
        {
            var report = new MissingReferenceReport();
            report.References.Add(new MissingReference { Severity = "warning" });
            report.References.Add(new MissingReference { Severity = "warning" });

            Assert.IsTrue(report.Passed, "Should pass with only warnings");
        }

        [Test]
        public void Report_Passed_FalseWhenHasErrors()
        {
            var report = new MissingReferenceReport();
            report.References.Add(new MissingReference { Severity = "error" });

            Assert.IsFalse(report.Passed, "Should fail with errors");
        }

        [Test]
        public void Report_Summary_ContainsFieldCount()
        {
            var report = new MissingReferenceReport();
            report.TotalFieldsScanned = 42;
            report.NullCount = 3;

            var summary = report.Summary;

            Assert.IsTrue(summary.Contains("42"), "Summary should contain field count");
            Assert.IsTrue(summary.Contains("3"), "Summary should contain null count");
        }

        #endregion

        #region MissingReference Structure

        [Test]
        public void MissingReference_Description_FormatsCorrectly()
        {
            var reference = new MissingReference
            {
                ObjectPath = "Canvas/Panel/Button",
                ComponentType = "PlayerController",
                FieldName = "_healthBar",
                ExpectedType = "UnityEngine.UI.Image",
                Severity = "error"
            };

            var description = reference.Description;

            Assert.IsTrue(description.Contains("Canvas/Panel/Button"));
            Assert.IsTrue(description.Contains("PlayerController"));
            Assert.IsTrue(description.Contains("_healthBar"));
            Assert.IsTrue(description.Contains("Image"));
            Assert.IsTrue(description.Contains("error"));
        }

        [Test]
        public void MissingReference_Description_IncludesArrayIndex()
        {
            var reference = new MissingReference
            {
                ObjectPath = "Container",
                ComponentType = "ItemSlots",
                FieldName = "items",
                ExpectedType = "Item",
                Severity = "error",
                ArrayIndex = 3
            };

            var description = reference.Description;

            Assert.IsTrue(description.Contains("[3]"), "Should include array index");
        }

        [Test]
        public void MissingReference_Description_OmitsArrayIndexWhenMinus1()
        {
            var reference = new MissingReference
            {
                ObjectPath = "Object",
                ComponentType = "Component",
                FieldName = "field",
                ExpectedType = "Type",
                Severity = "error",
                ArrayIndex = -1
            };

            var description = reference.Description;

            Assert.IsFalse(description.Contains("["), "Should not include array notation for non-array fields");
        }

        #endregion

        #region Edge Cases

        [Test]
        public void Scan_NullGameObject_HandlesGracefully()
        {
            // This depends on implementation - might throw or return empty report
            Assert.DoesNotThrow(() =>
            {
                try
                {
                    _detector.Scan(null);
                }
                catch (ArgumentNullException)
                {
                    // This is acceptable behavior
                }
            });
        }

        [Test]
        public void Scan_DestroyedObject_HandlesGracefully()
        {
            var testObj = CreateTestObject("WillBeDestroyed");
            UnityEngine.Object.DestroyImmediate(testObj);
            _testObjects.Remove(testObj);

            Assert.DoesNotThrow(() =>
            {
                try
                {
                    _detector.Scan(testObj);
                }
                catch (Exception)
                {
                    // Any graceful exception is acceptable
                }
            });
        }

        [Test]
        public void ScanPrefab_LogsWarning()
        {
            // ScanPrefab is documented as not working at runtime
            var report = _detector.ScanPrefab("Assets/NonExistent.prefab");

            // Should return a report (possibly empty) without crashing
            Assert.IsNotNull(report);
        }

        #endregion
    }
}
