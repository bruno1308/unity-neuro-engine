using System;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to retrieve game events from any game implementing IGameEventProvider.
    /// Poll this every 5-10 seconds during gameplay to receive notifications about
    /// phase changes, important state updates, and actions that require AI response.
    /// </summary>
    [McpForUnityTool("get_game_events", Description = "Gets pending game events/notifications. Poll every 5-10 seconds during gameplay. Games must implement IGameEventProvider. Returns events with priority levels: 0=info, 1=suggested, 2=required.")]
    public static class GetGameEvents
    {
        public static object HandleCommand(JObject @params)
        {
            bool clear = @params["clear"]?.Value<bool>() ?? true;
            string sinceTimestamp = @params["since_timestamp"]?.ToString();
            bool includeState = @params["include_state"]?.Value<bool>() ?? false;

            if (!EditorApplication.isPlaying)
            {
                return new ErrorResponse("Requires Play Mode. Use manage_editor(action='play') first.");
            }

            try
            {
                // Find any MonoBehaviour implementing IGameEventProvider
                var providers = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                    .OfType<IGameEventProvider>()
                    .ToArray();

                if (providers.Length == 0)
                {
                    return new ErrorResponse(
                        "No IGameEventProvider found in scene. " +
                        "The game must implement IGameEventProvider to use this tool. " +
                        "See NeuroEngine.Core.IGameEventProvider for the interface.",
                        new { hint = "Add a MonoBehaviour implementing IGameEventProvider to your game" }
                    );
                }

                // Use the first provider (games typically have one)
                var provider = providers[0];
                var events = provider.GetEvents(clear, sinceTimestamp);

                var eventData = events.Select(e => new
                {
                    type = e.Type,
                    prompt = e.Prompt,
                    suggested_tool = e.SuggestedTool,
                    context = e.Context,
                    priority = e.Priority,
                    priority_label = GetPriorityLabel(e.Priority),
                    timestamp = e.Timestamp
                }).ToList();

                // Separate high priority events
                var actionRequired = eventData.Where(e => e.priority >= 2).ToList();
                var actionSuggested = eventData.Where(e => e.priority == 1).ToList();

                var result = new
                {
                    event_count = events.Count,
                    action_required_count = actionRequired.Count,
                    action_suggested_count = actionSuggested.Count,
                    events = eventData,
                    cleared = clear
                };

                // Optionally include current state snapshot
                if (includeState)
                {
                    var state = provider.GetStateSnapshot();
                    return new SuccessResponse($"Retrieved {events.Count} events", new
                    {
                        result.event_count,
                        result.action_required_count,
                        result.action_suggested_count,
                        result.events,
                        result.cleared,
                        state = new
                        {
                            phase = state.Phase,
                            is_active = state.IsActive,
                            data = state.Data,
                            timestamp = state.Timestamp
                        }
                    });
                }

                return new SuccessResponse($"Retrieved {events.Count} events", result);
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error retrieving events: {e.Message}");
            }
        }

        private static string GetPriorityLabel(int priority)
        {
            return priority switch
            {
                0 => "info",
                1 => "action_suggested",
                2 => "action_required",
                _ => "unknown"
            };
        }
    }
}
