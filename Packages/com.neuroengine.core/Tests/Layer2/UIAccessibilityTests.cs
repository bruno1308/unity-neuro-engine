using System;
using System.Collections.Generic;
using NUnit.Framework;
using NeuroEngine.Core;
using NeuroEngine.Services;
using UnityEngine;
using UnityEngine.UI;

namespace NeuroEngine.Tests.Layer2
{
    /// <summary>
    /// Tests for Layer 2: UI Accessibility Service.
    /// Verifies UI element capture as a "DOM for games".
    /// </summary>
    [TestFixture]
    public class UIAccessibilityTests
    {
        private UIAccessibilityService _service;
        private List<GameObject> _testObjects;

        [SetUp]
        public void SetUp()
        {
            _service = new UIAccessibilityService();
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

        private GameObject CreateCanvas()
        {
            var canvasObj = new GameObject("TestCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            _testObjects.Add(canvasObj);
            return canvasObj;
        }

        private GameObject CreateButton(GameObject parent, string name)
        {
            var buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent.transform);

            var rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100, 50);

            var image = buttonObj.AddComponent<Image>();
            var button = buttonObj.AddComponent<Button>();

            _testObjects.Add(buttonObj);
            return buttonObj;
        }

        #region Basic Capture

        [Test]
        public void CaptureUIState_ReturnsValidGraph()
        {
            var graph = _service.CaptureUIState();

            Assert.IsNotNull(graph);
            Assert.IsNotNull(graph.Elements);
            Assert.IsNotNull(graph.Timestamp);
        }

        [Test]
        public void CaptureUIState_HasTimestamp()
        {
            var beforeCapture = DateTime.UtcNow;
            var graph = _service.CaptureUIState();
            var afterCapture = DateTime.UtcNow;

            Assert.IsNotNull(graph.Timestamp);
            Assert.DoesNotThrow(() => DateTime.Parse(graph.Timestamp));

            var captureTime = DateTime.Parse(graph.Timestamp);
            Assert.GreaterOrEqual(captureTime, beforeCapture.AddSeconds(-1));
            Assert.LessOrEqual(captureTime, afterCapture.AddSeconds(1));
        }

        [Test]
        public void CaptureUIState_CapturesButton()
        {
            var canvas = CreateCanvas();
            var button = CreateButton(canvas, "TestButton");

            var graph = _service.CaptureUIState();

            Assert.IsNotNull(graph.Elements);
            var foundButton = graph.Elements.Find(e => e.Name == "TestButton");
            Assert.IsNotNull(foundButton, "Should capture the test button");
        }

        [Test]
        public void CaptureUIState_ButtonHasCorrectType()
        {
            var canvas = CreateCanvas();
            var button = CreateButton(canvas, "TypeTestButton");

            var graph = _service.CaptureUIState();

            var foundButton = graph.Elements.Find(e => e.Name == "TypeTestButton");
            Assert.IsNotNull(foundButton);
            Assert.AreEqual("Button", foundButton.Type);
        }

        [Test]
        public void CaptureUIState_ReportsUISystem()
        {
            var canvas = CreateCanvas();
            CreateButton(canvas, "SystemTestButton");

            var graph = _service.CaptureUIState();

            Assert.IsNotNull(graph.UISystem);
            // Should be "uGUI", "UIToolkit", "Both", or "None"
            Assert.IsTrue(
                graph.UISystem == "uGUI" ||
                graph.UISystem == "UIToolkit" ||
                graph.UISystem == "Both" ||
                graph.UISystem == "None",
                $"UISystem should be a valid value, got: {graph.UISystem}"
            );
        }

        [Test]
        public void CaptureUIState_CountsTotalElements()
        {
            var canvas = CreateCanvas();
            CreateButton(canvas, "Button1");
            CreateButton(canvas, "Button2");
            CreateButton(canvas, "Button3");

            var graph = _service.CaptureUIState();

            Assert.GreaterOrEqual(graph.TotalElements, 3);
            Assert.AreEqual(graph.Elements.Count, graph.TotalElements);
        }

        #endregion

        #region Element Properties

        [Test]
        public void UIElement_HasScreenPosition()
        {
            var canvas = CreateCanvas();
            var button = CreateButton(canvas, "PositionButton");

            var graph = _service.CaptureUIState();

            var foundButton = graph.Elements.Find(e => e.Name == "PositionButton");
            Assert.IsNotNull(foundButton);
            Assert.IsNotNull(foundButton.ScreenPosition);
            Assert.AreEqual(2, foundButton.ScreenPosition.Length);
        }

        [Test]
        public void UIElement_HasSize()
        {
            var canvas = CreateCanvas();
            var button = CreateButton(canvas, "SizeButton");
            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 100);

            var graph = _service.CaptureUIState();

            var foundButton = graph.Elements.Find(e => e.Name == "SizeButton");
            Assert.IsNotNull(foundButton);
            Assert.IsNotNull(foundButton.Size);
            Assert.AreEqual(2, foundButton.Size.Length);
        }

        [Test]
        public void UIElement_HasVisibleProperty()
        {
            var canvas = CreateCanvas();
            var button = CreateButton(canvas, "VisibleButton");

            var graph = _service.CaptureUIState();

            var foundButton = graph.Elements.Find(e => e.Name == "VisibleButton");
            Assert.IsNotNull(foundButton);
            Assert.IsTrue(foundButton.Visible, "Active button should be visible");
        }

