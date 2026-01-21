using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NeuroEngine.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Manages tasks assigned to AI agents.
    /// Tasks are persisted to hooks/tasks/{taskId}/assignment.json
    /// </summary>
    public class TaskManagerService : ITaskManager
    {
        private readonly string _tasksRoot;
        private readonly string _convoysRoot;
        private readonly ConcurrentDictionary<string, AgentTask> _taskCache = new ConcurrentDictionary<string, AgentTask>();
        private int _taskCounter;
        private readonly object _fileLock = new object();

        // Valid state transitions for task lifecycle
        private static readonly Dictionary<string, HashSet<string>> _validTransitions = new Dictionary<string, HashSet<string>>
        {
            { "pending", new HashSet<string> { "in_progress", "cancelled" } },
            { "in_progress", new HashSet<string> { "completed", "failed", "cancelled" } },
            { "completed", new HashSet<string>() },  // Terminal state
            { "failed", new HashSet<string>() },     // Terminal state
            { "cancelled", new HashSet<string>() }   // Terminal state
        };

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        public TaskManagerService(IEnvConfig config)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var hooksPath = config.HooksPath;

            if (hooksPath.StartsWith("./"))
                hooksPath = hooksPath.Substring(2);

            _tasksRoot = Path.Combine(projectRoot, hooksPath, "tasks");
            _convoysRoot = Path.Combine(projectRoot, hooksPath, "convoys");

            Directory.CreateDirectory(_tasksRoot);
            Directory.CreateDirectory(_convoysRoot);

            // Initialize counter from existing tasks
            InitializeTaskCounter();
        }

        /// <summary>
        /// Parameterless constructor for editor/standalone use.
        /// </summary>
        public TaskManagerService()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            _tasksRoot = Path.Combine(projectRoot, "hooks", "tasks");
            _convoysRoot = Path.Combine(projectRoot, "hooks", "convoys");

            Directory.CreateDirectory(_tasksRoot);
            Directory.CreateDirectory(_convoysRoot);

            InitializeTaskCounter();
        }

        private void InitializeTaskCounter()
        {
            if (Directory.Exists(_tasksRoot))
            {
                var existingTasks = Directory.GetDirectories(_tasksRoot)
                    .Select(d => Path.GetFileName(d))
                    .Where(n => n.StartsWith("task-"))
                    .Select(n => {
                        var parts = n.Split('-');
                        return parts.Length >= 2 && int.TryParse(parts[1], out var num) ? num : 0;
                    })
                    .DefaultIfEmpty(0)
                    .Max();

                _taskCounter = existingTasks;
            }
        }

        public AgentTask CreateTask(TaskAssignment assignment)
        {
            var newCounter = Interlocked.Increment(ref _taskCounter);
            var taskId = $"task-{newCounter:D4}";

            var task = new AgentTask
            {
                TaskId = taskId,
                Assignment = assignment,
                Status = "pending",
                CreatedAt = DateTime.UtcNow.ToString("o"),
                Progress = new TaskProgress
                {
                    CurrentStep = "Waiting for agent",
                    PercentComplete = 0,
                    LastUpdated = DateTime.UtcNow.ToString("o")
                }
            };

            // Create task directory and save
            var taskDir = Path.Combine(_tasksRoot, taskId);
            Directory.CreateDirectory(taskDir);
            Directory.CreateDirectory(Path.Combine(taskDir, "artifacts"));

            SaveTask(task);
            _taskCache[taskId] = task;

            Debug.Log($"[TaskManager] Created task {taskId}: {assignment.Description}");
            return task;
        }

        public AgentTask GetTask(string taskId)
        {
            // Check cache first
            if (_taskCache.TryGetValue(taskId, out var task))
                return task;

            // Load from disk
            var path = GetTaskPath(taskId);
            if (File.Exists(path))
            {
                lock (_fileLock)
                {
                    var json = File.ReadAllText(path);
                    task = JsonConvert.DeserializeObject<AgentTask>(json, _jsonSettings);
                }
                _taskCache.TryAdd(taskId, task);
                return task;
            }

            return null;
        }

        public void UpdateProgress(string taskId, TaskProgress progress)
        {
            var task = GetTask(taskId);
            if (task == null)
            {
                Debug.LogWarning($"[TaskManager] Task {taskId} not found");
                return;
            }

            progress.LastUpdated = DateTime.UtcNow.ToString("o");
            task.Progress = progress;
            SaveTask(task);
        }

        public void StartTask(string taskId, string agentName)
        {
            var task = GetTask(taskId);
            if (task == null)
            {
                Debug.LogWarning($"[TaskManager] Task {taskId} not found");
                return;
            }

            if (!ValidateStateTransition(task.Status, "in_progress"))
            {
                Debug.LogWarning($"[TaskManager] Invalid state transition for task {taskId}: {task.Status} -> in_progress");
                return;
            }

            task.Status = "in_progress";
            task.AssignedAgent = agentName;
            task.StartedAt = DateTime.UtcNow.ToString("o");
            task.Progress = new TaskProgress
            {
                CurrentStep = "Starting",
                PercentComplete = 0,
                LastUpdated = DateTime.UtcNow.ToString("o")
            };
            task.TranscriptPath = $"hooks/tasks/{taskId}/transcript.json";

            SaveTask(task);
            Debug.Log($"[TaskManager] Task {taskId} started by {agentName}");
        }

        public void CompleteTask(string taskId, TaskResult result)
        {
            var task = GetTask(taskId);
            if (task == null)
            {
                Debug.LogWarning($"[TaskManager] Task {taskId} not found");
                return;
            }

            if (!ValidateStateTransition(task.Status, "completed"))
            {
                Debug.LogWarning($"[TaskManager] Invalid state transition for task {taskId}: {task.Status} -> completed");
                return;
            }

            task.Status = "completed";
            task.CompletedAt = DateTime.UtcNow.ToString("o");
            task.Result = result;
            task.Progress = new TaskProgress
            {
                CurrentStep = "Completed",
                PercentComplete = 100,
                LastUpdated = DateTime.UtcNow.ToString("o")
            };

            SaveTask(task);
            Debug.Log($"[TaskManager] Task {taskId} completed: {result.Summary}");
        }

        public void FailTask(string taskId, string errorMessage)
        {
            var task = GetTask(taskId);
            if (task == null)
            {
                Debug.LogWarning($"[TaskManager] Task {taskId} not found");
                return;
            }

            if (!ValidateStateTransition(task.Status, "failed"))
            {
                Debug.LogWarning($"[TaskManager] Invalid state transition for task {taskId}: {task.Status} -> failed");
                return;
            }

            task.Status = "failed";
            task.CompletedAt = DateTime.UtcNow.ToString("o");
            task.ErrorMessage = errorMessage;
            if (task.Progress != null)
            {
                task.Progress.CurrentStep = "Failed";
            }

            SaveTask(task);
            Debug.Log($"[TaskManager] Task {taskId} failed: {errorMessage}");
        }

        public void CancelTask(string taskId, string reason)
        {
            var task = GetTask(taskId);
            if (task == null)
            {
                Debug.LogWarning($"[TaskManager] Task {taskId} not found");
                return;
            }

            if (!ValidateStateTransition(task.Status, "cancelled"))
            {
                Debug.LogWarning($"[TaskManager] Invalid state transition for task {taskId}: {task.Status} -> cancelled");
                return;
            }

            task.Status = "cancelled";
            task.CompletedAt = DateTime.UtcNow.ToString("o");
            task.ErrorMessage = $"Cancelled: {reason}";
            if (task.Progress != null)
            {
                task.Progress.CurrentStep = "Cancelled";
            }

            SaveTask(task);
            Debug.Log($"[TaskManager] Task {taskId} cancelled: {reason}");
        }

        public async Task<List<AgentTask>> ListTasksAsync(TaskFilter filter = null)
        {
            var tasks = new List<AgentTask>();

            if (!Directory.Exists(_tasksRoot))
                return tasks;

            var taskDirs = Directory.GetDirectories(_tasksRoot);

            foreach (var taskDir in taskDirs)
            {
                var assignmentPath = Path.Combine(taskDir, "assignment.json");
                if (!File.Exists(assignmentPath))
                    continue;

                try
                {
                    var json = await File.ReadAllTextAsync(assignmentPath);
                    var task = JsonConvert.DeserializeObject<AgentTask>(json, _jsonSettings);

                    // Apply filters
                    if (filter != null)
                    {
                        if (!string.IsNullOrEmpty(filter.Status) && task.Status != filter.Status)
                            continue;
                        if (!string.IsNullOrEmpty(filter.AgentType) && task.Assignment?.AgentType != filter.AgentType)
                            continue;
                        if (!string.IsNullOrEmpty(filter.AssignedAgent) && task.AssignedAgent != filter.AssignedAgent)
                            continue;
                        if (!string.IsNullOrEmpty(filter.ConvoyId) && task.Assignment?.ConvoyId != filter.ConvoyId)
                            continue;
                        if (!filter.IncludeCompleted && (task.Status == "completed" || task.Status == "cancelled"))
                            continue;
                        if (filter.Tags != null && filter.Tags.Count > 0)
                        {
                            var taskTags = task.Assignment?.Tags ?? new List<string>();
                            if (!filter.Tags.Any(t => taskTags.Contains(t)))
                                continue;
                        }
                    }

                    tasks.Add(task);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[TaskManager] Failed to load task from {assignmentPath}: {e.Message}");
                }
            }

            // Sort by priority (desc), then created date (asc)
            var sorted = tasks
                .OrderByDescending(t => t.Assignment?.Priority ?? 0)
                .ThenBy(t => t.CreatedAt)
                .ToList();

            if (filter?.Limit > 0)
                return sorted.Take(filter.Limit.Value).ToList();

            return sorted;
        }

        public AgentTask GetNextTask(string agentType)
        {
            var filter = new TaskFilter
            {
                Status = "pending",
                AgentType = agentType,
                Limit = 1
            };

            var tasks = ListTasksAsync(filter).GetAwaiter().GetResult();
            return tasks.FirstOrDefault();
        }

        public Convoy CreateConvoy(string name, string description, List<string> taskIds)
        {
            var convoyId = $"convoy-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

            var convoy = new Convoy
            {
                ConvoyId = convoyId,
                Name = name,
                Description = description,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                Status = "pending",
                TaskIds = taskIds ?? new List<string>(),
                Progress = CalculateConvoyProgress(taskIds)
            };

            // Update tasks with convoy ID
            foreach (var taskId in convoy.TaskIds)
            {
                var task = GetTask(taskId);
                if (task?.Assignment != null)
                {
                    task.Assignment.ConvoyId = convoyId;
                    SaveTask(task);
                }
            }

            SaveConvoy(convoy);
            Debug.Log($"[TaskManager] Created convoy {convoyId}: {name}");
            return convoy;
        }

        public Convoy GetConvoy(string convoyId)
        {
            var path = Path.Combine(_convoysRoot, $"{convoyId}.json");
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            var convoy = JsonConvert.DeserializeObject<Convoy>(json, _jsonSettings);

            // Update progress
            convoy.Progress = CalculateConvoyProgress(convoy.TaskIds);
            return convoy;
        }

        public async Task<List<Convoy>> ListConvoysAsync()
        {
            var convoys = new List<Convoy>();

            if (!Directory.Exists(_convoysRoot))
                return convoys;

            var files = Directory.GetFiles(_convoysRoot, "*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var convoy = JsonConvert.DeserializeObject<Convoy>(json, _jsonSettings);
                    convoy.Progress = CalculateConvoyProgress(convoy.TaskIds);
                    convoys.Add(convoy);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[TaskManager] Failed to load convoy from {file}: {e.Message}");
                }
            }

            return convoys.OrderByDescending(c => c.CreatedAt).ToList();
        }

        private ConvoyProgress CalculateConvoyProgress(List<string> taskIds)
        {
            var progress = new ConvoyProgress { TotalTasks = taskIds?.Count ?? 0 };

            if (taskIds == null || taskIds.Count == 0)
                return progress;

            foreach (var taskId in taskIds)
            {
                var task = GetTask(taskId);
                if (task == null) continue;

                switch (task.Status)
                {
                    case "pending": progress.PendingTasks++; break;
                    case "in_progress": progress.InProgressTasks++; break;
                    case "completed": progress.CompletedTasks++; break;
                    case "failed": progress.FailedTasks++; break;
                }
            }

            return progress;
        }

        private string GetTaskPath(string taskId)
        {
            return Path.Combine(_tasksRoot, taskId, "assignment.json");
        }

        private void SaveTask(AgentTask task)
        {
            var taskDir = Path.Combine(_tasksRoot, task.TaskId);
            Directory.CreateDirectory(taskDir);

            var path = Path.Combine(taskDir, "assignment.json");
            var json = JsonConvert.SerializeObject(task, _jsonSettings);
            WriteFileAtomic(path, json);

            _taskCache.AddOrUpdate(task.TaskId, task, (_, __) => task);
        }

        /// <summary>
        /// Validate that a state transition is allowed.
        /// </summary>
        private bool ValidateStateTransition(string currentState, string newState)
        {
            if (string.IsNullOrEmpty(currentState))
                return true; // Allow any transition from null state

            if (!_validTransitions.TryGetValue(currentState, out var allowedStates))
                return false;

            return allowedStates.Contains(newState);
        }

        /// <summary>
        /// Write file atomically using temp file + rename pattern.
        /// Prevents corruption if process is interrupted during write.
        /// </summary>
        private void WriteFileAtomic(string path, string content)
        {
            lock (_fileLock)
            {
                var tempPath = path + ".tmp";
                try
                {
                    // Write to temp file first
                    File.WriteAllText(tempPath, content);

                    // Atomic rename (overwrite if exists)
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    File.Move(tempPath, path);
                }
                catch
                {
                    // Clean up temp file on failure
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                    throw;
                }
            }
        }

        private void SaveConvoy(Convoy convoy)
        {
            var path = Path.Combine(_convoysRoot, $"{convoy.ConvoyId}.json");
            var json = JsonConvert.SerializeObject(convoy, _jsonSettings);
            WriteFileAtomic(path, json);
        }
    }
}
