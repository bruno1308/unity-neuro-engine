namespace Iteration1.Services
{
    public interface IScoreService
    {
        int CurrentScore { get; }
        int TargetScore { get; }
        bool HasWon { get; }
        void AddScore(int amount);
        void Reset();
        event System.Action<int> OnScoreChanged;
        event System.Action OnWin;
    }
}
