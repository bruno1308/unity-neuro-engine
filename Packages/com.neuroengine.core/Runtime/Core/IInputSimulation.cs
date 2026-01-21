using System.Collections.Generic;
using UnityEngine;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for simulating player input.
    /// This is Layer 3 (Interaction) - enables AI to "play" the game.
    /// </summary>
    public interface IInputSimulation
    {
        /// <summary>
        /// Simulate a key press (down and up).
        /// </summary>
        void PressKey(KeyCode key);

        /// <summary>
        /// Simulate key down (held).
        /// </summary>
        void KeyDown(KeyCode key);

        /// <summary>
        /// Simulate key up (released).
        /// </summary>
        void KeyUp(KeyCode key);

        /// <summary>
        /// Simulate mouse button click at screen position.
        /// </summary>
        void MouseClick(Vector2 screenPosition, int button = 0);

        /// <summary>
        /// Simulate mouse move to screen position.
        /// </summary>
        void MouseMove(Vector2 screenPosition);

        /// <summary>
        /// Simulate mouse scroll.
        /// </summary>
        void MouseScroll(float delta);

        /// <summary>
        /// Get currently held keys.
        /// </summary>
        IReadOnlyList<KeyCode> GetHeldKeys();

        /// <summary>
        /// Get current simulated mouse position.
        /// </summary>
        Vector2 GetMousePosition();

        /// <summary>
        /// Release all held keys.
        /// </summary>
        void ReleaseAll();
    }

    /// <summary>
    /// Result of an input simulation action.
    /// </summary>
    public class InputSimulationResult
    {
        public bool Success;
        public string Action;
        public string Details;
        public string Timestamp;

        public InputSimulationResult(bool success, string action, string details = null)
        {
            Success = success;
            Action = action;
            Details = details;
            Timestamp = System.DateTime.UtcNow.ToString("o");
        }
    }
}
