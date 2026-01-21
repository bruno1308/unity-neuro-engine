using System.Collections.Generic;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for querying UI state as a "DOM for games".
    /// This is Layer 2 (Observation) - enables AI to understand what UI is on screen.
    /// </summary>
    public interface IUIAccessibility
    {
        /// <summary>
        /// Capture the current state of all UI elements.
        /// </summary>
        /// <returns>A graph of all UI elements with their states.</returns>
        UIGraph CaptureUIState();

        /// <summary>
        /// Find a specific UI element by name.
        /// </summary>
        /// <param name="name">The name of the element to find.</param>
        /// <returns>The element if found, null otherwise.</returns>
        UIElement FindElementByName(string name);

        /// <summary>
        /// Find all elements that can be interacted with (clicked, typed into, etc).
        /// </summary>
        /// <returns>List of interactable elements.</returns>
        List<UIElement> FindInteractableElements();

        /// <summary>
        /// Find all elements that are blocked by other UI elements.
        /// </summary>
        /// <returns>List of blocked elements.</returns>
        List<UIElement> FindBlockedElements();
    }

    /// <summary>
    /// A graph of all UI elements in the current scene.
    /// Note: Not marked [Serializable] - we use Newtonsoft.Json for serialization.
    /// </summary>
    public class UIGraph
    {
        /// <summary>
        /// All UI elements found in the scene.
        /// </summary>
        public List<UIElement> Elements = new List<UIElement>();

        /// <summary>
        /// ISO 8601 timestamp when the graph was captured.
        /// </summary>
        public string Timestamp;

        /// <summary>
        /// Total number of UI elements in the graph.
        /// </summary>
        public int TotalElements;

        /// <summary>
        /// Count of elements that can be interacted with.
        /// </summary>
        public int InteractableCount;

        /// <summary>
        /// Count of elements blocked by other UI elements.
        /// </summary>
        public int BlockedCount;

        /// <summary>
        /// The UI system(s) that contributed elements: "UIToolkit", "uGUI", "Both", "None"
        /// </summary>
        public string UISystem;
    }

    /// <summary>
    /// Represents a single UI element with its state and properties.
    /// Note: Not marked [Serializable] - we use Newtonsoft.Json for serialization.
    /// </summary>
    public class UIElement
    {
        /// <summary>
        /// The name of this element.
        /// </summary>
        public string Name;

        /// <summary>
        /// The type: "Button", "TextField", "Label", "Toggle", "Dropdown", "Slider", etc.
        /// </summary>
        public string Type;

        /// <summary>
        /// Hierarchy path to this element.
        /// </summary>
        public string Path;

        /// <summary>
        /// Screen position in pixels [x, y].
        /// </summary>
        public float[] ScreenPosition = new float[2];

        /// <summary>
        /// Size of the element [width, height].
        /// </summary>
        public float[] Size = new float[2];

        /// <summary>
        /// Whether this element is currently visible.
        /// </summary>
        public bool Visible;

        /// <summary>
        /// Whether this element can be interacted with.
        /// </summary>
        public bool Interactable;

        /// <summary>
        /// The name of another element blocking this one, or null.
        /// </summary>
        public string BlockedBy;

        /// <summary>
        /// The UI system: "UIToolkit" or "uGUI".
        /// </summary>
        public string Source;

        /// <summary>
        /// Type-specific properties (text, value, choices, etc).
        /// </summary>
        public Dictionary<string, object> Properties = new Dictionary<string, object>();
    }
}
