using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NeuroEngine.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
// Resolve ambiguity with System.Threading.Tasks.TaskStatus
using TaskStatus = NeuroEngine.Core.TaskStatus;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Layer 6: Agent Orchestration Service
    ///
    /// Manages task lifecycle (pending, assigned, in_progress, completed, failed).
    /// Tracks task assignment to agent types and supports iteration counting for retry logic.
    /// Persists all state to hooks/tasks/{taskId}.json for session survival.
    ///
    /// This service is used by the Mayor agent to orchestrate work across
    /// specialized Polecat agents (Script, Scene, Asset, Eyes).
    /// </summary>
    public class OrchestrationService : IOrchestration
    {
        private readonly string _tasksRoot;
        private readonly string _convoysRoot;
        private readonly ConcurrentDictionary<string, TaskInfo> _taskCache = new ConcurrentDictionary<string, TaskInfo>();
        private readonly ConcurrentDictionary<string, ConvoyInfo> _convoyCache = new ConcurrentDictionary<string, ConvoyInfo>();
        private readonly ConcurrentDictionary<string, object> _taskLocks = new ConcurrentDictionary<string, object>();
        private readonly ConcurrentDictionary<string, object> _convoyLocks = new ConcurrentDictionary<string, object>();
        private int _taskCounter;
        private int _convoyCounter;
        private readonly object _counterLock = new object();
        private readonly object _fileLock = new object();

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new List<JsonConverter> { new StringEnumConverter() }
        };

        // Valid task state transitions
        private static readonly Dictionary<TaskStatus, HashSet<TaskStatus>> _validTaskTransitions =
            new Dictionary<TaskStatus, HashSet<TaskStatus>>
            {
                { TaskStatus.Pending, new HashSet<TaskStatus> { TaskStatus.Assigned, TaskStatus.Blocked, TaskStatus.Cancelled } },
                { TaskStatus.Blocked, new HashSet<TaskStatus> { TaskStatus.Pending, TaskStatus.Cancelled } },
                { TaskStatus.Assigned, new HashSet<TaskStatus> { TaskStatus.InProgress, TaskStatus.Pending, TaskStatus.Cancelled } },
                { TaskStatus.InProgress, new HashSet<TaskStatus> { TaskStatus.Completed, TaskStatus.Failed, TaskStatus.Cancelled } },
                { TaskStatus.Completed, new HashSet<TaskStatus>() }, // Terminal
                { TaskStatus.Failed, new HashSet<TaskStatus> { TaskStatus.Pending } }, // Can retry
                { TaskStatus.Cancelled, new HashSet<TaskStatus>() } // Terminal
            };

        // Valid convoy state transitions
        private static readonly Dictionary<ConvoyStatus, HashSet<ConvoyStatus>> _validConvoyTransitions =
            new Dictionary<ConvoyStatus, HashSet<ConvoyStatus>>
            {
                { ConvoyStatus.Pending, new HashSet<ConvoyStatus> { ConvoyStatus.InProgress, ConvoyStatus.Blocked, ConvoyStatus.Cancelled } },
                { ConvoyStatus.Blocked, new HashSet<ConvoyStatus> { ConvoyStatus.Pending, ConvoyStatus.InProgress, ConvoyStatus.Cancelled } },
                { ConvoyStatus.InProgress, new HashSet<ConvoyStatus> { ConvoyStatus.Completed, ConvoyStatus.Failed, ConvoyStatus.Cancelled } },
                { ConvoyStatus.Completed, new HashSet<ConvoyStatus>() }, // Terminal
                { ConvoyStatus.Failed, new HashSet<ConvoyStatus> { ConvoyStatus.Pending } }, // Can retry
                { ConvoyStatus.Cancelled, new HashSet<ConvoyStatus>() } // Terminal
            };

        /// <summary>
        /// Constructor with dependency injection.
        /// </summary>
        public OrchestrationService(IEnvConfig config)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var hooksPath = config.HooksPath;

            if (hooksPath.StartsWith("./"))
                hooksPath = hooksPath.Substring(2);

            _tasksRoot = Path.Combine(projectRoot, hooksPath, "tasks");
            _convoysRoot = Path.Combine(projectRoot, hooksPath, "convoys");

            EnsureDirectories();
            InitializeCounters();
        }

        /// <summary>
        /// Parameterless constructor for editor/standalone use.
        /// </summary>
        public OrchestrationService()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            _tasksRoot = Path.Combine(projectRoot, "hooks", "tasks");
            _convoysRoot = Path.Combine(projectRoot, "hooks", "convoys");

            EnsureDirectories();
            InitializeCounters();
        }

        private void EnsureDirectories()
        {
            Directory.CreateDirectory(_tasksRoot);
            Directory.CreateDirectory(_convoysRoot);
        }

        private void InitializeCounters()
        {
            // Initialize task counter from existing files
            if (Directory.Exists(_tasksRoot))
            {
                var existingFiles = Directory.GetFiles(_tasksRoot, "task-*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => n.StartsWith("task-"))
                    .Select(n =>
                    {
                        var numPart = n.Substring(5); // After "task-"
                        return int.TryParse(numPart, out var num) ? num : 0;
                    })
                    .DefaultIfEmpty(0)
                    .Max();

                _taskCounter = existingFiles;
            }

            // Initialize convoy counter from existing files
            if (Directory.Exists(_convoysRoot))
            {
                var existingFiles = Directory.GetFiles(_convoysRoot, "convoy-*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => n.StartsWith("convoy-"))
                    .Select(n =>
                    {
                        var numPart = n.Substring(7); // After "convoy-"
                        return int.TryParse(numPart, out var num) ? num : 0;
                    })
                    .DefaultIfEmpty(0)
                    .Max();

                _convoyCounter = existingFiles;
            }
        }

        #region Task Operations

        public TaskInfo CreateTask(TaskConfig config)
        {
            if (string.IsNullOrEmpty(config.Name))
                throw new ArgumentException("Task name is required", nameof(config));

            int newCounter;
            lock (_counterLock)
            {
                newCounter = ++_taskCounter;
            }

            var taskId = $"task-{newCounter:D3}";
            var now = DateTime.UtcNow.ToString("o");

            var task = new TaskInfo
            {
                Id = taskId,
                Name = config.Name,
                Description = config.Description,
                Iteration = config.Iteration,
                ConvoyId = config.ConvoyId,
                Status = TaskStatus.Pending,
                Dependencies = config.Dependencies ?? new List<string>(),
                Priority = config.Priority,
                Deliverable = config.Deliverable,
                SuccessCriteria = config.SuccessCriteria ?? new List<string>(),
                Specification = config.Specification ?? new Dictionary<string, object>(),
                EstimatedMinutes = config.EstimatedMinutes,
                MaxIterations = config.MaxIterations,
                IterationCount = 0,
                CreatedAt = now,
                History = new List<TaskHistoryEntry>
                {
                    new TaskHistoryEntry
                    {
                        Timestamp = now,
                        ToStatus = TaskStatus.Pending,
                        Message = "Task created"
                    }
                }
            };

            // Check if blocked by dependencies
            if (task.Dependencies.Count > 0)
            {
                var allDepsComplete = task.Dependencies.All(depId =>
                {
                    var dep = GetTaskStatus(depId);
                    return dep?.Status == TaskStatus.Completed;
                });

                if (!allDepsComplete)
                {
                    task.Status = TaskStatus.Blocked;
                    task.History.Add(new TaskHistoryEntry
                    {
                        Timestamp = now,
                        FromStatus = TaskStatus.Pending,
                        ToStatus = TaskStatus.Blocked,
                        Message = "Blocked by pending dependencies"
                    });
                }
            }

            SaveTask(task);
            Debug.Log($"[Orchestration] Created task {taskId}: {config.Name}");
            return task;
        }

        public void AssignTask(string taskId, AgentType agentType)
        {
            var task = GetTaskStatus(taskId);
            if (task == null)
                throw new ArgumentException($"Task '{taskId}' not found");

            if (agentType == AgentType.Mayor)
                throw new InvalidOperationException("Mayor should not be assigned tasks - it orchestrates, not executes");

            lock (GetTaskLock(taskId))
            {
                // Reload inside lock
                task = LoadTask(taskId);

                if (!ValidateTaskTransition(task.Status, TaskStatus.Assigned))
                {
                    Debug.LogWarning($"[Orchestration] Invalid transition for {taskId}: {task.Status} -> Assigned");
                    return;
                }

                var now = DateTime.UtcNow.ToString("o");
                task.Status = TaskStatus.Assigned;
                task.AssignedAgent = agentType;
                task.AssignedAt = now;
                task.History.Add(new TaskHistoryEntry
                {
                    Timestamp = now,
                    FromStatus = TaskStatus.Pending,
                    ToStatus = TaskStatus.Assigned,
                    Agent = agentType,
                    Message = $"Assigned to {agentType}"
                });

                SaveTask(task);
            }

            Debug.Log($"[Orchestration] Task {taskId} assigned to {agentType}");
        }

        public TaskInfo GetTaskStatus(string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
                return null;

            // Check cache first
            if (_taskCache.TryGetValue(taskId, out var cached))
                return cached;

            return LoadTask(taskId);
        }

        public void CompleteTask(string taskId, TaskCompletionResult result)
        {
            var task = GetTaskStatus(taskId);
            if (task == null)
                throw new ArgumentException($"Task '{taskId}' not found");

            lock (GetTaskLock(taskId))
            {
                task = LoadTask(taskId);

                if (!ValidateTaskTransition(task.Status, TaskStatus.Completed))
                {
                    Debug.LogWarning($"[Orchestration] Invalid transition for {taskId}: {task.Status} -> Completed");
                    return;
                }

                var now = DateTime.UtcNow.ToString("o");
                task.Status = TaskStatus.Completed;
                task.CompletedAt = now;
                task.Result = result;
                task.History.Add(new TaskHistoryEntry
                {
                    Timestamp = now,
                    FromStatus = TaskStatus.InProgress,
                    ToStatus = TaskStatus.Completed,
                    Agent = task.AssignedAgent,
                    Message = result?.Summary ?? "Task completed"
                });

                SaveTask(task);

                // Check if any blocked tasks can now be unblocked
                UnblockDependentTasks(taskId);
            }

            Debug.Log($"[Orchestration] Task {taskId} completed: {result?.Summary}");

            // Update convoy status if applicable
            if (!string.IsNullOrEmpty(task.ConvoyId))
            {
                UpdateConvoyStatus(task.ConvoyId);
            }
        }

        public void FailTask(string taskId, string reason)
        {
            var task = GetTaskStatus(taskId);
            if (task == null)
                throw new ArgumentException($"Task '{taskId}' not found");

            lock (GetTaskLock(taskId))
            {
                task = LoadTask(taskId);

                if (!ValidateTaskTransition(task.Status, TaskStatus.Failed))
                {
                    Debug.LogWarning($"[Orchestration] Invalid transition for {taskId}: {task.Status} -> Failed");
                    return;
                }

                var now = DateTime.UtcNow.ToString("o");
                task.Status = TaskStatus.Failed;
                task.CompletedAt = now;
                task.ErrorMessage = reason;
                task.IterationCount++; // Increment for retry tracking
                task.History.Add(new TaskHistoryEntry
                {
                    Timestamp = now,
                    FromStatus = TaskStatus.InProgress,
                    ToStatus = TaskStatus.Failed,
                    Agent = task.AssignedAgent,
                    Message = $"Failed (iteration {task.IterationCount}): {reason}"
                });

                SaveTask(task);
            }

            Debug.Log($"[Orchestration] Task {taskId} failed (iteration {task.IterationCount}): {reason}");

            // Check if max iterations exceeded
            if (task.IterationCount >= task.MaxIterations)
            {
                Debug.LogWarning($"[Orchestration] Task {taskId} exceeded max iterations ({task.MaxIterations}). Escalation required.");
            }
        }

        public async Task<List<TaskInfo>> GetTasksAsync(TaskStatusFilter filter = null)
        {
            var tasks = new List<TaskInfo>();

            if (!Directory.Exists(_tasksRoot))
                return tasks;

            var files = Directory.GetFiles(_tasksRoot, "task-*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var task = JsonConvert.DeserializeObject<TaskInfo>(json, _jsonSettings);

                    // Apply filters
                    if (filter != null)
                    {
                        if (filter.Status.HasValue && task.Status != filter.Status.Value)
                            continue;
                        if (filter.AgentType.HasValue && task.AssignedAgent != filter.AgentType.Value)
                            continue;
                        if (!string.IsNullOrEmpty(filter.ConvoyId) && task.ConvoyId != filter.ConvoyId)
                            continue;
                        if (!string.IsNullOrEmpty(filter.Iteration) && task.Iteration != filter.Iteration)
                            continue;
                        if (!filter.IncludeCompleted &&
                            (task.Status == TaskStatus.Completed || task.Status == TaskStatus.Cancelled))
                            continue;
                    }

                    tasks.Add(task);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Orchestration] Failed to load task from {file}: {e.Message}");
                }
            }

            // Sort by priority (desc), then created date (asc)
            var sorted = tasks
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .ToList();

            if (filter?.Limit > 0)
                return sorted.Take(filter.Limit.Value).ToList();

            return sorted;
        }

        public int IncrementTaskIteration(string taskId)
        {
            var task = GetTaskStatus(taskId);
            if (task == null)
                throw new ArgumentException($"Task '{taskId}' not found");

            lock (GetTaskLock(taskId))
            {
                task = LoadTask(taskId);
                task.IterationCount++;
                task.History.Add(new TaskHistoryEntry
                {
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    ToStatus = task.Status,
                    Message = $"Iteration incremented to {task.IterationCount}"
                });

                SaveTask(task);
                return task.IterationCount;
            }
        }

        /// <summary>
        /// Start a task (transition from Assigned to InProgress).
        /// </summary>
        public void StartTask(string taskId)
        {
            var task = GetTaskStatus(taskId);
            if (task == null)
                throw new ArgumentException($"Task '{taskId}' not found");

            lock (GetTaskLock(taskId))
            {
                task = LoadTask(taskId);

                if (!ValidateTaskTransition(task.Status, TaskStatus.InProgress))
                {
                    Debug.LogWarning($"[Orchestration] Invalid transition for {taskId}: {task.Status} -> InProgress");
                    return;
                }

                var now = DateTime.UtcNow.ToString("o");
                task.Status = TaskStatus.InProgress;
                task.StartedAt = now;
                task.History.Add(new TaskHistoryEntry
                {
                    Timestamp = now,
                    FromStatus = TaskStatus.Assigned,
                    ToStatus = TaskStatus.InProgress,
                    Agent = task.AssignedAgent,
                    Message = "Task started"
                });

                SaveTask(task);
            }

            Debug.Log($"[Orchestration] Task {taskId} started");

            // Update convoy status
            if (!string.IsNullOrEmpty(task.ConvoyId))
            {
                UpdateConvoyStatus(task.ConvoyId);
            }
        }

        /// <summary>
        /// Reset a failed task back to pending for retry.
        /// </summary>
        public void RetryTask(string taskId)
        {
            var task = GetTaskStatus(taskId);
            if (task == null)
                throw new ArgumentException($"Task '{taskId}' not found");

            if (task.Status != TaskStatus.Failed)
                throw new InvalidOperationException($"Can only retry failed tasks. Current status: {task.Status}");

            lock (GetTaskLock(taskId))
            {
                task = LoadTask(taskId);

                var now = DateTime.UtcNow.ToString("o");
                task.Status = TaskStatus.Pending;
                task.AssignedAgent = null;
                task.AssignedAt = null;
                task.StartedAt = null;
                task.CompletedAt = null;
                task.ErrorMessage = null;
                task.History.Add(new TaskHistoryEntry
                {
                    Timestamp = now,
                    FromStatus = TaskStatus.Failed,
                    ToStatus = TaskStatus.Pending,
                    Message = $"Retrying (iteration {task.IterationCount})"
                });

                SaveTask(task);
            }

            Debug.Log($"[Orchestration] Task {taskId} reset for retry (iteration {task.IterationCount})");
        }

        private void UnblockDependentTasks(string completedTaskId)
        {
            // Find tasks blocked by this one and check if they can be unblocked
            var allTasks = GetTasksAsync(new TaskStatusFilter { Status = TaskStatus.Blocked, IncludeCompleted = false })
                .GetAwaiter().GetResult();

            foreach (var task in allTasks.Where(t => t.Dependencies?.Contains(completedTaskId) == true))
            {
                var allDepsComplete = task.Dependencies.All(depId =>
                {
                    var dep = GetTaskStatus(depId);
                    return dep?.Status == TaskStatus.Completed;
                });

                if (allDepsComplete)
                {
                    lock (GetTaskLock(task.Id))
                    {
                        var t = LoadTask(task.Id);
                        t.Status = TaskStatus.Pending;
                        t.History.Add(new TaskHistoryEntry
                        {
                            Timestamp = DateTime.UtcNow.ToString("o"),
                            FromStatus = TaskStatus.Blocked,
                            ToStatus = TaskStatus.Pending,
                            Message = "Dependencies satisfied, task unblocked"
                        });
                        SaveTask(t);
                    }
                    Debug.Log($"[Orchestration] Task {task.Id} unblocked (dependency {completedTaskId} completed)");
                }
            }
        }

        private TaskInfo LoadTask(string taskId)
        {
            var path = Path.Combine(_tasksRoot, $"{taskId}.json");
            if (!File.Exists(path))
                return null;

            lock (_fileLock)
            {
                var json = File.ReadAllText(path);
                var task = JsonConvert.DeserializeObject<TaskInfo>(json, _jsonSettings);
                _taskCache.AddOrUpdate(taskId, task, (_, __) => task);
                return task;
            }
        }

        private void SaveTask(TaskInfo task)
        {
            var path = Path.Combine(_tasksRoot, $"{task.Id}.json");
            var json = JsonConvert.SerializeObject(task, _jsonSettings);
            WriteFileAtomic(path, json);
            _taskCache.AddOrUpdate(task.Id, task, (_, __) => task);
        }

        private object GetTaskLock(string taskId)
        {
            return _taskLocks.GetOrAdd(taskId, _ => new object());
        }

        private bool ValidateTaskTransition(TaskStatus current, TaskStatus target)
        {
            if (!_validTaskTransitions.TryGetValue(current, out var allowed))
                return false;
            return allowed.Contains(target);
        }

        #endregion

        #region Convoy Operations

        public ConvoyInfo CreateConvoy(ConvoyConfig config)
        {
            if (string.IsNullOrEmpty(config.Name))
                throw new ArgumentException("Convoy name is required", nameof(config));

            int newCounter;
            lock (_counterLock)
            {
                newCounter = ++_convoyCounter;
            }

            var convoyId = $"convoy-{newCounter:D3}";
            var now = DateTime.UtcNow.ToString("o");

            var convoy = new ConvoyInfo
            {
                Id = convoyId,
                Name = config.Name,
                Description = config.Description,
                Iteration = config.Iteration,
                Status = ConvoyStatus.Pending,
                Dependencies = config.Dependencies ?? new List<string>(),
                TaskIds = config.TaskIds ?? new List<string>(),
                Priority = config.Priority,
                AssignedAgent = config.AssignedAgent,
                Deliverables = config.Deliverables ?? new List<string>(),
                CompletionCriteria = config.CompletionCriteria ?? new List<string>(),
                CreatedAt = now,
                Progress = new ConvoyProgress()
            };

            // Check if blocked by dependencies
            if (convoy.Dependencies.Count > 0)
            {
                var allDepsComplete = convoy.Dependencies.All(depId =>
                {
                    var dep = GetConvoyStatus(depId);
                    return dep?.Status == ConvoyStatus.Completed;
                });

                if (!allDepsComplete)
                {
                    convoy.Status = ConvoyStatus.Blocked;
                }
            }

            // Update tasks with convoy ID and calculate progress
            foreach (var taskId in convoy.TaskIds)
            {
                var task = GetTaskStatus(taskId);
                if (task != null && string.IsNullOrEmpty(task.ConvoyId))
                {
                    lock (GetTaskLock(taskId))
                    {
                        task = LoadTask(taskId);
                        task.ConvoyId = convoyId;
                        SaveTask(task);
                    }
                }
            }

            convoy.Progress = CalculateConvoyProgress(convoy.TaskIds);
            SaveConvoy(convoy);

            Debug.Log($"[Orchestration] Created convoy {convoyId}: {config.Name}");
            return convoy;
        }

        public ConvoyInfo GetConvoyStatus(string convoyId)
        {
            if (string.IsNullOrEmpty(convoyId))
                return null;

            var convoy = LoadConvoy(convoyId);
            if (convoy != null)
            {
                convoy.Progress = CalculateConvoyProgress(convoy.TaskIds);
            }
            return convoy;
        }

        public void CompleteConvoy(string convoyId)
        {
            var convoy = GetConvoyStatus(convoyId);
            if (convoy == null)
                throw new ArgumentException($"Convoy '{convoyId}' not found");

            lock (GetConvoyLock(convoyId))
            {
                convoy = LoadConvoy(convoyId);
                convoy.Progress = CalculateConvoyProgress(convoy.TaskIds);

                // Verify all tasks are complete
                if (!convoy.Progress.AllTasksComplete)
                {
                    Debug.LogWarning($"[Orchestration] Cannot complete convoy {convoyId}: not all tasks are complete " +
                                     $"({convoy.Progress.CompletedTasks}/{convoy.Progress.TotalTasks})");
                    return;
                }

                if (!ValidateConvoyTransition(convoy.Status, ConvoyStatus.Completed))
                {
                    Debug.LogWarning($"[Orchestration] Invalid transition for {convoyId}: {convoy.Status} -> Completed");
                    return;
                }

                convoy.Status = ConvoyStatus.Completed;
                convoy.CompletedAt = DateTime.UtcNow.ToString("o");
                SaveConvoy(convoy);

                // Check if any blocked convoys can now be unblocked
                UnblockDependentConvoys(convoyId);
            }

            Debug.Log($"[Orchestration] Convoy {convoyId} completed");
        }

        public void FailConvoy(string convoyId, string reason)
        {
            var convoy = GetConvoyStatus(convoyId);
            if (convoy == null)
                throw new ArgumentException($"Convoy '{convoyId}' not found");

            lock (GetConvoyLock(convoyId))
            {
                convoy = LoadConvoy(convoyId);

                if (!ValidateConvoyTransition(convoy.Status, ConvoyStatus.Failed))
                {
                    Debug.LogWarning($"[Orchestration] Invalid transition for {convoyId}: {convoy.Status} -> Failed");
                    return;
                }

                convoy.Status = ConvoyStatus.Failed;
                convoy.CompletedAt = DateTime.UtcNow.ToString("o");
                convoy.ErrorMessage = reason;
                SaveConvoy(convoy);
            }

            Debug.Log($"[Orchestration] Convoy {convoyId} failed: {reason}");
        }

        public async Task<List<ConvoyInfo>> GetConvoysAsync(ConvoyStatusFilter filter = null)
        {
            var convoys = new List<ConvoyInfo>();

            if (!Directory.Exists(_convoysRoot))
                return convoys;

            var files = Directory.GetFiles(_convoysRoot, "convoy-*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var convoy = JsonConvert.DeserializeObject<ConvoyInfo>(json, _jsonSettings);
                    convoy.Progress = CalculateConvoyProgress(convoy.TaskIds);

                    // Apply filters
                    if (filter != null)
                    {
                        if (filter.Status.HasValue && convoy.Status != filter.Status.Value)
                            continue;
                        if (!string.IsNullOrEmpty(filter.Iteration) && convoy.Iteration != filter.Iteration)
                            continue;
                        if (!filter.IncludeCompleted &&
                            (convoy.Status == ConvoyStatus.Completed || convoy.Status == ConvoyStatus.Cancelled))
                            continue;
                    }

                    convoys.Add(convoy);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Orchestration] Failed to load convoy from {file}: {e.Message}");
                }
            }

            // Sort by priority (desc), then created date (asc)
            var sorted = convoys
                .OrderByDescending(c => c.Priority)
                .ThenBy(c => c.CreatedAt)
                .ToList();

            if (filter?.Limit > 0)
                return sorted.Take(filter.Limit.Value).ToList();

            return sorted;
        }

        /// <summary>
        /// Add a task to an existing convoy.
        /// </summary>
        public void AddTaskToConvoy(string convoyId, string taskId)
        {
            var convoy = GetConvoyStatus(convoyId);
            if (convoy == null)
                throw new ArgumentException($"Convoy '{convoyId}' not found");

            var task = GetTaskStatus(taskId);
            if (task == null)
                throw new ArgumentException($"Task '{taskId}' not found");

            lock (GetConvoyLock(convoyId))
            {
                convoy = LoadConvoy(convoyId);

                if (!convoy.TaskIds.Contains(taskId))
                {
                    convoy.TaskIds.Add(taskId);
                }

                convoy.Progress = CalculateConvoyProgress(convoy.TaskIds);
                SaveConvoy(convoy);
            }

            // Update task with convoy ID
            lock (GetTaskLock(taskId))
            {
                task = LoadTask(taskId);
                task.ConvoyId = convoyId;
                SaveTask(task);
            }

            Debug.Log($"[Orchestration] Added task {taskId} to convoy {convoyId}");
        }

        private void UpdateConvoyStatus(string convoyId)
        {
            var convoy = LoadConvoy(convoyId);
            if (convoy == null) return;

            lock (GetConvoyLock(convoyId))
            {
                convoy = LoadConvoy(convoyId);
                convoy.Progress = CalculateConvoyProgress(convoy.TaskIds);

                // Auto-update convoy status based on tasks
                if (convoy.Status == ConvoyStatus.Pending && convoy.Progress.InProgressTasks > 0)
                {
                    convoy.Status = ConvoyStatus.InProgress;
                }
                else if (convoy.Status == ConvoyStatus.Blocked)
                {
                    // Check if dependencies are now satisfied
                    var allDepsComplete = convoy.Dependencies.All(depId =>
                    {
                        var dep = GetConvoyStatus(depId);
                        return dep?.Status == ConvoyStatus.Completed;
                    });

                    if (allDepsComplete)
                    {
                        convoy.Status = convoy.Progress.InProgressTasks > 0
                            ? ConvoyStatus.InProgress
                            : ConvoyStatus.Pending;
                    }
                }

                SaveConvoy(convoy);
            }
        }

        private void UnblockDependentConvoys(string completedConvoyId)
        {
            var allConvoys = GetConvoysAsync(new ConvoyStatusFilter { Status = ConvoyStatus.Blocked })
                .GetAwaiter().GetResult();

            foreach (var convoy in allConvoys.Where(c => c.Dependencies?.Contains(completedConvoyId) == true))
            {
                var allDepsComplete = convoy.Dependencies.All(depId =>
                {
                    var dep = GetConvoyStatus(depId);
                    return dep?.Status == ConvoyStatus.Completed;
                });

                if (allDepsComplete)
                {
                    lock (GetConvoyLock(convoy.Id))
                    {
                        var c = LoadConvoy(convoy.Id);
                        c.Status = ConvoyStatus.Pending;
                        SaveConvoy(c);
                    }
                    Debug.Log($"[Orchestration] Convoy {convoy.Id} unblocked (dependency {completedConvoyId} completed)");
                }
            }
        }

        private ConvoyProgress CalculateConvoyProgress(List<string> taskIds)
        {
            var progress = new ConvoyProgress { TotalTasks = taskIds?.Count ?? 0 };

            if (taskIds == null || taskIds.Count == 0)
                return progress;

            foreach (var taskId in taskIds)
            {
                var task = GetTaskStatus(taskId);
                if (task == null) continue;

                switch (task.Status)
                {
                    case TaskStatus.Pending:
                        progress.PendingTasks++;
                        break;
                    case TaskStatus.Assigned:
                        progress.AssignedTasks++;
                        break;
                    case TaskStatus.InProgress:
                        progress.InProgressTasks++;
                        break;
                    case TaskStatus.Completed:
                        progress.CompletedTasks++;
                        break;
                    case TaskStatus.Failed:
                        progress.FailedTasks++;
                        break;
                    case TaskStatus.Blocked:
                        progress.BlockedTasks++;
                        break;
                }
            }

            return progress;
        }

        private ConvoyInfo LoadConvoy(string convoyId)
        {
            var path = Path.Combine(_convoysRoot, $"{convoyId}.json");
            if (!File.Exists(path))
                return null;

            lock (_fileLock)
            {
                var json = File.ReadAllText(path);
                var convoy = JsonConvert.DeserializeObject<ConvoyInfo>(json, _jsonSettings);
                _convoyCache.AddOrUpdate(convoyId, convoy, (_, __) => convoy);
                return convoy;
            }
        }

        private void SaveConvoy(ConvoyInfo convoy)
        {
            var path = Path.Combine(_convoysRoot, $"{convoy.Id}.json");
            var json = JsonConvert.SerializeObject(convoy, _jsonSettings);
            WriteFileAtomic(path, json);
            _convoyCache.AddOrUpdate(convoy.Id, convoy, (_, __) => convoy);
        }

        private object GetConvoyLock(string convoyId)
        {
            return _convoyLocks.GetOrAdd(convoyId, _ => new object());
        }

        private bool ValidateConvoyTransition(ConvoyStatus current, ConvoyStatus target)
        {
            if (!_validConvoyTransitions.TryGetValue(current, out var allowed))
                return false;
            return allowed.Contains(target);
        }

        #endregion

        #region File Operations

        private void WriteFileAtomic(string path, string content)
        {
            lock (_fileLock)
            {
                var tempPath = path + ".tmp";
                try
                {
                    File.WriteAllText(tempPath, content);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    File.Move(tempPath, path);
                }
                catch
                {
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); }
                        catch { }
                    }
                    throw;
                }
            }
        }

        #endregion
    }
}
