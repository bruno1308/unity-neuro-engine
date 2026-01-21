using System;

namespace Iteration1.Services
{
    /// <summary>
    /// Tracks player score for the Target Clicker game.
    /// Win condition: CurrentScore >= TargetScore (10 points).
    /// </summary>
    public class ScoreService : IScoreService
    {
        public int CurrentScore { get; private set; }
        public int TargetScore => 10;
        public bool HasWon => CurrentScore >= TargetScore;

        public event Action<int> OnScoreChanged;
        public event Action OnWin;

        public void AddScore(int amount)
        {
            if (amount <= 0) return;

            CurrentScore += amount;
            OnScoreChanged?.Invoke(CurrentScore);

            if (HasWon)
            {
                OnWin?.Invoke();
            }
        }

        public void Reset()
        {
            CurrentScore = 0;
            OnScoreChanged?.Invoke(CurrentScore);
        }
    }
}
