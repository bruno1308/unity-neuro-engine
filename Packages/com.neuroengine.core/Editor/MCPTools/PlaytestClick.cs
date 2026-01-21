#if UNITY_EDITOR
using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to invoke PlaytestService.SimulateClick for automated game testing.
    /// Resolves IGameStateService from any VContainer LifetimeScope in the scene.
    /// </summary>
    [McpForUnityTool("playtest_click", Description = "Simulates a target click using PlaytestService. Returns the updated game state JSON. Requires Play Mode and a game with IGameStateService registered in VContainer.")]
    public static class PlaytestClick
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant() ?? "click";

            if (!EditorApplication.isPlaying)
            {
                return new ErrorResponse("Cannot playtest outside of Play Mode. Use manage_editor(action='play') first.");
            }

            try
            {
                // Find any LifetimeScope in the scene
                var lifetimeScopes = UnityEngine.Object.FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None);

                if (lifetimeScopes.Length == 0)
                {
                    return new ErrorResponse("No VContainer LifetimeScope found in scene. The game must use VContainer for dependency injection.");
                }

                // Try to resolve IGameStateService from each scope
                object gameStateService = null;
                LifetimeScope resolvedScope = null;

                foreach (var scope in lifetimeScopes)
                {
                    if (scope.Container == null) continue;

                    try
                    {
                        // Use reflection to check if IGameStateService is registered
                        var gameStateType = FindGameStateServiceType();
                        if (gameStateType == null)
                        {
                            return new ErrorResponse("IGameStateService interface not found. Ensure Iteration1.Services namespace is accessible.");
                        }

                        gameStateService = scope.Container.Resolve(gameStateType);
                        if (gameStateService != null)
                        {
                            resolvedScope = scope;
                            break;
                        }
                    }
                    catch
                    {
                        // This scope doesn't have IGameStateService, try the next one
                        continue;
                    }
                }

                if (gameStateService == null)
                {
                    return new ErrorResponse("IGameStateService not registered in any VContainer scope. Ensure the game registers GameStateService.", new
                    {
                        scopes_found = lifetimeScopes.Length,
                        scope_names = GetScopeNames(lifetimeScopes)
                    });
                }

                switch (action)
                {
                    case "click":
                        return HandleClick(gameStateService, resolvedScope.name);

                    case "state":
                        return HandleGetState(gameStateService, resolvedScope.name);

                    default:
                        return new ErrorResponse($"Unknown action '{action}'. Use: click, state");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error in playtest: {e.Message}");
            }
        }

        private static object HandleClick(object gameStateService, string scopeName)
        {
            // Use reflection to call SimulateClick
            var simulateMethod = gameStateService.GetType().GetMethod("SimulateClick");
            if (simulateMethod == null)
            {
                return new ErrorResponse("IGameStateService does not have SimulateClick method.");
            }

            bool clicked = (bool)simulateMethod.Invoke(gameStateService, null);

            if (!clicked)
            {
                return new ErrorResponse("SimulateClick returned false. No active target to click.", new
                {
                    game_state = GetGameStateJson(gameStateService)
                });
            }

            // Get updated state
            string stateJson = GetGameStateJson(gameStateService);

            return new SuccessResponse("Target clicked successfully", new
            {
                clicked = true,
                scope = scopeName,
                game_state = stateJson
            });
        }

        private static object HandleGetState(object gameStateService, string scopeName)
        {
            string stateJson = GetGameStateJson(gameStateService);

            return new SuccessResponse("Game state retrieved", new
            {
                scope = scopeName,
                game_state = stateJson
            });
        }

        private static string GetGameStateJson(object gameStateService)
        {
            var toJsonMethod = gameStateService.GetType().GetMethod("ToJson");
            if (toJsonMethod != null)
            {
                return (string)toJsonMethod.Invoke(gameStateService, null);
            }
            return "{}";
        }

        private static Type FindGameStateServiceType()
        {
            // Search all loaded assemblies for IGameStateService
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType("Iteration1.Services.IGameStateService");
                    if (type != null) return type;
                }
                catch
                {
                    // Assembly might not be accessible, continue
                }
            }
            return null;
        }

        private static string[] GetScopeNames(LifetimeScope[] scopes)
        {
            var names = new string[scopes.Length];
            for (int i = 0; i < scopes.Length; i++)
            {
                names[i] = scopes[i].name;
            }
            return names;
        }
    }
}
#endif
