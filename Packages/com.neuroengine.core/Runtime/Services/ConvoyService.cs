using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Layer 6: Convoy Service
    ///
    /// Specialized service for managing convoys - groups of related tasks that
    /// should be executed together with dependency tracking.
    ///
    /// Convoys enable:
    /// - Grouping tasks for coordinated delivery
    /// - Tracking convoy dependencies (convoy B waits for convoy A)
    /// - Completion criteria tracking
    /// - Progress aggregation across tasks
    ///
    /// All state is persisted to hooks/convoys/{convoyId}.json
    /// </summary>
    public class ConvoyService
    {
        private readonly string _convoysRoot;
        private readonly string _tasksRoot;
        private readonly IOrchestration _orchestration;
        private readonly ConcurrentDictionary<string, ConvoyInfo> _cache = new ConcurrentDictionary<string, ConvoyInfo>();
        private readonly ConcurrentDictionary<string, object> _locks = new ConcurrentDictionary<string, object>();
        private int _counter;
        private readonly object _counterLock = new object();
        private readonly object _fileLock = new object();

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new List<JsonConverter> { new StringEnumConverter() }
        };

        /// <summary>
        /// Constructor with orchestration service dependency.
        /// </summary>
        public ConvoyService(IOrchestration orchestration, IEnvConfig config)
        {
            _orchestration = orchestration;

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var hooksPath = config.HooksPath;

            if (hooksPath.StartsWith("./"))
                hooksPath = hooksPath.Substring(2);

            _convoysRoot = Path.Combine(projectRoot, hooksPath, "convoys");
            _tasksRoot = Path.Combine(projectRoot, hooksPath, "tasks");

            EnsureDirectories();
            InitializeCounter();
        }

        /// <summary>
        /// Constructor with orchestration service for editor/standalone use.
        /// </summary>
        public ConvoyService(IOrchestration orchestration)
        {
            _orchestration = orchestration;

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            _convoysRoot = Path.Combine(projectRoot, "hooks", "convoys");
            _tasksRoot = Path.Combine(projectRoot, "hooks", "tasks");

            EnsureDirectories();
            InitializeCounter();
        }

        /// <summary>
        /// Parameterless constructor for standalone use (creates internal orchestration).
        /// </summary>
        public ConvoyService()
        {
            _orchestration = new OrchestrationService();

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            _convoysRoot = Path.Combine(projectRoot, "hooks", "convoys");
            _tasksRoot = Path.Combine(projectRoot, "hooks", "tasks");

            EnsureDirectories();
            InitializeCounter();
        }

        private void EnsureDirectories()
        {
            Directory.CreateDirectory(_convoysRoot);
            Directory.CreateDirectory(_tasksRoot);
        }

        private void InitializeCounter()
        {
            if (Directory.Exists(_convoysRoot))
            {
                var existingFiles = Directory.GetFiles(_convoysRoot, "convoy-*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => n.StartsWith("convoy-"))
                    .Select(n =>
                    {
                        var numPart = n.Substring(7);
                        return int.TryParse(numPart, out var num) ? num : 0;
                    })
                    .DefaultIfEmpty(0)
                    .Max();

                _counter = existingFiles;
            }
        }

        #region Convoy Operations

        /// <summary>
        /// Create a new convoy from a configuration.
        /// </summary>
        public ConvoyInfo Create(ConvoyConfig config)
        {
            // Delegate to orchestration service which handles full convoy creation
            return _orchestration.CreateConvoy(config);
        }

        /// <summary>
        /// Create a convoy and automatically create tasks from specifications.
        /// </summary>
        public ConvoyInfo CreateWithTasks(ConvoyConfig config, List<TaskConfig> taskConfigs)
        {
            if (string.IsNullOrEmpty(config.Name))
                throw new ArgumentException("Convoy name is required", nameof(config));

            int newCounter;
            lock (_counterLock)
            {
                newCounter = ++_counter;
            }

            var convoyId = $"convoy-{newCounter:D3}";
            var now = DateTime.UtcNow.ToString("o");

            // Create tasks first
            var taskIds = new List<string>();
            foreach (var taskConfig in taskConfigs)
            {
                taskConfig.ConvoyId = convoyId;
                taskConfig.Iteration = config.Iteration;
                var task = _orchestration.CreateTask(taskConfig);
                taskIds.Add(task.Id);
            }

            // Create convoy with the task IDs
            config.TaskIds = taskIds;
            return _orchestration.CreateConvoy(config);
        }

        /// <summary>
        /// Get convoy status with full progress calculation.
        /// </summary>
        public ConvoyInfo GetStatus(string convoyId)
        {
            return _orchestration.GetConvoyStatus(convoyId);
        }

        /// <summary>
        /// Add an existing task to a convoy.
        /// </summary>
        public void AddTask(string convoyId, string taskId)
        {
            var convoy = GetStatus(convoyId);
            if (convoy == null)
                throw new ArgumentException($"Convoy '{convoyId}' not found");

            var task = _orchestration.GetTaskStatus(taskId);
            if (task == null)
                throw new ArgumentException($"Task '{taskId}' not found");

            lock (GetLock(convoyId))
            {
                convoy = LoadConvoy(convoyId);
                if (!convoy.TaskIds.Contains(taskId))
                {
                    convoy.TaskIds.Add(taskId);
                    convoy.Progress = CalculateProgress(convoy);
                    SaveConvoy(convoy);
                }
            }

            Debug.Log($"[ConvoyService] Added task {taskId} to convoy {convoyId}");
        }

        /// <summary>
        /// Remove a task from a convoy.
        /// </summary>
        public void RemoveTask(string convoyId, string taskId)
        {
            var convoy = GetStatus(convoyId);
            if (convoy == null)
                throw new ArgumentException($"Convoy '{convoyId}' not found");

            lock (GetLock(convoyId))
            {
                convoy = LoadConvoy(convoyId);
                if (convoy.TaskIds.Remove(taskId))
                {
                    convoy.Progress = CalculateProgress(convoy);
                    SaveConvoy(convoy);
                }
            }

            Debug.Log($"[ConvoyService] Removed task {taskId} from convoy {convoyId}");
        }

        /// <summary>
        /// Mark a convoy as completed.
        /// Validates that all tasks are complete before allowing completion.
        /// </summary>
        public void Complete(string convoyId)
        {
            _orchestration.CompleteConvoy(convoyId);
        }

        /// <summary>
        /// Mark a convoy as failed with a reason.
        /// </summary>
        public void Fail(string convoyId, string reason)
        {
            _orchestration.FailConvoy(convoyId, reason);
        }

        /// <summary>
        /// List all convoys with optional filtering.
        /// </summary>
        public async Task<List<ConvoyInfo>> ListAsync(ConvoyStatusFilter filter = null)
        {
            return await _orchestration.GetConvoysAsync(filter);
        }

        /// <summary>
        /// Check if a convoy's dependencies are satisfied.
        /// </summary>
        public bool AreDependenciesSatisfied(string convoyId)
        {
            var convoy = GetStatus(convoyId);
            if (convoy == null)
                return false;

            if (convoy.Dependencies == null || convoy.Dependencies.Count == 0)
                return true;

            return convoy.Dependencies.All(depId =>
            {
                var dep = GetStatus(depId);
                return dep?.Status == ConvoyStatus.Completed;
            });
        }

        /// <summary>
        /// Get the next convoy that is ready to start (pending with satisfied dependencies).
        /// </summary>
        public async Task<ConvoyInfo> GetNextReadyConvoyAsync(string iteration = null)
        {
            var filter = new ConvoyStatusFilter
            {
                Status = ConvoyStatus.Pending,
                Iteration = iteration,
                IncludeCompleted = false
            };

            var convoys = await ListAsync(filter);

            foreach (var convoy in convoys.OrderByDescending(c => c.Priority))
            {
                if (AreDependenciesSatisfied(convoy.Id))
                {
                    return convoy;
                }
            }

            // Also check blocked convoys that might be unblocked now
            filter.Status = ConvoyStatus.Blocked;
            var blockedConvoys = await ListAsync(filter);

            foreach (var convoy in blockedConvoys.OrderByDescending(c => c.Priority))
            {
                if (AreDependenciesSatisfied(convoy.Id))
                {
                    // Unblock and return
                    lock (GetLock(convoy.Id))
                    {
                        var c = LoadConvoy(convoy.Id);
                        c.Status = ConvoyStatus.Pending;
                        SaveConvoy(c);
                    }
                    return GetStatus(convoy.Id);
                }
            }

            return null;
        }

        /// <summary>
        /// Get tasks in a convoy grouped by status.
        /// </summary>
        public async Task<ConvoyTasksSummary> GetTasksSummaryAsync(string convoyId)
        {
            var convoy = GetStatus(convoyId);
            if (convoy == null)
                throw new ArgumentException($"Convoy '{convoyId}' not found");

            var tasks = new List<TaskInfo>();
            foreach (var taskId in convoy.TaskIds)
            {
                var task = _orchestration.GetTaskStatus(taskId);
                if (task != null)
                    tasks.Add(task);
            }

            return new ConvoyTasksSummary
            {
                ConvoyId = convoyId,
                ConvoyName = convoy.Name,
                Pending = tasks.Where(t => t.Status == TaskStatus.Pending).ToList(),
                Assigned = tasks.Where(t => t.Status == TaskStatus.Assigned).ToList(),
                InProgress = tasks.Where(t => t.Status == TaskStatus.InProgress).ToList(),
                Completed = tasks.Where(t => t.Status == TaskStatus.Completed).ToList(),
                Failed = tasks.Where(t => t.Status == TaskStatus.Failed).ToList(),
                Blocked = tasks.Where(t => t.Status == TaskStatus.Blocked).ToList()
            };
        }

        /// <summary>
        /// Check completion criteria and automatically complete convoy if all criteria are met.
        /// </summary>
        public bool TryAutoComplete(string convoyId)
        {
            var convoy = GetStatus(convoyId);
            if (convoy == null)
                return false;

            if (convoy.Status != ConvoyStatus.InProgress)
                return false;

            // Check if all tasks are complete
            if (!convoy.Progress.AllTasksComplete)
                return false;

            // Mark as complete
            _orchestration.CompleteConvoy(convoyId);
            return true;
        }

        /// <summary>
        /// Add a dependency to a convoy.
        /// </summary>
        public void AddDependency(string convoyId, string dependsOnConvoyId)
        {
            var convoy = GetStatus(convoyId);
            if (convoy == null)
                throw new ArgumentException($"Convoy '{convoyId}' not found");

            var dependency = GetStatus(dependsOnConvoyId);
            if (dependency == null)
                throw new ArgumentException($"Dependency convoy '{dependsOnConvoyId}' not found");

            lock (GetLock(convoyId))
            {
                convoy = LoadConvoy(convoyId);
                if (!convoy.Dependencies.Contains(dependsOnConvoyId))
                {
                    convoy.Dependencies.Add(dependsOnConvoyId);

                    // Check if now blocked
                    if (dependency.Status != ConvoyStatus.Completed &&
                        convoy.Status == ConvoyStatus.Pending)
                    {
                        convoy.Status = ConvoyStatus.Blocked;
                    }

                    SaveConvoy(convoy);
                }
            }

            Debug.Log($"[ConvoyService] Convoy {convoyId} now depends on {dependsOnConvoyId}");
        }

        /// <summary>
        /// Remove a dependency from a convoy.
        /// </summary>
        public void RemoveDependency(string convoyId, string dependsOnConvoyId)
        {
            var convoy = GetStatus(convoyId);
            if (convoy == null)
                throw new ArgumentException($"Convoy '{convoyId}' not found");

            lock (GetLock(convoyId))
            {
                convoy = LoadConvoy(convoyId);
                if (convoy.Dependencies.Remove(dependsOnConvoyId))
                {
                    // Check if can be unblocked
                    if (convoy.Status == ConvoyStatus.Blocked && AreDependenciesSatisfied(convoyId))
                    {
                        convoy.Status = ConvoyStatus.Pending;
                    }

                    SaveConvoy(convoy);
                }
            }

            Debug.Log($"[ConvoyService] Removed dependency {dependsOnConvoyId} from convoy {convoyId}");
        }

        #endregion

        #region Private Helpers

        private ConvoyProgress CalculateProgress(ConvoyInfo convoy)
        {
            var progress = new ConvoyProgress { TotalTasks = convoy.TaskIds?.Count ?? 0 };

            if (convoy.TaskIds == null || convoy.TaskIds.Count == 0)
                return progress;

            foreach (var taskId in convoy.TaskIds)
            {
                var task = _orchestration.GetTaskStatus(taskId);
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
                _cache.AddOrUpdate(convoyId, convoy, (_, __) => convoy);
                return convoy;
            }
        }

        private void SaveConvoy(ConvoyInfo convoy)
        {
            var path = Path.Combine(_convoysRoot, $"{convoy.Id}.json");
            var json = JsonConvert.SerializeObject(convoy, _jsonSettings);
            WriteFileAtomic(path, json);
            _cache.AddOrUpdate(convoy.Id, convoy, (_, __) => convoy);
        }

        private object GetLock(string convoyId)
        {
            return _locks.GetOrAdd(convoyId, _ => new object());
        }

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

    /// <summary>
    /// Summary of tasks in a convoy grouped by status.
    /// </summary>
    public class ConvoyTasksSummary
    {
        public string ConvoyId { get; set; }
        public string ConvoyName { get; set; }
        public List<TaskInfo> Pending { get; set; } = new List<TaskInfo>();
        public List<TaskInfo> Assigned { get; set; } = new List<TaskInfo>();
        public List<TaskInfo> InProgress { get; set; } = new List<TaskInfo>();
        public List<TaskInfo> Completed { get; set; } = new List<TaskInfo>();
        public List<TaskInfo> Failed { get; set; } = new List<TaskInfo>();
        public List<TaskInfo> Blocked { get; set; } = new List<TaskInfo>();

        public int TotalTasks => Pending.Count + Assigned.Count + InProgress.Count +
                                 Completed.Count + Failed.Count + Blocked.Count;

        public int ActiveTasks => InProgress.Count + Assigned.Count;
    }
}
