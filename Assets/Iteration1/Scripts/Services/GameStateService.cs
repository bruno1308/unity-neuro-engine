using UnityEngine;
using VContainer;

namespace Iteration1.Services
{
    /// <summary>
    /// Aggregates game state from score and spawner services.
    /// Provides JSON serialization for Layer 2 observation.
    /// Exposes AI playtesting capabilities via IPlaytestService.
    /// </summary>
    public class GameStateService : IGameStateService
    {
        private readonly IScoreService _scoreService;
        private readonly ITargetSpawnerService _targetSpawnerService;
        private readonly IPlaytestService _playtestService;

        public string GameState => _scoreService.HasWon ? "Won" : "Playing";

        [Inject]
        public GameStateService(
            IScoreService scoreService,
            ITargetSpawnerService targetSpawnerService,
            IPlaytestService playtestService)
        {
            _scoreService = scoreService;
            _targetSpawnerService = targetSpawnerService;
            _playtestService = playtestService;
        }

        public bool SimulateClick()
        {
            return _playtestService.ClickCurrentTarget();
        }

        public Vector3 GetTargetScreenPosition()
        {
            return _playtestService.GetTargetScreenPosition();
        }

        public string ToJson()
        {
            var targetPos = _targetSpawnerService.CurrentTargetPosition;

            // Manual JSON construction to avoid Unity JsonUtility limitations with interfaces
            return $@"{{
  ""gameState"": ""{GameState}"",
  ""score"": {{
    ""current"": {_scoreService.CurrentScore},
    ""target"": {_scoreService.TargetScore},
    ""hasWon"": {_scoreService.HasWon.ToString().ToLower()}
  }},
  ""target"": {{
    ""hasActive"": {_targetSpawnerService.HasActiveTarget.ToString().ToLower()},
    ""position"": {{
      ""x"": {targetPos.x:F2},
      ""y"": {targetPos.y:F2},
      ""z"": {targetPos.z:F2}
    }}
  }}
}}";
        }
    }
}
