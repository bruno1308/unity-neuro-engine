using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using Iteration1.Services;

namespace Iteration1.Components
{
    /// <summary>
    /// Binds UI Toolkit elements to game state.
    /// Requires a UIDocument component on the same GameObject.
    /// Expected UI elements: "score-label" (Label), "win-panel" (VisualElement).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameHUDController : MonoBehaviour
    {
        [Inject] private IScoreService _scoreService;

        private Label _scoreLabel;
        private VisualElement _winPanel;

        private void Start()
        {
            if (_scoreService == null)
            {
                Debug.LogError("[GameHUDController] IScoreService not injected.");
                return;
            }

            var doc = GetComponent<UIDocument>();
            if (doc == null || doc.rootVisualElement == null)
            {
                Debug.LogError("[GameHUDController] UIDocument or rootVisualElement is null.");
                return;
            }

            var root = doc.rootVisualElement;
            _scoreLabel = root.Q<Label>("score-label");
            _winPanel = root.Q<VisualElement>("win-panel");

            if (_scoreLabel == null)
            {
                Debug.LogWarning("[GameHUDController] Could not find 'score-label' element.");
            }

            if (_winPanel == null)
            {
                Debug.LogWarning("[GameHUDController] Could not find 'win-panel' element.");
            }
            else
            {
                // Hide win panel initially
                _winPanel.style.display = DisplayStyle.None;
            }

            _scoreService.OnScoreChanged += UpdateScore;
            _scoreService.OnWin += ShowWinPanel;

            // Initialize display with current score
            UpdateScore(_scoreService.CurrentScore);
        }

        private void UpdateScore(int score)
        {
            if (_scoreLabel != null)
            {
                _scoreLabel.text = $"Score: {score} / {_scoreService.TargetScore}";
            }
        }

        private void ShowWinPanel()
        {
            if (_winPanel != null)
            {
                _winPanel.style.display = DisplayStyle.Flex;
            }
        }

        private void OnDestroy()
        {
            if (_scoreService != null)
            {
                _scoreService.OnScoreChanged -= UpdateScore;
                _scoreService.OnWin -= ShowWinPanel;
            }
        }
    }
}
