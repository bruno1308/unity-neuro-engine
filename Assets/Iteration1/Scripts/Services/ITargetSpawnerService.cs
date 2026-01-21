namespace Iteration1.Services
{
    public interface ITargetSpawnerService
    {
        UnityEngine.Vector3 CurrentTargetPosition { get; }
        bool HasActiveTarget { get; }
        void SpawnTarget();
        void DestroyCurrentTarget();
        event System.Action<UnityEngine.Vector3> OnTargetSpawned;
        event System.Action OnTargetDestroyed;
    }
}
