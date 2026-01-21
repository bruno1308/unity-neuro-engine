#if UNITY_EDITOR
using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Iteration1.Services;
using VContainer;

namespace Iteration1.Editor
{
    /// <summary>
    /// Custom MCP tools for playtesting Iteration1 Target Clicker game.
    /// Allows AI agents to simulate gameplay without requiring actual mouse input.
    /// </summary>
    [McpForUnityTool("iteration1_click_target", Description = "Simulates clicking the current target in Iteration1 Target Clicker game. Returns new score and game state.")]
    public static class Iteration1ClickTarget
    {
        public static object HandleCommand(JObject @params)
        {
            if (!EditorApplication.isPlaying)
            {
                return new ErrorResponse("Game must be in Play mode. Use manage_editor(action='play') first.");
            }

            var lifetimeScope = UnityEngine.Object.FindFirstObjectByType<Iteration1LifetimeScope>();
            if (lifetimeScope == null)
            {
                return new ErrorResponse("Iteration1LifetimeScope not found. Is Iteration1Scene loaded?");
            }

            var container = lifetimeScope.Container;
            if (container == null)
            {
                return new ErrorResponse("VContainer not initialized yet. Wait for game to start.");
            }

            IScoreService scoreService;
            ITargetSpawnerService spawnerService;

            try
            {
                scoreService = container.Resolve<IScoreService>();
                spawnerService = container.Resolve<ITargetSpawnerService>();
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to resolve services: {e.Message}");
            }

            if (!spawnerService.HasActiveTarget)
            {
                return new ErrorResponse("No active target to click", new
                {
                    score = scoreService.CurrentScore,
                    targetScore = scoreService.TargetScore,
                    hasWon = scoreService.HasWon
                });
            }

            int scoreBefore = scoreService.CurrentScore;
            Vector3 targetPos = spawnerService.CurrentTargetPosition;

            // Simulate the click - same logic as Target.OnMouseDown()
            scoreService.AddScore(1);
            spawnerService.DestroyCurrentTarget();

            return new SuccessResponse($"Clicked target at ({targetPos.x:F2}, {targetPos.y:F2}, {targetPos.z:F2})", new
            {
                scoreBefore = scoreBefore,
                scoreAfter = scoreService.CurrentScore,
                targetScore = scoreService.TargetScore,
                hasWon = scoreService.HasWon,
                hasActiveTarget = spawnerService.HasActiveTarget,
                targetPosition = spawnerService.HasActiveTarget ? new { x = spawnerService.CurrentTargetPosition.x, y = spawnerService.CurrentTargetPosition.y, z = spawnerService.CurrentTargetPosition.z } : null
            });
        }
    }

    /// <summary>
    /// Gets the current game state for Iteration1 Target Clicker.
    /// </summary>
    [McpForUnityTool("iteration1_get_state", Description = "Gets current Iteration1 Target Clicker game state including score, win status, and target info.")]
    public static class Iteration1GetState
    {
        public static object HandleCommand(JObject @params)
        {
            if (!EditorApplication.isPlaying)
            {
                return new ErrorResponse("Game must be in Play mode. Use manage_editor(action='play') first.");
            }

            var lifetimeScope = UnityEngine.Object.FindFirstObjectByType<Iteration1LifetimeScope>();
            if (lifetimeScope == null)
            {
                return new ErrorResponse("Iteration1LifetimeScope not found. Is Iteration1Scene loaded?");
            }

            var container = lifetimeScope.Container;
            if (container == null)
            {
                return new ErrorResponse("VContainer not initialized yet. Wait for game to start.");
            }

            IScoreService scoreService;
            ITargetSpawnerService spawnerService;

            try
            {
                scoreService = container.Resolve<IScoreService>();
                spawnerService = container.Resolve<ITargetSpawnerService>();
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to resolve services: {e.Message}");
            }

            var targetPos = spawnerService.HasActiveTarget ? spawnerService.CurrentTargetPosition : Vector3.zero;

            return new SuccessResponse("Game state retrieved", new
            {
                score = scoreService.CurrentScore,
                targetScore = scoreService.TargetScore,
                hasWon = scoreService.HasWon,
                hasActiveTarget = spawnerService.HasActiveTarget,
                targetPosition = spawnerService.HasActiveTarget ? new { x = targetPos.x, y = targetPos.y, z = targetPos.z } : null
            });
        }
    }
}
#endif
