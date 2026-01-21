using UnityEngine;
using VContainer;

namespace Iteration1.Services
{
    /// <summary>
    /// Service that enables AI to simulate player interactions.
    /// Bypasses physics-based input (OnMouseDown) by directly invoking game logic.
    /// </summary>
    public class PlaytestService : IPlaytestService
    {
        private readonly ITargetSpawnerService _spawnerService;
        private readonly IScoreService _scoreService;

        [Inject]
        public PlaytestService(ITargetSpawnerService spawnerService, IScoreService scoreService)
        {
            _spawnerService = spawnerService;
            _scoreService = scoreService;
        }

        public bool ClickCurrentTarget()
        {
            if (!_spawnerService.HasActiveTarget) return false;

            // Programmatically trigger the click logic (same as Target.OnMouseDown)
            _scoreService.AddScore(1);
            _spawnerService.DestroyCurrentTarget();
            return true;
        }

        public Vector3 GetTargetScreenPosition()
        {
            if (!_spawnerService.HasActiveTarget) return Vector3.zero;

            var worldPos = _spawnerService.CurrentTargetPosition;
            var mainCamera = Camera.main;

            if (mainCamera == null) return Vector3.zero;

            return mainCamera.WorldToScreenPoint(worldPos);
        }
    }
}
