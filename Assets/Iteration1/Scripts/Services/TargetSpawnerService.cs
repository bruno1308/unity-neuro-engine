using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Iteration1.Services
{
    /// <summary>
    /// Spawns targets at random positions within defined bounds.
    /// Bounds: X [-4, 4], Y [0, 3], Z = 5
    /// </summary>
    public class TargetSpawnerService : ITargetSpawnerService
    {
        private readonly IObjectResolver _resolver;
        private readonly GameObject _targetPrefab;
        private readonly Transform _parentTransform;

        private GameObject _currentTarget;

        // Spawn bounds
        private const float MinX = -4f;
        private const float MaxX = 4f;
        private const float MinY = 0f;
        private const float MaxY = 3f;
        private const float SpawnZ = 5f;

        public Vector3 CurrentTargetPosition => _currentTarget != null
            ? _currentTarget.transform.position
            : Vector3.zero;

        public bool HasActiveTarget => _currentTarget != null;

        public event Action<Vector3> OnTargetSpawned;
        public event Action OnTargetDestroyed;

        [Inject]
        public TargetSpawnerService(IObjectResolver resolver, GameObject targetPrefab, Transform parentTransform)
        {
            _resolver = resolver;
            _targetPrefab = targetPrefab;
            _parentTransform = parentTransform;
        }

        public void SpawnTarget()
        {
            // Destroy existing target first
            if (_currentTarget != null)
            {
                DestroyCurrentTarget();
            }

            // Generate random position within bounds
            var position = new Vector3(
                UnityEngine.Random.Range(MinX, MaxX),
                UnityEngine.Random.Range(MinY, MaxY),
                SpawnZ
            );

            // Instantiate the target using VContainer's resolver for proper dependency injection
            // Preserve prefab rotation (target faces camera at -Z)
            _currentTarget = _resolver.Instantiate(_targetPrefab, position, _targetPrefab.transform.rotation, _parentTransform);

            OnTargetSpawned?.Invoke(position);
        }

        public void DestroyCurrentTarget()
        {
            if (_currentTarget == null) return;

            Object.Destroy(_currentTarget);
            _currentTarget = null;

            OnTargetDestroyed?.Invoke();
        }
    }
}
