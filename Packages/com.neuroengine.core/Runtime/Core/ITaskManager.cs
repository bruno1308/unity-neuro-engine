using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for managing tasks assigned to AI agents.
    /// This is Layer 4 (Persistence) - task state survives domain reloads.
    ///
    /// Tasks represent discrete units of work that can be assigned to agents,
    /// tracked for progress, and organized into convoys (related task groups).
    /// </summary>
    public interface ITaskManager
    {
        /// <summary>
        /// Create a new task.
        /// </summary>
        AgentTask CreateTask(TaskAssignment assignment);

        /// <summary>
        /// Get a task by ID.
        /// </summary>
        AgentTask GetTask(string taskId);

        /// <summary>
        /// Update task progress.
        /// </summary>
        void UpdateProgress(string taskId, TaskProgress progress);

        /// <summary>
        /// Mark task as started by an agent.
        /// </summary>
        void StartTask(string taskId, string agentName);

        /// <summary>
        /// Mark task as completed successfully.
        /// </summary>
        void CompleteTask(string taskId, TaskResult result);

        /// <summary>
        /// Mark task as failed.
        /// </summary>
        void FailTask(string taskId, string errorMessage);

        /// <summary>
        /// Cancel a task.
        /// </summary>
        void CancelTask(string taskId, string reason);

        /// <summary>
        /// List tasks with optional filtering.
        /// </summary>
        Task<List<AgentTask>> ListTasksAsync(TaskFilter filter = null);

        /// <summary>
        /// Get the next available task for an agent type.
        /// </summary>
        AgentTask GetNextTask(string agentType);

        /// <summary>
        /// Create a convoy (group of related tasks).
        /// </summary>
        Convoy CreateConvoy(string name, string description, List<string> taskIds);

        /// <summary>
        /// Get convoy by ID.
        /// </summary>
        Convoy GetConvoy(string convoyId);

        /// <summary>
        /// List all convoys.
        /// </summary>
        Task<List<Convoy>> ListConvoysAsync();
    }

    /// <summary>
    /// Assignment details for creating a new task.
    /// </summary>
    public class TaskAssignment
    {
        /// <summary>Human-readable task description</summary>
        public string Description;

        /// <summary>Type of agent that should handle this: "script", "art", "audio", "test"</summary>
        public string AgentType;

        /// <summary>Priority: 0=low, 1=normal, 2=high, 3=critical</summary>
        public int Priority;

        /// <summary>Optional parent task ID (for subtasks)</summary>
        public string ParentTaskId;

        /// <summary>Optional convoy ID this task belongs to</summary>
        public string ConvoyId;

        /// <summary>Additional context data for the agent</summary>
        public Dictionary<string, object> Context = new Dictionary<string, object>();

        /// <summary>Files that should be read before starting</summary>
        public List<string> InputFiles = new List<string>();

        /// <summary>Expected output files</summary>
        public List<string> ExpectedOutputs = new List<string>();

        /// <summary>Tags for filtering and organization</summary>
        public List<string> Tags = new List<string>();
    }

    /// <summary>
    /// A task assigned to an AI agent.
    /// </summary>
    public class AgentTask
    {
        /// <summary>Unique task identifier (auto-generated)</summary>
        public string TaskId;

        /// <summary>The original assignment</summary>
        public TaskAssignment Assignment;

        /// <summary>Current status: "pending", "in_progress", "completed", "failed", "cancelled"</summary>
        public string Status;

        /// <summary>Agent currently working on this task (null if pending)</summary>
        public string AssignedAgent;

        /// <summary>ISO timestamp when created</summary>
        public string CreatedAt;

        /// <summary>ISO timestamp when started</summary>
        public string StartedAt;

        /// <summary>ISO timestamp when completed/failed</summary>
        public string CompletedAt;

        /// <summary>Current progress information</summary>
        public TaskProgress Progress;

        /// <summary>Final result (null if not completed)</summary>
        public TaskResult Result;

        /// <summary>Error message if failed</summary>
        public string ErrorMessage;

        /// <summary>Path to the transcript file</summary>
        public string TranscriptPath;
    }

    /// <summary>
    /// Progress update for a task.
    /// </summary>
    public class TaskProgress
    {
        /// <summary>Current step description</summary>
        public string CurrentStep;

        /// <summary>Percentage complete (0-100)</summary>
        public int PercentComplete;

        /// <summary>Number of steps completed</summary>
        public int StepsCompleted;

        /// <summary>Total steps (if known)</summary>
        public int? TotalSteps;

        /// <summary>ISO timestamp of last update</summary>
        public string LastUpdated;

        /// <summary>Any blockers or issues</summary>
        public string Blockers;
    }

    /// <summary>
    /// Result of a completed task.
    /// </summary>
    public class TaskResult
    {
        /// <summary>Whether the task succeeded</summary>
        public bool Success;

        /// <summary>Human-readable summary</summary>
        public string Summary;

        /// <summary>Files created</summary>
        public List<string> FilesCreated = new List<string>();

        /// <summary>Files modified</summary>
        public List<string> FilesModified = new List<string>();

        /// <summary>Any warnings or notes</summary>
        public List<string> Warnings = new List<string>();

        /// <summary>Suggested follow-up tasks</summary>
        public List<string> SuggestedFollowUps = new List<string>();
    }

    /// <summary>
    /// Filter for listing tasks.
    /// </summary>
    public class TaskFilter
    {
        /// <summary>Filter by status</summary>
        public string Status;

        /// <summary>Filter by agent type</summary>
        public string AgentType;

        /// <summary>Filter by assigned agent</summary>
        public string AssignedAgent;

        /// <summary>Filter by convoy ID</summary>
        public string ConvoyId;

        /// <summary>Filter by tags (any match)</summary>
        public List<string> Tags;

        /// <summary>Include completed tasks (default: false)</summary>
        public bool IncludeCompleted;

        /// <summary>Maximum number of results</summary>
        public int? Limit;
    }

    /// <summary>
    /// A convoy is a group of related tasks that should be executed together.
    /// Convoys enable multi-agent coordination on complex features.
    /// </summary>
    public class Convoy
    {
        /// <summary>Unique convoy identifier</summary>
        public string ConvoyId;

        /// <summary>Human-readable name</summary>
        public string Name;

        /// <summary>Description of the convoy's goal</summary>
        public string Description;

        /// <summary>ISO timestamp when created</summary>
        public string CreatedAt;

        /// <summary>Current status: "pending", "in_progress", "completed", "failed"</summary>
        public string Status;

        /// <summary>List of task IDs in this convoy</summary>
        public List<string> TaskIds = new List<string>();

        /// <summary>Progress summary</summary>
        public ConvoyProgress Progress;
    }

    /// <summary>
    /// Progress summary for a convoy.
    /// </summary>
    public class ConvoyProgress
    {
        public int TotalTasks;
        public int PendingTasks;
        public int InProgressTasks;
        public int CompletedTasks;
        public int FailedTasks;
        public int PercentComplete => TotalTasks > 0 ? (CompletedTasks * 100) / TotalTasks : 0;
    }
}
