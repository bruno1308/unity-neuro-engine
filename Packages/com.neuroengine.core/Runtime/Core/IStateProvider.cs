namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for components that can expose their state as JSON.
    /// This is fundamental to Layer 2 (Observation) - all game state must be queryable.
    /// </summary>
    public interface IStateProvider
    {
        /// <summary>
        /// Returns the current state as a JSON-serializable object.
        /// </summary>
        object GetState();

        /// <summary>
        /// Returns a unique identifier for this state provider.
        /// </summary>
        string StateId { get; }
    }
}
