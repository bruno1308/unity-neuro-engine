using System;
using System.Collections.Generic;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for games to implement event notification for AI agents.
    /// Attach a MonoBehaviour implementing this to your game and the
    /// get_game_events MCP tool will automatically find and query it.
    /// </summary>
    public interface IGameEventProvider
    {
        /// <summary>
        /// Get pending events since last poll.
        /// </summary>
        /// <param name="clearAfterRead">If true, clear the event queue after reading</param>
        /// <param name="sinceTimestamp">Optional: only return events after this ISO timestamp</param>
        IReadOnlyList<GameEvent> GetEvents(bool clearAfterRead = true, string sinceTimestamp = null);

        /// <summary>
        /// Get the current high-level game state for AI context.
        /// </summary>
        GameStateSnapshot GetStateSnapshot();
    }

    /// <summary>
    /// A game event that AI agents should be notified about.
    /// </summary>
    [Serializable]
    public class GameEvent
    {
        /// <summary>Event type identifier (e.g., "phase_change", "player_death", "action_required")</summary>
        public string Type;

        /// <summary>Human-readable description for the AI</summary>
        public string Prompt;

        /// <summary>Suggested MCP tool to handle this event (optional)</summary>
        public string SuggestedTool;

        /// <summary>Additional context data as key-value pairs</summary>
        public Dictionary<string, object> Context;

        /// <summary>Priority: 0=info, 1=action_suggested, 2=action_required</summary>
        public int Priority;

        /// <summary>ISO timestamp when event occurred</summary>
        public string Timestamp;

        public GameEvent(string type, string prompt, int priority = 0)
        {
            Type = type;
            Prompt = prompt;
            Priority = priority;
            Timestamp = DateTime.UtcNow.ToString("o");
            Context = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Snapshot of game state for AI context.
    /// Games should extend this with game-specific properties.
    /// </summary>
    [Serializable]
    public class GameStateSnapshot
    {
        /// <summary>Current game phase/state name</summary>
        public string Phase;

        /// <summary>Is the game currently active/running</summary>
        public bool IsActive;

        /// <summary>Game-specific state data</summary>
        public Dictionary<string, object> Data;

        /// <summary>Timestamp of this snapshot</summary>
        public string Timestamp;

        public GameStateSnapshot()
        {
            Data = new Dictionary<string, object>();
            Timestamp = DateTime.UtcNow.ToString("o");
        }
    }
}
