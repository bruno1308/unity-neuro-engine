using System;
using System.Collections.Generic;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for Layer 6 Safety Controls.
    /// Enforces limits on agent behavior to prevent runaway costs, infinite loops,
    /// and ensures human oversight for critical decisions.
    ///
    /// Safety limits (from mayor.md):
    /// - Max iterations per task: 50
    /// - Max API cost per hour: $10
    /// - Max parallel agents: 5
    /// </summary>
    public interface ISafetyControl
    {
        /// <summary>
        /// Check if a task can continue iterating without exceeding limits.
        /// </summary>
        /// <param name="taskId">The task identifier</param>
        /// <returns>True if safe to continue, false if iteration limit reached</returns>
        bool CheckIterationLimit(string taskId);

        /// <summary>
        /// Increment the iteration count for a task.
        /// Call this at the start of each iteration.
        /// </summary>
        /// <param name="taskId">The task identifier</param>
        void IncrementIteration(string taskId);

        /// <summary>
        /// Check if a cost can be incurred without exceeding hourly budget.
        /// </summary>
        /// <param name="costEstimate">Estimated cost in USD</param>
        /// <returns>True if budget allows, false if would exceed limit</returns>
        bool CheckBudget(decimal costEstimate);

        /// <summary>
        /// Record an API cost. Updates hourly budget tracking.
        /// </summary>
        /// <param name="amount">Cost in USD</param>
        /// <param name="description">Description of the cost (e.g., "Claude API call")</param>
        void RecordCost(decimal amount, string description);

        /// <summary>
        /// Get current budget status including spent, remaining, and cost history.
        /// </summary>
        BudgetInfo GetBudgetStatus();

        /// <summary>
        /// Check if another parallel agent can be spawned.
        /// </summary>
        /// <returns>True if under parallel agent limit, false otherwise</returns>
        bool CheckParallelAgents();

        /// <summary>
        /// Register an agent as active. Call when spawning agents.
        /// </summary>
        /// <param name="agentId">Unique agent identifier</param>
        /// <param name="agentType">Type of agent (e.g., "script", "scene", "asset")</param>
        void RegisterAgent(string agentId, string agentType);

        /// <summary>
        /// Unregister an agent when it completes. Call when agent finishes.
        /// </summary>
        /// <param name="agentId">Unique agent identifier</param>
        void UnregisterAgent(string agentId);

        /// <summary>
        /// Get the count of currently active agents.
        /// </summary>
        int GetActiveAgentCount();

        /// <summary>
        /// Trigger a git rollback due to a safety issue or regression.
        /// Creates a rollback record and optionally executes git reset.
        /// </summary>
        /// <param name="reason">Why the rollback was triggered</param>
        /// <returns>Rollback result with commit info</returns>
        RollbackResult TriggerRollback(string reason);

        /// <summary>
        /// Create an approval request for human review.
        /// Used for critical decisions that require human oversight.
        /// </summary>
        /// <param name="reason">Why approval is needed</param>
        /// <param name="context">Additional context for the reviewer</param>
        /// <returns>The created approval request</returns>
        ApprovalRequest RequestHumanApproval(string reason, Dictionary<string, object> context);

        /// <summary>
        /// Get the status of an approval request.
        /// </summary>
        /// <param name="requestId">The approval request ID</param>
        /// <returns>Current status of the request</returns>
        ApprovalStatus GetApprovalStatus(string requestId);

        /// <summary>
        /// List all pending approval requests.
        /// </summary>
        List<ApprovalRequest> ListPendingApprovals();

        /// <summary>
        /// Approve or reject a pending request (typically called by human).
        /// </summary>
        /// <param name="requestId">The approval request ID</param>
        /// <param name="approved">True to approve, false to reject</param>
        /// <param name="reviewerNotes">Optional notes from reviewer</param>
        void ResolveApproval(string requestId, bool approved, string reviewerNotes = null);

        /// <summary>
        /// Get iteration info for a specific task.
        /// </summary>
        /// <param name="taskId">The task identifier</param>
        IterationInfo GetIterationInfo(string taskId);

        /// <summary>
        /// Reset iteration count for a task (use carefully, e.g., after human approval).
        /// </summary>
        /// <param name="taskId">The task identifier</param>
        void ResetIterations(string taskId);
    }

    /// <summary>
    /// Budget status information.
    /// </summary>
    public class BudgetInfo
    {
        /// <summary>Total cost limit per hour in USD</summary>
        public decimal HourlyLimit { get; set; } = 10.00m;

        /// <summary>Cost spent in current hour</summary>
        public decimal SpentThisHour { get; set; }

        /// <summary>Remaining budget for current hour</summary>
        public decimal RemainingBudget => HourlyLimit - SpentThisHour;

        /// <summary>When the current hour window started</summary>
        public DateTime HourWindowStart { get; set; }

        /// <summary>When the current hour window ends</summary>
        public DateTime HourWindowEnd => HourWindowStart.AddHours(1);

        /// <summary>Total cost spent across all sessions</summary>
        public decimal TotalSpent { get; set; }

        /// <summary>Recent cost entries (last 24 hours)</summary>
        public List<CostEntry> RecentCosts { get; set; } = new List<CostEntry>();

        /// <summary>Whether budget is currently paused due to limit breach</summary>
        public bool IsPaused { get; set; }

        /// <summary>Reason for pause (if applicable)</summary>
        public string PauseReason { get; set; }
    }

    /// <summary>
    /// A single cost entry.
    /// </summary>
    public class CostEntry
    {
        /// <summary>Cost in USD</summary>
        public decimal Amount { get; set; }

        /// <summary>Description of the cost</summary>
        public string Description { get; set; }

        /// <summary>When the cost was incurred</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Which task incurred this cost (optional)</summary>
        public string TaskId { get; set; }

        /// <summary>Which agent incurred this cost (optional)</summary>
        public string AgentId { get; set; }
    }

    /// <summary>
    /// Iteration tracking for a task.
    /// </summary>
    public class IterationInfo
    {
        /// <summary>Task identifier</summary>
        public string TaskId { get; set; }

        /// <summary>Current iteration count</summary>
        public int CurrentIteration { get; set; }

        /// <summary>Maximum allowed iterations</summary>
        public int MaxIterations { get; set; } = 50;

        /// <summary>Remaining iterations</summary>
        public int RemainingIterations => MaxIterations - CurrentIteration;

        /// <summary>Whether the task has reached its iteration limit</summary>
        public bool LimitReached => CurrentIteration >= MaxIterations;

        /// <summary>When this task first started iterating</summary>
        public DateTime FirstIteration { get; set; }

        /// <summary>When the last iteration occurred</summary>
        public DateTime LastIteration { get; set; }
    }

    /// <summary>
    /// Result of a rollback operation.
    /// </summary>
    public class RollbackResult
    {
        /// <summary>Whether the rollback was successful</summary>
        public bool Success { get; set; }

        /// <summary>The commit hash that was rolled back to</summary>
        public string RolledBackToCommit { get; set; }

        /// <summary>The commit hash that was rolled back from</summary>
        public string RolledBackFromCommit { get; set; }

        /// <summary>Number of commits rolled back</summary>
        public int CommitsRolledBack { get; set; }

        /// <summary>Reason for the rollback</summary>
        public string Reason { get; set; }

        /// <summary>When the rollback occurred</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Error message if rollback failed</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Files affected by the rollback</summary>
        public List<string> AffectedFiles { get; set; } = new List<string>();
    }

    /// <summary>
    /// A request for human approval.
    /// </summary>
    public class ApprovalRequest
    {
        /// <summary>Unique request identifier</summary>
        public string RequestId { get; set; }

        /// <summary>Why approval is needed</summary>
        public string Reason { get; set; }

        /// <summary>Additional context for the reviewer</summary>
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();

        /// <summary>Request status: "pending", "approved", "rejected"</summary>
        public string Status { get; set; } = "pending";

        /// <summary>When the request was created</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>When the request was resolved (if applicable)</summary>
        public DateTime? ResolvedAt { get; set; }

        /// <summary>Notes from the reviewer</summary>
        public string ReviewerNotes { get; set; }

        /// <summary>Which task triggered this request (optional)</summary>
        public string TaskId { get; set; }

        /// <summary>Which agent triggered this request (optional)</summary>
        public string AgentId { get; set; }

        /// <summary>Priority level: 0=low, 1=normal, 2=high, 3=critical</summary>
        public int Priority { get; set; } = 1;

        /// <summary>Category of approval: "budget", "iteration", "rollback", "other"</summary>
        public string Category { get; set; } = "other";
    }

    /// <summary>
    /// Status of an approval request.
    /// </summary>
    public class ApprovalStatus
    {
        /// <summary>Request identifier</summary>
        public string RequestId { get; set; }

        /// <summary>Current status: "pending", "approved", "rejected", "expired"</summary>
        public string Status { get; set; }

        /// <summary>Whether the request has been resolved</summary>
        public bool IsResolved => Status == "approved" || Status == "rejected" || Status == "expired";

        /// <summary>Whether the request was approved</summary>
        public bool IsApproved => Status == "approved";

        /// <summary>Notes from the reviewer</summary>
        public string ReviewerNotes { get; set; }

        /// <summary>When the request was resolved</summary>
        public DateTime? ResolvedAt { get; set; }

        /// <summary>Time remaining before expiry (if pending)</summary>
        public TimeSpan? TimeRemaining { get; set; }
    }

    /// <summary>
    /// Information about an active agent.
    /// </summary>
    public class ActiveAgent
    {
        /// <summary>Unique agent identifier</summary>
        public string AgentId { get; set; }

        /// <summary>Agent type: "script", "scene", "asset", "eyes", "evaluator"</summary>
        public string AgentType { get; set; }

        /// <summary>When the agent was spawned</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>Which task the agent is working on (optional)</summary>
        public string TaskId { get; set; }
    }
}
