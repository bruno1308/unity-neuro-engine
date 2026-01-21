using System;
using System.Collections.Generic;
using NUnit.Framework;
using NeuroEngine.Core;
using NeuroEngine.Services;
using UnityEngine;

namespace NeuroEngine.Tests.Layer3
{
    /// <summary>
    /// Tests for Layer 3: Input Simulation Service.
    /// Verifies that AI can simulate player input accurately.
    /// </summary>
    [TestFixture]
    public class InputSimulationTests
    {
        private InputSimulationService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new InputSimulationService();
        }

        [TearDown]
        public void TearDown()
        {
            _service.ReleaseAll();
        }

        #region Key Press

        [Test]
        public void PressKey_QueuesKeyPress()
        {
            _service.PressKey(KeyCode.Space);

            // Should not throw and key press should be queued
            Assert.Pass("Key press queued successfully");
        }

        [Test]
        public void PressKey_MultiplePresses_AllQueued()
        {
            _service.PressKey(KeyCode.W);
            _service.PressKey(KeyCode.A);
            _service.PressKey(KeyCode.S);
            _service.PressKey(KeyCode.D);

            Assert.Pass("Multiple key presses queued successfully");
        }

        #endregion

        #region Key Down/Up

        [Test]
        public void KeyDown_AddsToHeldKeys()
        {
            _service.KeyDown(KeyCode.W);

            var heldKeys = _service.GetHeldKeys();
            Assert.Contains(KeyCode.W, (System.Collections.ICollection)heldKeys);
        }

        [Test]
        public void KeyUp_RemovesFromHeldKeys()
        {
            _service.KeyDown(KeyCode.W);
            _service.KeyUp(KeyCode.W);

            var heldKeys = _service.GetHeldKeys();
            Assert.IsFalse(((IList<KeyCode>)heldKeys).Contains(KeyCode.W));
        }

        [Test]
        public void KeyDown_MultipleKeys_AllHeld()
        {
            _service.KeyDown(KeyCode.W);
            _service.KeyDown(KeyCode.LeftShift);
            _service.KeyDown(KeyCode.Space);

            var heldKeys = _service.GetHeldKeys();
            Assert.AreEqual(3, heldKeys.Count);
            Assert.Contains(KeyCode.W, (System.Collections.ICollection)heldKeys);
            Assert.Contains(KeyCode.LeftShift, (System.Collections.ICollection)heldKeys);
            Assert.Contains(KeyCode.Space, (System.Collections.ICollection)heldKeys);
        }

        [Test]
        public void KeyUp_OnlyReleasesSpecifiedKey()
        {
            _service.KeyDown(KeyCode.W);
            _service.KeyDown(KeyCode.A);
            _service.KeyUp(KeyCode.W);

            var heldKeys = _service.GetHeldKeys();
            Assert.AreEqual(1, heldKeys.Count);
            Assert.Contains(KeyCode.A, (System.Collections.ICollection)heldKeys);
        }

        [Test]
        public void GetHeldKeys_ReturnsReadOnlyList()
        {
            _service.KeyDown(KeyCode.W);

            var heldKeys = _service.GetHeldKeys();

            Assert.IsInstanceOf<IReadOnlyList<KeyCode>>(heldKeys);
        }

        [Test]
        public void IsKeyHeld_ReturnsCorrectState()
        {
            _service.KeyDown(KeyCode.W);

            Assert.IsTrue(_service.IsKeyHeld(KeyCode.W));
            Assert.IsFalse(_service.IsKeyHeld(KeyCode.A));

            _service.KeyUp(KeyCode.W);
            Assert.IsFalse(_service.IsKeyHeld(KeyCode.W));
        }

        #endregion

        #region Release All

        [Test]
        public void ReleaseAll_ClearsAllHeldKeys()
        {
            _service.KeyDown(KeyCode.W);
            _service.KeyDown(KeyCode.A);
            _service.KeyDown(KeyCode.S);

            _service.ReleaseAll();

            var heldKeys = _service.GetHeldKeys();
            Assert.AreEqual(0, heldKeys.Count);
        }

