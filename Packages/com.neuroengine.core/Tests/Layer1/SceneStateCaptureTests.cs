using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using NeuroEngine.Core;
using NeuroEngine.Services;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace NeuroEngine.Tests.Layer1
{
    /// <summary>
    /// Tests for Layer 1: Scene State Capture Service.
    /// Verifies that Unity scene state is properly captured as JSON-serializable data.
    /// </summary>
    [TestFixture]
    public class SceneStateCaptureTests
    {
        private SceneStateCaptureService _service;
        private List<GameObject> _testObjects;

        [SetUp]
        public void SetUp()
        {
            _service = new SceneStateCaptureService();
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

        private GameObject CreateTestObject(string name, GameObject parent = null)
        {
            var obj = new GameObject(name);
            if (parent != null)
                obj.transform.SetParent(parent.transform);
            _testObjects.Add(obj);
            return obj;
        }

        #region Basic Hierarchy Capture

        [Test]
        public void CaptureScene_ReturnsValidSnapshot()
        {
            var snapshot = _service.CaptureScene();

            Assert.IsNotNull(snapshot);
            Assert.IsNotNull(snapshot.SceneName);
            Assert.IsNotNull(snapshot.Timestamp);
            Assert.IsNotNull(snapshot.RootObjects);
        }

        [Test]
        public void CaptureScene_IncludesTimestamp()
        {
            var beforeCapture = DateTime.UtcNow;
            var snapshot = _service.CaptureScene();
            var afterCapture = DateTime.UtcNow;

            Assert.IsNotNull(snapshot.Timestamp);
            Assert.DoesNotThrow(() => DateTime.Parse(snapshot.Timestamp));

            var captureTime = DateTime.Parse(snapshot.Timestamp);
            Assert.GreaterOrEqual(captureTime, beforeCapture.AddSeconds(-1));
            Assert.LessOrEqual(captureTime, afterCapture.AddSeconds(1));
        }

        [Test]
        public void CaptureScene_CapturesRootObject()
        {
            var testObj = CreateTestObject("TestRootObject");
            testObj.transform.position = new Vector3(1, 2, 3);

            var snapshot = _service.CaptureScene();

            var found = Array.Find(snapshot.RootObjects, r => r.Name == "TestRootObject");
            Assert.IsNotNull(found, "Should capture the test root object");
            Assert.AreEqual("TestRootObject", found.Name);
        }

        [Test]
        public void CaptureScene_CapturesTransformPosition()
        {
            var testObj = CreateTestObject("PositionTest");
            testObj.transform.position = new Vector3(10f, 20f, 30f);

            var snapshot = _service.CaptureScene();

            var found = Array.Find(snapshot.RootObjects, r => r.Name == "PositionTest");
            Assert.IsNotNull(found);
            Assert.AreEqual(3, found.Position.Length);
            Assert.AreEqual(10f, found.Position[0], 0.001f);
            Assert.AreEqual(20f, found.Position[1], 0.001f);
            Assert.AreEqual(30f, found.Position[2], 0.001f);
        }

        [Test]
        public void CaptureScene_CapturesTransformRotation()
        {
            var testObj = CreateTestObject("RotationTest");
            testObj.transform.eulerAngles = new Vector3(45f, 90f, 180f);

            var snapshot = _service.CaptureScene();

            var found = Array.Find(snapshot.RootObjects, r => r.Name == "RotationTest");
            Assert.IsNotNull(found);
            Assert.AreEqual(3, found.Rotation.Length);
            Assert.AreEqual(45f, found.Rotation[0], 0.1f);
            Assert.AreEqual(90f, found.Rotation[1], 0.1f);
            Assert.AreEqual(180f, found.Rotation[2], 0.1f);
        }

        [Test]
        public void CaptureScene_CapturesTransformScale()
        {
            var testObj = CreateTestObject("ScaleTest");
            testObj.transform.localScale = new Vector3(2f, 3f, 4f);

            var snapshot = _service.CaptureScene();

            var found = Array.Find(snapshot.RootObjects, r => r.Name == "ScaleTest");
            Assert.IsNotNull(found);
            Assert.AreEqual(3, found.Scale.Length);
            Assert.AreEqual(2f, found.Scale[0], 0.001f);
            Assert.AreEqual(3f, found.Scale[1], 0.001f);
            Assert.AreEqual(4f, found.Scale[2], 0.001f);
        }

        [Test]
        public void CaptureScene_CapturesActiveState()
        {
            var activeObj = CreateTestObject("ActiveObject");
            activeObj.SetActive(true);

            var inactiveObj = CreateTestObject("InactiveObject");
            inactiveObj.SetActive(false);

            var snapshot = _service.CaptureScene();

            var foundActive = Array.Find(snapshot.RootObjects, r => r.Name == "ActiveObject");
            var foundInactive = Array.Find(snapshot.RootObjects, r => r.Name == "InactiveObject");

            Assert.IsNotNull(foundActive);
            Assert.IsTrue(foundActive.Active);

            Assert.IsNotNull(foundInactive);
            Assert.IsFalse(foundInactive.Active);
        }

        [Test]
        public void CaptureScene_CapturesTagAndLayer()
        {
            var testObj = CreateTestObject("TagLayerTest");
            testObj.tag = "MainCamera"; // Use built-in tag
            testObj.layer = 5; // UI layer

            var snapshot = _service.CaptureScene();

            var found = Array.Find(snapshot.RootObjects, r => r.Name == "TagLayerTest");
            Assert.IsNotNull(found);
            Assert.AreEqual("MainCamera", found.Tag);
            Assert.AreEqual(5, found.Layer);
        }

        #endregion

        #region Hierarchy Depth

        [Test]
        public void CaptureScene_CapturesChildren()
        {
            var parent = CreateTestObject("Parent");
            var child = CreateTestObject("Child", parent);

            var snapshot = _service.CaptureScene();

            var foundParent = Array.Find(snapshot.RootObjects, r => r.Name == "Parent");
            Assert.IsNotNull(foundParent);
            Assert.IsNotNull(foundParent.Children);
            Assert.AreEqual(1, foundParent.Children.Length);
            Assert.AreEqual("Child", foundParent.Children[0].Name);
        }

        [Test]
        public void CaptureScene_CapturesDeepHierarchy()
        {
            // Create 5-level deep hierarchy
            var root = CreateTestObject("Root");
            var level1 = CreateTestObject("Level1", root);
            var level2 = CreateTestObject("Level2", level1);
            var level3 = CreateTestObject("Level3", level2);
            var level4 = CreateTestObject("Level4", level3);

            var snapshot = _service.CaptureScene();

            var foundRoot = Array.Find(snapshot.RootObjects, r => r.Name == "Root");
            Assert.IsNotNull(foundRoot);

            var foundL1 = foundRoot.Children[0];
            Assert.AreEqual("Level1", foundL1.Name);

            var foundL2 = foundL1.Children[0];
            Assert.AreEqual("Level2", foundL2.Name);

            var foundL3 = foundL2.Children[0];
            Assert.AreEqual("Level3", foundL3.Name);

            var foundL4 = foundL3.Children[0];
            Assert.AreEqual("Level4", foundL4.Name);
        }

        [Test]
        public void CaptureScene_WithMaxDepth_LimitsCapture()
        {
            var root = CreateTestObject("Root");
            var level1 = CreateTestObject("Level1", root);
            var level2 = CreateTestObject("Level2", level1);
            var level3 = CreateTestObject("Level3", level2);

            var options = new SceneCaptureOptions { MaxDepth = 1 };
            var snapshot = _service.CaptureScene(options);

            var foundRoot = Array.Find(snapshot.RootObjects, r => r.Name == "Root");
            Assert.IsNotNull(foundRoot);

            // MaxDepth=1 should capture root (depth 0) and immediate children (depth 1)
            Assert.IsNotNull(foundRoot.Children);
            Assert.AreEqual(1, foundRoot.Children.Length);
            Assert.AreEqual("Level1", foundRoot.Children[0].Name);

            // Level1's children should be empty (depth 2 is beyond MaxDepth=1)
            Assert.IsNotNull(foundRoot.Children[0].Children);
            Assert.AreEqual(0, foundRoot.Children[0].Children.Length);
        }

        [Test]
        public void CaptureScene_WithMaxDepthZero_CapturesOnlyRoots()
        {
            var root = CreateTestObject("Root");
            var child = CreateTestObject("Child", root);

            var options = new SceneCaptureOptions { MaxDepth = 0 };
            var snapshot = _service.CaptureScene(options);

            var foundRoot = Array.Find(snapshot.RootObjects, r => r.Name == "Root");
            Assert.IsNotNull(foundRoot);
            Assert.AreEqual(0, foundRoot.Children.Length, "Children should not be captured with MaxDepth=0");
        }

        #endregion

        #region Component Capture

        [Test]
        public void CaptureScene_CapturesComponentNames()
        {
            var testObj = CreateTestObject("ComponentTest");
            testObj.AddComponent<BoxCollider>();
            testObj.AddComponent<Rigidbody>();

            var snapshot = _service.CaptureScene();

            var found = Array.Find(snapshot.RootObjects, r => r.Name == "ComponentTest");
            Assert.IsNotNull(found);
            Assert.IsNotNull(found.Components);
            Assert.Contains("Transform", found.Components);
            Assert.Contains("BoxCollider", found.Components);
            Assert.Contains("Rigidbody", found.Components);
        }

        [Test]
        public void CaptureScene_WithExcludeComponents_FiltersComponents()
        {
            var testObj = CreateTestObject("FilterTest");
            testObj.AddComponent<BoxCollider>();
            testObj.AddComponent<Rigidbody>();

            var options = new SceneCaptureOptions
            {
                IncludeComponentData = true,
                ExcludeComponents = new List<string> { "Transform", "BoxCollider" }
            };
            var snapshot = _service.CaptureScene(options);

            var found = Array.Find(snapshot.RootObjects, r => r.Name == "FilterTest");
            Assert.IsNotNull(found);

            // ComponentData should not include Transform or BoxCollider
            if (found.ComponentData != null)
            {
                foreach (var comp in found.ComponentData)
                {
                    Assert.AreNotEqual("Transform", comp.Type);
                    Assert.AreNotEqual("BoxCollider", comp.Type);
                }
            }
        }

        [Test]
        public void CaptureScene_CapturesRigidbodyFields()
        {
            var testObj = CreateTestObject("RigidbodyTest");
            var rb = testObj.AddComponent<Rigidbody>();
            rb.mass = 5.5f;
            rb.useGravity = false;

            var options = new SceneCaptureOptions
            {
                IncludeComponentData = true,
                ExcludeComponents = new List<string>() // Capture all
            };
            var snapshot = _service.CaptureScene(options);

            var found = Array.Find(snapshot.RootObjects, r => r.Name == "RigidbodyTest");
            Assert.IsNotNull(found);
            Assert.IsNotNull(found.ComponentData);

            var rbData = Array.Find(found.ComponentData, c => c.Type == "Rigidbody");
            Assert.IsNotNull(rbData, "Rigidbody component data should be captured");
            Assert.IsTrue(rbData.Fields.ContainsKey("mass"));
            Assert.AreEqual(5.5f, Convert.ToSingle(rbData.Fields["mass"]), 0.001f);
        }

        [Test]
        public void CaptureScene_WithoutComponentData_SkipsFieldCapture()
        {
            var testObj = CreateTestObject("NoDataTest");
            testObj.AddComponent<Rigidbody>();

            var options = new SceneCaptureOptions { IncludeComponentData = false };
            var snapshot = _service.CaptureScene(options);

            var found = Array.Find(snapshot.RootObjects, r => r.Name == "NoDataTest");
            Assert.IsNotNull(found);

            // Components array should still be populated
            Assert.Contains("Rigidbody", found.Components);

            // ComponentData should be null or empty
            Assert.IsTrue(found.ComponentData == null || found.ComponentData.Length == 0);
        }

        #endregion

        #region Counting

        [Test]
        public void CaptureScene_CountsTotalObjects()
        {
            var root = CreateTestObject("Root");
            CreateTestObject("Child1", root);
            CreateTestObject("Child2", root);

            var snapshot = _service.CaptureScene();

            // Count should include root + 2 children (plus any other scene objects)
            Assert.GreaterOrEqual(snapshot.TotalObjectCount, 3);
        }

        [Test]
        public void CaptureScene_CountsComponentsWithData()
        {
            var testObj = CreateTestObject("CountTest");
            testObj.AddComponent<BoxCollider>();
            testObj.AddComponent<Rigidbody>();

            var options = new SceneCaptureOptions
            {
                IncludeComponentData = true,
                ExcludeComponents = new List<string> { "Transform" }
            };
            var snapshot = _service.CaptureScene(options);

            // Should have captured data for BoxCollider and Rigidbody
            Assert.GreaterOrEqual(snapshot.TotalComponentsWithData, 2);
        }

        #endregion

        #region Edge Cases

        [Test]
        public void CaptureScene_HandlesMultipleChildren()
        {
            var parent = CreateTestObject("Parent");
            CreateTestObject("Child1", parent);
            CreateTestObject("Child2", parent);
            CreateTestObject("Child3", parent);

            var snapshot = _service.CaptureScene();

            var foundParent = Array.Find(snapshot.RootObjects, r => r.Name == "Parent");
            Assert.IsNotNull(foundParent);
            Assert.AreEqual(3, foundParent.Children.Length);
        }

        [Test]
        public void CaptureScene_HandlesEmptyScene()
        {
            // Just capture without adding test objects
            var service = new SceneStateCaptureService();
            var snapshot = service.CaptureScene();

            Assert.IsNotNull(snapshot);
            Assert.IsNotNull(snapshot.RootObjects);
            // There may be other objects in the scene, but it shouldn't crash
        }

        [Test]
        public void CaptureScene_SceneNameMatchesActiveScene()
        {
            var snapshot = _service.CaptureScene();
            var activeSceneName = SceneManager.GetActiveScene().name;

            Assert.AreEqual(activeSceneName, snapshot.SceneName);
        }

        #endregion
    }
}
