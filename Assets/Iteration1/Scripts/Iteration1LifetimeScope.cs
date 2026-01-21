using UnityEngine;
using VContainer;
using VContainer.Unity;
using Iteration1.Services;
using Iteration1.Components;

namespace Iteration1
{
    public class Iteration1LifetimeScope : LifetimeScope
    {
        [SerializeField] private GameObject _targetPrefab;
        [SerializeField] private Transform _spawnParent;

        protected override void Configure(IContainerBuilder builder)
        {
            // Register services (Singleton lifetime)
            builder.Register<IScoreService, ScoreService>(Lifetime.Singleton);

            builder.Register<ITargetSpawnerService, TargetSpawnerService>(Lifetime.Singleton)
                .WithParameter("targetPrefab", _targetPrefab)
                .WithParameter("parentTransform", _spawnParent);

            // PlaytestService must be registered before GameStateService (dependency)
            builder.Register<IPlaytestService, PlaytestService>(Lifetime.Singleton);

            builder.Register<IGameStateService, GameStateService>(Lifetime.Singleton);

            // Register MonoBehaviours in hierarchy for injection
            builder.RegisterComponentInHierarchy<TargetClicker>();
            builder.RegisterComponentInHierarchy<GameHUDController>();
            builder.RegisterComponentInHierarchy<PlaytestBridge>();
        }
    }
}
