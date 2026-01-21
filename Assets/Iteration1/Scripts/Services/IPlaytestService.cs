namespace Iteration1.Services
{
    /// <summary>
    /// Enables AI input simulation for playtesting.
    /// Allows programmatic invocation of game interactions without physics-based mouse input.
    /// </summary>
    public interface IPlaytestService
    {
        /// <summary>
        /// Simulates clicking the current target. Returns true if click was successful.
        /// </summary>
        bool ClickCurrentTarget();

        /// <summary>
        /// Gets the screen position of the current target for verification.
        /// </summary>
        UnityEngine.Vector3 GetTargetScreenPosition();
    }
}
