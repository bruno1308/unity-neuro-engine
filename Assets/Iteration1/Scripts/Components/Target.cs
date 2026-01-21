using UnityEngine;
using VContainer;
using Iteration1.Services;

namespace Iteration1.Components
{
    /// <summary>
    /// Component attached to target prefabs. Handles click detection and notifies services.
    /// Requires a Collider component for OnMouseDown to work.
    /// </summary>
    public class Target : MonoBehaviour
    {
        [Inject] private IScoreService _scoreService;
        [Inject] private ITargetSpawnerService _spawnerService;

        private void OnMouseDown()
        {
            if (_scoreService == null || _spawnerService == null)
            {
                Debug.LogError($"[Target] Services not injected! ScoreService={_scoreService}, SpawnerService={_spawnerService}");
                return;
            }

            _scoreService.AddScore(1);
            _spawnerService.DestroyCurrentTarget();
        }
    }
}