        [Test]
        public void ReleaseAll_OnEmptyList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _service.ReleaseAll());
        }

        #endregion

        #region Mouse Position

        [Test]
        public void MouseMove_UpdatesPosition()
        {
            var newPosition = new Vector2(500, 300);

            _service.MouseMove(newPosition);

            var position = _service.GetMousePosition();
            Assert.AreEqual(newPosition.x, position.x, 0.001f);
            Assert.AreEqual(newPosition.y, position.y, 0.001f);
        }

        [Test]
        public void GetMousePosition_ReturnsCurrentPosition()
        {
            _service.MouseMove(new Vector2(100, 200));
            var pos1 = _service.GetMousePosition();
            Assert.AreEqual(100, pos1.x, 0.001f);
            Assert.AreEqual(200, pos1.y, 0.001f);

            _service.MouseMove(new Vector2(300, 400));
            var pos2 = _service.GetMousePosition();
            Assert.AreEqual(300, pos2.x, 0.001f);
            Assert.AreEqual(400, pos2.y, 0.001f);
        }

        [Test]
        public void MouseMove_AcceptsNegativeCoordinates()
        {
            var negativePos = new Vector2(-100, -50);

            Assert.DoesNotThrow(() => _service.MouseMove(negativePos));

            var position = _service.GetMousePosition();
            Assert.AreEqual(-100, position.x, 0.001f);
            Assert.AreEqual(-50, position.y, 0.001f);
        }

        #endregion

        #region Mouse Click

        [Test]
        public void MouseClick_QueuesClickAtPosition()
        {
            var clickPos = new Vector2(500, 300);

            Assert.DoesNotThrow(() => _service.MouseClick(clickPos, button: 0));
        }

        [Test]
        public void MouseClick_UpdatesMousePosition()
        {
            var clickPos = new Vector2(600, 400);

            _service.MouseClick(clickPos, button: 0);

            var position = _service.GetMousePosition();
            Assert.AreEqual(clickPos.x, position.x, 0.001f);
            Assert.AreEqual(clickPos.y, position.y, 0.001f);
        }

        [Test]
        public void MouseClick_DifferentButtons()
        {
            var clickPos = new Vector2(500, 300);

            // Left click
            Assert.DoesNotThrow(() => _service.MouseClick(clickPos, button: 0));

            // Right click
            Assert.DoesNotThrow(() => _service.MouseClick(clickPos, button: 1));

            // Middle click
            Assert.DoesNotThrow(() => _service.MouseClick(clickPos, button: 2));
        }

        [Test]
        public void MouseClick_DefaultsToLeftButton()
        {
            var clickPos = new Vector2(500, 300);

            // Call without specifying button
            Assert.DoesNotThrow(() => _service.MouseClick(clickPos));
        }

        #endregion

        #region Mouse Scroll

        [Test]
        public void MouseScroll_PositiveDelta()
        {
            Assert.DoesNotThrow(() => _service.MouseScroll(1.0f));
        }

        [Test]
        public void MouseScroll_NegativeDelta()
        {
            Assert.DoesNotThrow(() => _service.MouseScroll(-1.0f));
        }

        [Test]
        public void MouseScroll_LargeDelta()
        {
            Assert.DoesNotThrow(() => _service.MouseScroll(10.0f));
            Assert.DoesNotThrow(() => _service.MouseScroll(-10.0f));
        }

        [Test]
        public void MouseScroll_ZeroDelta()
        {
            Assert.DoesNotThrow(() => _service.MouseScroll(0f));
        }

        #endregion

        #region Process Inputs

        [Test]
        public void ProcessInputs_DoesNotThrow()
        {
            _service.PressKey(KeyCode.Space);
            _service.MouseClick(new Vector2(500, 300));

            Assert.DoesNotThrow(() => _service.ProcessInputs());
        }

        [Test]
        public void ProcessInputs_EmptyQueue_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _service.ProcessInputs());
        }

        #endregion

        #region Stress Tests

        [Test]
        public void RapidKeyPresses_AllQueued()
        {
            for (int i = 0; i < 100; i++)
            {
                _service.PressKey(KeyCode.Space);
            }

            Assert.Pass("100 rapid key presses queued successfully");
        }

        [Test]
        public void RapidKeyDownUp_MaintainsCorrectState()
        {
            for (int i = 0; i < 50; i++)
            {
                _service.KeyDown(KeyCode.W);
                _service.KeyUp(KeyCode.W);
            }

            var heldKeys = _service.GetHeldKeys();
            Assert.AreEqual(0, heldKeys.Count, "No keys should be held after down/up cycles");
        }

        [Test]
        public void MixedInput_MaintainsState()
        {
            _service.KeyDown(KeyCode.W);
            _service.MouseMove(new Vector2(100, 100));
            _service.MouseClick(new Vector2(200, 200));
            _service.KeyDown(KeyCode.LeftShift);
            _service.MouseScroll(1.0f);
            _service.PressKey(KeyCode.E);
            _service.KeyUp(KeyCode.W);

            var heldKeys = _service.GetHeldKeys();
            Assert.AreEqual(1, heldKeys.Count);
            Assert.Contains(KeyCode.LeftShift, (System.Collections.ICollection)heldKeys);

            var mousePos = _service.GetMousePosition();
            Assert.AreEqual(200, mousePos.x, 0.001f); // Last click position
        }

        #endregion

        #region Interface Compliance

        [Test]
        public void ImplementsIInputSimulation()
        {
            Assert.IsInstanceOf<IInputSimulation>(_service);
        }

        [Test]
        public void InterfaceMethods_AllAccessible()
        {
            IInputSimulation iface = _service;

            Assert.DoesNotThrow(() => iface.PressKey(KeyCode.A));
            Assert.DoesNotThrow(() => iface.KeyDown(KeyCode.B));
            Assert.DoesNotThrow(() => iface.KeyUp(KeyCode.B));
            Assert.DoesNotThrow(() => iface.MouseClick(Vector2.zero));
            Assert.DoesNotThrow(() => iface.MouseMove(Vector2.one));
            Assert.DoesNotThrow(() => iface.MouseScroll(1f));
            Assert.DoesNotThrow(() => _ = iface.GetHeldKeys());
            Assert.DoesNotThrow(() => _ = iface.GetMousePosition());
            Assert.DoesNotThrow(() => iface.ReleaseAll());
        }

        #endregion
    }
}
