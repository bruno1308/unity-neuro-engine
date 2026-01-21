using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Layer 6: Agent Orchestration
    ///
    /// Manages the lifecycle of tasks and their assignment to specialized agents (Polecats).
    /// This is the governance layer that enables multi-agent coordination.
    ///
    /// The Mayor agent uses this interface to:
    /// - Create and track tasks
    /// - Assign work to specialized agents (Script, Scene, Asset, Eyes Polecats)
    /// - Track iteration counts for retry logic
    /// - Persist state to hooks/ for session survival
    /// </summary>
    public interface IOrchestration
    {
        /// <summary>
        /// Create a new orchestration task.
        /// Task is persisted to hooks/tasks/{taskId}.json
        /// </summary>
        TaskInfo CreateTask(TaskConfig config);

        /// <summary>
        /// Assign a task to a specific agent type.
        /// Updates task status to "assigned" and records the agent type.
        /// </summary>
        void AssignTask(string taskId, AgentType agentType);

        /// <summary>
        /// Get the current status of a task.
        /// </summary>
        TaskInfo GetTaskStatus(string taskId);

        /// <summary>
        /// Mark a task as completed with result data.
        /// </summary>
        void CompleteTask(string taskId, TaskCompletionResult result);

        /// <summary>
        /// Mark a task as failed with reason.
        /// Increments iteration count for retry tracking.
        /// </summary>
        void FailTask(string taskId, string reason);

        /// <summary>
        /// Get all tasks matching a filter.
        /// </summary>
        Task<List<TaskInfo>> GetTasksAsync(TaskStatusFilter filter = null);

        /// <summary>
        /// Increment the iteration count for a task (used for retry tracking).
        /// Returns the new iteration count.
        /// </summary>
        int IncrementTaskIteration(string taskId);

        /// <summary>
        /// Create a new convoy (group of related tasks).
        /// Convoy is persisted to hooks/convoys/{convoyId}.json
        /// </summary>
        ConvoyInfo CreateConvoy(ConvoyConfig config);

        /// <summary>
        /// Get the current status of a convoy.
        /// </summary>
        ConvoyInfo GetConvoyStatus(string convoyId);

        /// <summary>
        /// Mark a convoy as completed.
        /// All tasks in the convoy should be completed first.
        /// </summary>
        void CompleteConvoy(string convoyId);

        /// <summary>
        /// Fail a convoy with reason.
        /// </summary>
        void FailConvoy(string convoyId, string reason);

        /// <summary>
        /// Get all convoys matching a filter.
        /// </summary>
        Task<List<ConvoyInfo>> GetConvoysAsync(ConvoyStatusFilter filter = null);
    }

    /// <summary>
    /// Types of specialized agents that can execute tasks.
    /// </summary>
    public enum AgentType
    {
        /// <summary>Script Polecat - writes C# code</summary>
        ScriptPolecat,

        /// <summary>Scene Polecat - modifies scenes and prefabs via MCP</summary>
        ScenePolecat,

        /// <summary>Asset Polecat - generates 3D models, textures, audio</summary>
        AssetPolecat,

        /// <summary>Eyes Polecat - observes and reports state</summary>
        EyesPolecat,

        /// <summary>Evaluator - grades outcomes</summary>
        Evaluator,

        /// <summary>Mayor - orchestration (should not be assigned tasks)</summary>
        Mayor
    }

    /// <summary>
    /// Task lifecycle status.
    /// </summary>
    public enum TaskStatus
    {
        /// <summary>Task created but not yet assigned</summary>
        Pending,

        /// <summary>Task assigned to an agent but not yet started</summary>
        Assigned,

        /// <summary>Task is currently being worked on</summary>
        InProgress,

        /// <summary>Task completed successfully</summary>
        Completed,

        /// <summary>Task failed (may be retried)</summary>
        Failed,

        /// <summary>Task was cancelled</summary>
        Cancelled,

        /// <summary>Task is blocked by dependencies</summary>
        Blocked
    }

    /// <summary>
    /// Configuration for creating a new task.
    /// </summary>
    public class TaskConfig
    {
        /// <summary>Human-readable task name</summary>
        public string Name { get; set; }

        /// <summary>Detailed description of what needs to be done</summary>
        public string Description { get; set; }

        /// <summary>Iteration folder (e.g., "Iteration1")</summary>
        public string Iteration { get; set; }

        /// <summary>Convoy this task belongs to (optional)</summary>
        public string ConvoyId { get; set; }

        /// <summary>Task IDs this task depends on</summary>
        public List<string> Dependencies { get; set; } = new List<string>();

        /// <summary>Priority (higher = more important): 0=low, 1=normal, 2=high, 3=critical</summary>
        public int Priority { get; set; } = 1;

        /// <summary>Expected deliverable file path</summary>
        public string Deliverable { get; set; }

        /// <summary>Success criteria (human-readable checklist)</summary>
        public List<string> SuccessCriteria { get; set; } = new List<string>();

        /// <summary>Additional specification data (flexible structure)</summary>
        public Dictionary<string, object> Specification { get; set; } = new Dictionary<string, object>();

        /// <summary>Estimated time in minutes</summary>
        public int EstimatedMinutes { get; set; } = 15;

        /// <summary>Maximum retry iterations before escalation</summary>
        public int MaxIterations { get; set; } = 50;
    }

    /// <summary>
    /// Full task information including status and history.
    /// </summary>
    public class TaskInfo
    {
        /// <summary>Unique task identifier (e.g., "task-001")</summary>
        public string Id { get; set; }

        /// <summary>Human-readable task name</summary>
        public string Name { get; set; }

        /// <summary>Detailed description</summary>
        public string Description { get; set; }

        /// <summary>Iteration folder</summary>
        public string Iteration { get; set; }

        /// <summary>Convoy ID (if part of a convoy)</summary>
        public string ConvoyId { get; set; }

        /// <summary>Current status</summary>
        public TaskStatus Status { get; set; }

        /// <summary>Assigned agent type (null if not assigned)</summary>
        public AgentType? AssignedAgent { get; set; }

        /// <summary>Task IDs this task depends on</summary>
        public List<string> Dependencies { get; set; } = new List<string>();

        /// <summary>Priority level</summary>
        public int Priority { get; set; }

        /// <summary>Expected deliverable path</summary>
        public string Deliverable { get; set; }

        /// <summary>Success criteria</summary>
        public List<string> SuccessCriteria { get; set; } = new List<string>();

        /// <summary>Additional specification</summary>
        public Dictionary<string, object> Specification { get; set; } = new Dictionary<string, object>();

        /// <summary>Estimated time in minutes</summary>
        public int EstimatedMinutes { get; set; }

        /// <summary>Maximum iterations before escalation</summary>
        public int MaxIterations { get; set; }

        /// <summary>Current iteration count (retry tracking)</summary>
        public int IterationCount { get; set; }

        /// <summary>ISO timestamp when created</summary>
        public string CreatedAt { get; set; }

        /// <summary>ISO timestamp when assigned</summary>
        public string AssignedAt { get; set; }

        /// <summary>ISO timestamp when started</summary>
        public string StartedAt { get; set; }

        /// <summary>ISO timestamp when completed/failed</summary>
        public string CompletedAt { get; set; }

        /// <summary>Error message if failed</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Completion result (if completed)</summary>
        public TaskCompletionResult Result { get; set; }

        /// <summary>History of status changes</summary>
        public List<TaskHistoryEntry> History { get; set; } = new List<TaskHistoryEntry>();
    }

    /// <summary>
    /// Result data when completing a task.
    /// </summary>
    public class TaskCompletionResult
    {
        /// <summary>Whether the task succeeded</summary>
        public bool Success { get; set; } = true;

        /// <summary>Human-readable summary</summary>
        public string Summary { get; set; }

        /// <summary>Files created</summary>
        public List<string> FilesCreated { get; set; } = new List<string>();

        /// <summary>Files modified</summary>
        public List<string> FilesModified { get; set; } = new List<string>();

        /// <summary>Any warnings or notes</summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>Verification results from Eyes Polecat</summary>
        public VerificationResult Verification { get; set; }
    }

    /// <summary>
    /// Verification result from Eyes Polecat.
    /// </summary>
    public class VerificationResult
    {
        /// <summary>Whether verification passed</summary>
        public bool Passed { get; set; }

        /// <summary>Console errors detected</summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>Console warnings detected</summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>Missing references found</summary>
        public List<string> MissingReferences { get; set; } = new List<string>();

        /// <summary>Timestamp of verification</summary>
        public string VerifiedAt { get; set; }
    }

    /// <summary>
    /// Entry in task history tracking status changes.
    /// </summary>
    public class TaskHistoryEntry
    {
        /// <summary>ISO timestamp</summary>
        public string Timestamp { get; set; }

        /// <summary>Previous status</summary>
        public TaskStatus? FromStatus { get; set; }

        /// <summary>New status</summary>
        public TaskStatus ToStatus { get; set; }

        /// <summary>Agent that made the change</summary>
        public AgentType? Agent { get; set; }

        /// <summary>Optional message</summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// Filter for querying tasks.
    /// </summary>
    public class TaskStatusFilter
    {
        /// <summary>Filter by status</summary>
        public TaskStatus? Status { get; set; }

        /// <summary>Filter by assigned agent type</summary>
        public AgentType? AgentType { get; set; }

        /// <summary>Filter by convoy ID</summary>
        public string ConvoyId { get; set; }

        /// <summary>Filter by iteration</summary>
        public string Iteration { get; set; }

        /// <summary>Include completed tasks</summary>
        public bool IncludeCompleted { get; set; } = false;

        /// <summary>Maximum results</summary>
        public int? Limit { get; set; }
    }

    /// <summary>
    /// Convoy lifecycle status.
    /// </summary>
    public enum ConvoyStatus
    {
        /// <summary>Convoy created, waiting to start</summary>
        Pending,

        /// <summary>Convoy is blocked by dependencies</summary>
        Blocked,

        /// <summary>Convoy has tasks in progress</summary>
        InProgress,

        /// <summary>All tasks completed successfully</summary>
        Completed,

        /// <summary>Convoy failed (one or more tasks failed)</summary>
        Failed,

        /// <summary>Convoy was cancelled</summary>
        Cancelled
    }

    /// <summary>
    /// Configuration for creating a new convoy.
    /// </summary>
    public class ConvoyConfig
    {
        /// <summary>Human-readable convoy name</summary>
        public string Name { get; set; }

        /// <summary>Detailed description of convoy goal</summary>
        public string Description { get; set; }

        /// <summary>Iteration folder</summary>
        public string Iteration { get; set; }

        /// <summary>Convoy IDs this convoy depends on</summary>
        public List<string> Dependencies { get; set; } = new List<string>();

        /// <summary>Task IDs in this convoy</summary>
        public List<string> TaskIds { get; set; } = new List<string>();

        /// <summary>Priority level</summary>
        public int Priority { get; set; } = 1;

        /// <summary>Default agent type for tasks in this convoy</summary>
        public AgentType? AssignedAgent { get; set; }

        /// <summary>Expected deliverable files</summary>
        public List<string> Deliverables { get; set; } = new List<string>();

        /// <summary>Completion criteria (human-readable)</summary>
        public List<string> CompletionCriteria { get; set; } = new List<string>();
    }

    /// <summary>
    /// Full convoy information including status and progress.
    /// </summary>
    public class ConvoyInfo
    {
        /// <summary>Unique convoy identifier (e.g., "convoy-001")</summary>
        public string Id { get; set; }

        /// <summary>Human-readable convoy name</summary>
        public string Name { get; set; }

        /// <summary>Detailed description</summary>
        public string Description { get; set; }

        /// <summary>Iteration folder</summary>
        public string Iteration { get; set; }

        /// <summary>Current status</summary>
        public ConvoyStatus Status { get; set; }

        /// <summary>Convoy IDs this convoy depends on</summary>
        public List<string> Dependencies { get; set; } = new List<string>();

        /// <summary>Task IDs in this convoy</summary>
        public List<string> TaskIds { get; set; } = new List<string>();

        /// <summary>Priority level</summary>
        public int Priority { get; set; }

        /// <summary>Default agent type for tasks</summary>
        public AgentType? AssignedAgent { get; set; }

        /// <summary>Expected deliverables</summary>
        public List<string> Deliverables { get; set; } = new List<string>();

        /// <summary>Completion criteria</summary>
        public List<string> CompletionCriteria { get; set; } = new List<string>();

        /// <summary>ISO timestamp when created</summary>
        public string CreatedAt { get; set; }

        /// <summary>ISO timestamp when completed</summary>
        public string CompletedAt { get; set; }

        /// <summary>Error message if failed</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Progress summary</summary>
        public ConvoyProgress Progress { get; set; }
    }

    /// <summary>
    /// Progress summary for a convoy.
    /// </summary>
    public class ConvoyProgress
    {
        public int TotalTasks { get; set; }
        public int PendingTasks { get; set; }
        public int AssignedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int FailedTasks { get; set; }
        public int BlockedTasks { get; set; }

        public int PercentComplete => TotalTasks > 0
            ? (CompletedTasks * 100) / TotalTasks
            : 0;

        public bool AllTasksComplete => TotalTasks > 0 && CompletedTasks == TotalTasks;
        public bool HasFailures => FailedTasks > 0;
    }

    /// <summary>
    /// Filter for querying convoys.
    /// </summary>
    public class ConvoyStatusFilter
    {
        /// <summary>Filter by status</summary>
        public ConvoyStatus? Status { get; set; }

        /// <summary>Filter by iteration</summary>
        public string Iteration { get; set; }

        /// <summary>Include completed convoys</summary>
        public bool IncludeCompleted { get; set; } = false;

        /// <summary>Maximum results</summary>
        public int? Limit { get; set; }
    }
}