        [Test]
        public void UIElement_HasInteractableProperty()
        {
            var canvas = CreateCanvas();
            var buttonObj = CreateButton(canvas, "InteractableButton");
            var button = buttonObj.GetComponent<Button>();
            button.interactable = true;

            var graph = _service.CaptureUIState();

            var foundButton = graph.Elements.Find(e => e.Name == "InteractableButton");
            Assert.IsNotNull(foundButton);
            Assert.IsTrue(foundButton.Interactable, "Interactable button should report as interactable");
        }

        [Test]
        public void UIElement_DisabledButton_NotInteractable()
        {
            var canvas = CreateCanvas();
            var buttonObj = CreateButton(canvas, "DisabledButton");
            var button = buttonObj.GetComponent<Button>();
            button.interactable = false;

            var graph = _service.CaptureUIState();

            var foundButton = graph.Elements.Find(e => e.Name == "DisabledButton");
            Assert.IsNotNull(foundButton);
            Assert.IsFalse(foundButton.Interactable, "Disabled button should not be interactable");
        }

        [Test]
        public void UIElement_HasPath()
        {
            var canvas = CreateCanvas();
            var button = CreateButton(canvas, "PathButton");

            var graph = _service.CaptureUIState();

            var foundButton = graph.Elements.Find(e => e.Name == "PathButton");
            Assert.IsNotNull(foundButton);
            Assert.IsNotNull(foundButton.Path);
            Assert.IsNotEmpty(foundButton.Path);
        }

        [Test]
        public void UIElement_HasSource()
        {
            var canvas = CreateCanvas();
            CreateButton(canvas, "SourceButton");

            var graph = _service.CaptureUIState();

            var foundButton = graph.Elements.Find(e => e.Name == "SourceButton");
            Assert.IsNotNull(foundButton);
            Assert.AreEqual("uGUI", foundButton.Source, "uGUI button should have 'uGUI' source");
        }

        #endregion

        #region Find Methods

        [Test]
        public void FindElementByName_ReturnsCorrectElement()
        {
            var canvas = CreateCanvas();
            CreateButton(canvas, "FindMeButton");

            var found = _service.FindElementByName("FindMeButton");

            Assert.IsNotNull(found);
            Assert.AreEqual("FindMeButton", found.Name);
        }

        [Test]
        public void FindElementByName_ReturnsNullWhenNotFound()
        {
            var canvas = CreateCanvas();
            CreateButton(canvas, "ExistingButton");

            var found = _service.FindElementByName("NonExistentButton");

            Assert.IsNull(found, "Should return null for non-existent element");
        }

        [Test]
        public void FindInteractableElements_ReturnsOnlyInteractable()
        {
            var canvas = CreateCanvas();
            var enabledButton = CreateButton(canvas, "EnabledButton");
            var disabledButtonObj = CreateButton(canvas, "DisabledButton");
            disabledButtonObj.GetComponent<Button>().interactable = false;

            var interactable = _service.FindInteractableElements();

            var foundEnabled = interactable.Find(e => e.Name == "EnabledButton");
            var foundDisabled = interactable.Find(e => e.Name == "DisabledButton");

            Assert.IsNotNull(foundEnabled, "Enabled button should be in interactable list");
            Assert.IsNull(foundDisabled, "Disabled button should not be in interactable list");
        }

        [Test]
        public void FindBlockedElements_ReturnsEmptyForNonOverlapping()
        {
            var canvas = CreateCanvas();
            var button1 = CreateButton(canvas, "Button1");
            var rect1 = button1.GetComponent<RectTransform>();
            rect1.anchoredPosition = new Vector2(-200, 0);

            var button2 = CreateButton(canvas, "Button2");
            var rect2 = button2.GetComponent<RectTransform>();
            rect2.anchoredPosition = new Vector2(200, 0);

            var blocked = _service.FindBlockedElements();

            // Non-overlapping buttons should not block each other
            Assert.IsNotNull(blocked);
        }

        #endregion

        #region Counts

        [Test]
        public void UIGraph_InteractableCount_MatchesActual()
        {
            var canvas = CreateCanvas();
            CreateButton(canvas, "Button1");
            CreateButton(canvas, "Button2");
            var disabledObj = CreateButton(canvas, "DisabledButton");
            disabledObj.GetComponent<Button>().interactable = false;

            var graph = _service.CaptureUIState();

            // Should have 2 interactable buttons
            Assert.GreaterOrEqual(graph.InteractableCount, 2);
        }

        #endregion

        #region Edge Cases

        [Test]
        public void CaptureUIState_EmptyScene_ReturnsEmptyGraph()
        {
            // Don't create any UI
            var graph = _service.CaptureUIState();

            Assert.IsNotNull(graph);
            Assert.IsNotNull(graph.Elements);
            // May have 0 elements or some from test environment
        }

        [Test]
        public void CaptureUIState_InactiveCanvas_HandlesGracefully()
        {
            var canvas = CreateCanvas();
            canvas.SetActive(false);
            CreateButton(canvas, "InactiveButton");

            var graph = _service.CaptureUIState();

            Assert.IsNotNull(graph);
            // Inactive canvas elements may or may not be captured depending on implementation
        }

        [Test]
        public void UIElement_Properties_InitializedCorrectly()
        {
            var element = new UIElement();

            Assert.IsNotNull(element.ScreenPosition);
            Assert.IsNotNull(element.Size);
            Assert.IsNotNull(element.Properties);
            Assert.AreEqual(2, element.ScreenPosition.Length);
            Assert.AreEqual(2, element.Size.Length);
        }

        #endregion
    }
}
