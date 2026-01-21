using System;
using System.Collections.Generic;
using NeuroEngine.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Simulates player input for automated testing and AI interaction.
    /// Layer 3 (Interaction) - the "Hands" of the AI agent.
    ///
    /// Note: This uses Unity's legacy Input system simulation.
    /// For new Input System, games should use InputTestFixture.
    /// </summary>
    public class InputSimulationService : IInputSimulation
    {
        private readonly HashSet<KeyCode> _heldKeys = new HashSet<KeyCode>();
        private readonly Queue<SimulatedInput> _inputQueue = new Queue<SimulatedInput>();
        private Vector2 _mousePosition;

        public InputSimulationService()
        {
            _mousePosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
        }

        public void PressKey(KeyCode key)
        {
            _inputQueue.Enqueue(new SimulatedInput
            {
                Type = InputType.KeyPress,
                KeyCode = key,
                Timestamp = Time.time
            });
            Debug.Log($"[InputSimulation] Key press queued: {key}");
        }

        public void KeyDown(KeyCode key)
        {
            _heldKeys.Add(key);
            _inputQueue.Enqueue(new SimulatedInput
            {
                Type = InputType.KeyDown,
                KeyCode = key,
                Timestamp = Time.time
            });
            Debug.Log($"[InputSimulation] Key down: {key}");
        }

        public void KeyUp(KeyCode key)
        {
            _heldKeys.Remove(key);
            _inputQueue.Enqueue(new SimulatedInput
            {
                Type = InputType.KeyUp,
                KeyCode = key,
                Timestamp = Time.time
            });
            Debug.Log($"[InputSimulation] Key up: {key}");
        }

        public void MouseClick(Vector2 screenPosition, int button = 0)
        {
            _mousePosition = screenPosition;
            _inputQueue.Enqueue(new SimulatedInput
            {
                Type = InputType.MouseClick,
                MouseButton = button,
                MousePosition = screenPosition,
                Timestamp = Time.time
            });

            // Also try to raycast and click UI elements
            SimulateUIClick(screenPosition, button);

            Debug.Log($"[InputSimulation] Mouse click at {screenPosition}, button {button}");
        }

        public void MouseMove(Vector2 screenPosition)
        {
            _mousePosition = screenPosition;
            _inputQueue.Enqueue(new SimulatedInput
            {
                Type = InputType.MouseMove,
                MousePosition = screenPosition,
                Timestamp = Time.time
            });
        }

        public void MouseScroll(float delta)
        {
            _inputQueue.Enqueue(new SimulatedInput
            {
                Type = InputType.MouseScroll,
                ScrollDelta = delta,
                Timestamp = Time.time
            });
            Debug.Log($"[InputSimulation] Mouse scroll: {delta}");
        }

        public IReadOnlyList<KeyCode> GetHeldKeys()
        {
            return new List<KeyCode>(_heldKeys);
        }

        public void ReleaseAll()
        {
            foreach (var key in _heldKeys)
            {
                _inputQueue.Enqueue(new SimulatedInput
                {
                    Type = InputType.KeyUp,
                    KeyCode = key,
                    Timestamp = Time.time
                });
            }
            _heldKeys.Clear();
            Debug.Log("[InputSimulation] All keys released");
        }

        /// <summary>
        /// Check if a key is currently being simulated as held.
        /// </summary>
        public bool IsKeyHeld(KeyCode key)
        {
            return _heldKeys.Contains(key);
        }

        /// <summary>
        /// Get the simulated mouse position.
        /// </summary>
        public Vector2 GetMousePosition()
        {
            return _mousePosition;
        }

        /// <summary>
        /// Process pending inputs (call from Update).
        /// </summary>
        public void ProcessInputs()
        {
            while (_inputQueue.Count > 0)
            {
                var input = _inputQueue.Dequeue();
                // In a real implementation, this would inject into Unity's input system
                // For now, we log and let the tools handle direct interaction
            }
        }

        private void SimulateUIClick(Vector2 screenPosition, int button)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var pointerData = new PointerEventData(eventSystem)
            {
                position = screenPosition,
                button = (PointerEventData.InputButton)button
            };

            var raycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, raycastResults);

            if (raycastResults.Count > 0)
            {
                var target = raycastResults[0].gameObject;

                // Execute pointer click
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);

                Debug.Log($"[InputSimulation] UI click hit: {target.name}");
            }
        }

        private enum InputType
        {
            KeyPress,
            KeyDown,
            KeyUp,
            MouseClick,
            MouseMove,
            MouseScroll
        }

        private struct SimulatedInput
        {
            public InputType Type;
            public KeyCode KeyCode;
            public int MouseButton;
            public Vector2 MousePosition;
            public float ScrollDelta;
            public float Timestamp;
        }
    }
}
