namespace Iteration1.Services
{
    public interface IGameStateService
    {
        string GameState { get; }
        string ToJson();

        /// <summary>
        /// Simulates a click on the current target for AI playtesting.
        /// </summary>
        /// <returns>True if click was successful, false if no target exists.</returns>
        bool SimulateClick();

        /// <summary>
        /// Gets the screen position of the current target.
        /// </summary>
        UnityEngine.Vector3 GetTargetScreenPosition();
    }
}
