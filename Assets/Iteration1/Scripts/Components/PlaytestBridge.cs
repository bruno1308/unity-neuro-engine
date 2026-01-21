using UnityEngine;
using VContainer;
using Iteration1.Services;

namespace Iteration1.Components
{
    /// <summary>
    /// Bridge component that enables MCP to trigger playtest actions via property setters.
    /// Attach to any GameObject in the scene. Set triggerClick=true to simulate a target click.
    /// </summary>
    public class PlaytestBridge : MonoBehaviour
    {
        [Inject] private IGameStateService _gameStateService;

        [Header("Playtest Controls")]
        [Tooltip("Set to true to trigger a click. Auto-resets to false.")]
        [SerializeField] private bool _triggerClick;

        [Header("Read-Only State")]
        [SerializeField] private int _currentScore;
        [SerializeField] private bool _hasWon;
        [SerializeField] private string _lastClickResult;

        // Property that MCP can set to trigger a click
        public bool TriggerClick
        {
            get => _triggerClick;
            set
            {
                if (value && _gameStateService != null)
                {
                    bool result = _gameStateService.SimulateClick();
                    _lastClickResult = result ? "Success" : "No target";
                    UpdateState();
                }
                _triggerClick = false; // Auto-reset
            }
        }

        public int CurrentScore => _currentScore;
        public bool HasWon => _hasWon;
        public string LastClickResult => _lastClickResult;
        public string GameStateJson => _gameStateService?.ToJson() ?? "{}";

        private void Update()
        {
            // Handle inspector-triggered clicks
            if (_triggerClick && _gameStateService != null)
            {
                bool result = _gameStateService.SimulateClick();
                _lastClickResult = result ? "Success" : "No target";
                _triggerClick = false;
                UpdateState();
            }
        }

        private void UpdateState()
        {
            if (_gameStateService == null) return;

            // Parse state from JSON (simple approach)
            string json = _gameStateService.ToJson();

            // Extract current score using string parsing
            int scoreStart = json.IndexOf("\"current\":") + 10;
            int scoreEnd = json.IndexOf(",", scoreStart);
            if (scoreStart > 10 && scoreEnd > scoreStart)
            {
                int.TryParse(json.Substring(scoreStart, scoreEnd - scoreStart).Trim(), out _currentScore);
            }

            // Extract hasWon
            _hasWon = json.Contains("\"hasWon\": true");
        }

        private void Start()
        {
            UpdateState();
        }
    }
}
