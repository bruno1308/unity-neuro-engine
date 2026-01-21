using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NeuroEngine.Core;
using Newtonsoft.Json;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Layer 6 Safety Control Service.
    /// Enforces safety limits to prevent runaway costs, infinite loops,
    /// and ensures human oversight for critical decisions.
    ///
    /// Safety limits (from mayor.md):
    /// - Max iterations per task: 50
    /// - Max API cost per hour: $10
    /// - Max parallel agents: 5
    ///
    /// State is persisted to:
    /// - hooks/orchestration/budget.json (budget tracking)
    /// - hooks/reviews/pending-approval.json (approval requests)
    /// - hooks/orchestration/safety-state.json (iteration counts, agent registry)
    /// </summary>
    public class SafetyControlService : ISafetyControl
    {
        // Safety limits from mayor.md
        public const int MaxIterationsPerTask = 50;
        public const decimal MaxApiCostPerHour = 10.00m;
        public const int MaxParallelAgents = 5;

        // Approval request expiry time (24 hours)
        private static readonly TimeSpan ApprovalExpiryTime = TimeSpan.FromHours(24);

        // Paths
        private readonly string _hooksRoot;
        private string _budgetPath;
        private string _approvalsPath;
        private string _safetyStatePath;
        private string _rollbackLogPath;

        // In-memory state (backed by files)
        private readonly ConcurrentDictionary<string, IterationInfo> _iterationCounts = new();
        private readonly ConcurrentDictionary<string, ActiveAgent> _activeAgents = new();
        private readonly ConcurrentDictionary<string, ApprovalRequest> _approvalRequests = new();
        private BudgetInfo _budgetInfo;

        // Thread safety
        private readonly object _fileLock = new object();
        private readonly object _budgetLock = new object();

        // JSON settings
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatString = "o"
        };

        /// <summary>
        /// Create with IEnvConfig for dependency injection.
        /// </summary>
        public SafetyControlService(IEnvConfig config)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var hooksPath = config.HooksPath;

            if (hooksPath.StartsWith("./"))
                hooksPath = hooksPath.Substring(2);

            _hooksRoot = Path.Combine(projectRoot, hooksPath);
            InitializePaths();
        }

        /// <summary>
        /// Parameterless constructor for editor/standalone use.
        /// </summary>
        public SafetyControlService()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            _hooksRoot = Path.Combine(projectRoot, "hooks");
            InitializePaths();
        }

        private void InitializePaths()
        {
            var orchestrationDir = Path.Combine(_hooksRoot, "orchestration");
            var reviewsDir = Path.Combine(_hooksRoot, "reviews");

            Directory.CreateDirectory(orchestrationDir);
            Directory.CreateDirectory(reviewsDir);

            _budgetPath = Path.Combine(orchestrationDir, "budget.json");
            _safetyStatePath = Path.Combine(orchestrationDir, "safety-state.json");
            _approvalsPath = Path.Combine(reviewsDir, "pending-approval.json");
            _rollbackLogPath = Path.Combine(orchestrationDir, "rollback-log.json");

            LoadState();
        }

        #region Iteration Control

        public bool CheckIterationLimit(string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
                return true;

            var info = GetOrCreateIterationInfo(taskId);
            return !info.LimitReached;
        }

        public void IncrementIteration(string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
                return;

            var info = GetOrCreateIterationInfo(taskId);
            info.CurrentIteration++;
            info.LastIteration = DateTime.UtcNow;

            _iterationCounts[taskId] = info;
            SaveState();

            Debug.Log($"[SafetyControl] Task {taskId}: iteration {info.CurrentIteration}/{info.MaxIterations}");

            if (info.LimitReached)
            {
                Debug.LogWarning($"[SafetyControl] Task {taskId} has reached iteration limit ({MaxIterationsPerTask})");
            }
        }

        public IterationInfo GetIterationInfo(string taskId)
        {
            return GetOrCreateIterationInfo(taskId);
        }

        public void ResetIterations(string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
                return;

            if (_iterationCounts.TryGetValue(taskId, out var info))
            {
                info.CurrentIteration = 0;
                info.FirstIteration = DateTime.UtcNow;
                info.LastIteration = DateTime.UtcNow;
                SaveState();

                Debug.Log($"[SafetyControl] Reset iterations for task {taskId}");
            }
        }

        private IterationInfo GetOrCreateIterationInfo(string taskId)
        {
            return _iterationCounts.GetOrAdd(taskId, _ => new IterationInfo
            {
                TaskId = taskId,
                CurrentIteration = 0,
                MaxIterations = MaxIterationsPerTask,
                FirstIteration = DateTime.UtcNow,
                LastIteration = DateTime.UtcNow
            });
        }

        #endregion

        #region Budget Control

        public bool CheckBudget(decimal costEstimate)
        {
            lock (_budgetLock)
            {
                EnsureBudgetWindow();

                if (_budgetInfo.IsPaused)
                    return false;

                return (_budgetInfo.SpentThisHour + costEstimate) <= _budgetInfo.HourlyLimit;
            }
        }

        public void RecordCost(decimal amount, string description)
        {
            lock (_budgetLock)
            {
                EnsureBudgetWindow();

                var entry = new CostEntry
                {
                    Amount = amount,
                    Description = description,
                    Timestamp = DateTime.UtcNow
                };

                _budgetInfo.SpentThisHour += amount;
                _budgetInfo.TotalSpent += amount;
                _budgetInfo.RecentCosts.Add(entry);

                // Trim old entries (keep last 24 hours)
                var cutoff = DateTime.UtcNow.AddHours(-24);
                _budgetInfo.RecentCosts = _budgetInfo.RecentCosts
                    .Where(c => c.Timestamp > cutoff)
                    .ToList();

                SaveBudget();

                Debug.Log($"[SafetyControl] Recorded cost: ${amount:F4} - {description}. " +
                          $"Spent this hour: ${_budgetInfo.SpentThisHour:F4}/${_budgetInfo.HourlyLimit:F2}");

                // Check if we've exceeded the limit
                if (_budgetInfo.SpentThisHour >= _budgetInfo.HourlyLimit)
                {
                    _budgetInfo.IsPaused = true;
                    _budgetInfo.PauseReason = $"Hourly budget limit (${MaxApiCostPerHour}) reached at {DateTime.UtcNow:o}";
                    SaveBudget();

                    Debug.LogWarning($"[SafetyControl] Budget limit reached! " +
                                     $"Spent ${_budgetInfo.SpentThisHour:F4} of ${_budgetInfo.HourlyLimit:F2} limit. " +
                                     "Operations paused until next hour window.");
                }
            }
        }

        public BudgetInfo GetBudgetStatus()
        {
            lock (_budgetLock)
            {
                EnsureBudgetWindow();
                return _budgetInfo;
            }
        }

        private void EnsureBudgetWindow()
        {
            if (_budgetInfo == null)
            {
                _budgetInfo = new BudgetInfo
                {
                    HourlyLimit = MaxApiCostPerHour,
                    SpentThisHour = 0,
                    HourWindowStart = DateTime.UtcNow,
                    TotalSpent = 0,
                    RecentCosts = new List<CostEntry>()
                };
            }

            // Check if we're in a new hour window
            if (DateTime.UtcNow >= _budgetInfo.HourWindowEnd)
            {
                // Start new hour window
                _budgetInfo.HourWindowStart = DateTime.UtcNow;
                _budgetInfo.SpentThisHour = 0;

                // Unpause if we were paused due to budget
                if (_budgetInfo.IsPaused && _budgetInfo.PauseReason?.Contains("budget") == true)
                {
                    _budgetInfo.IsPaused = false;
                    _budgetInfo.PauseReason = null;
                    Debug.Log("[SafetyControl] New hour window started. Budget reset.");
                }

                SaveBudget();
            }
        }

        #endregion

        #region Parallel Agent Control

        public bool CheckParallelAgents()
        {
            return _activeAgents.Count < MaxParallelAgents;
        }

        public void RegisterAgent(string agentId, string agentType)
        {
            if (string.IsNullOrEmpty(agentId))
                return;

            var agent = new ActiveAgent
            {
                AgentId = agentId,
                AgentType = agentType,
                StartedAt = DateTime.UtcNow
            };

            _activeAgents[agentId] = agent;
            SaveState();

            Debug.Log($"[SafetyControl] Registered agent: {agentId} ({agentType}). " +
                      $"Active agents: {_activeAgents.Count}/{MaxParallelAgents}");

            if (_activeAgents.Count >= MaxParallelAgents)
            {
                Debug.LogWarning($"[SafetyControl] Parallel agent limit reached ({MaxParallelAgents}). " +
                                 "New agents will be queued.");
            }
        }

        public void UnregisterAgent(string agentId)
        {
            if (string.IsNullOrEmpty(agentId))
                return;

            if (_activeAgents.TryRemove(agentId, out var agent))
            {
                var duration = DateTime.UtcNow - agent.StartedAt;
                Debug.Log($"[SafetyControl] Unregistered agent: {agentId}. " +
                          $"Duration: {duration.TotalSeconds:F1}s. " +
                          $"Active agents: {_activeAgents.Count}/{MaxParallelAgents}");
                SaveState();
            }
        }

        public int GetActiveAgentCount()
        {
            return _activeAgents.Count;
        }

        #endregion

        #region Rollback Control

        public RollbackResult TriggerRollback(string reason)
        {
            var result = new RollbackResult
            {
                Reason = reason,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);

                // Get current commit
                var currentCommit = RunGitCommand(projectRoot, "rev-parse HEAD");
                result.RolledBackFromCommit = currentCommit?.Trim();

                // Get the parent commit
                var parentCommit = RunGitCommand(projectRoot, "rev-parse HEAD~1");
                result.RolledBackToCommit = parentCommit?.Trim();

                // Get affected files
                var diffOutput = RunGitCommand(projectRoot, "diff --name-only HEAD~1 HEAD");
                if (!string.IsNullOrEmpty(diffOutput))
                {
                    result.AffectedFiles = diffOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                // Perform the rollback
                var resetOutput = RunGitCommand(projectRoot, "reset --hard HEAD~1");

                result.Success = true;
                result.CommitsRolledBack = 1;

                // Log the rollback
                LogRollback(result);

                Debug.Log($"[SafetyControl] Rollback successful. " +
                          $"From: {result.RolledBackFromCommit?.Substring(0, 7)} " +
                          $"To: {result.RolledBackToCommit?.Substring(0, 7)}. " +
                          $"Reason: {reason}");
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ErrorMessage = e.Message;

                Debug.LogError($"[SafetyControl] Rollback failed: {e.Message}");
            }

            return result;
        }

        private string RunGitCommand(string workingDirectory, string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    return null;

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
                {
                    throw new Exception($"Git command failed: {error}");
                }

                return output;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SafetyControl] Git command error: {e.Message}");
                throw;
            }
        }

        private void LogRollback(RollbackResult result)
        {
            lock (_fileLock)
            {
                List<RollbackResult> log = new List<RollbackResult>();

                if (File.Exists(_rollbackLogPath))
                {
                    try
                    {
                        var json = File.ReadAllText(_rollbackLogPath);
                        log = JsonConvert.DeserializeObject<List<RollbackResult>>(json, _jsonSettings) ?? new List<RollbackResult>();
                    }
                    catch { /* Start fresh if corrupt */ }
                }

                log.Add(result);

                // Keep last 100 rollbacks
                if (log.Count > 100)
                    log = log.Skip(log.Count - 100).ToList();

                var newJson = JsonConvert.SerializeObject(log, _jsonSettings);
                WriteFileAtomic(_rollbackLogPath, newJson);
            }
        }

        #endregion

        #region Approval Requests

        public ApprovalRequest RequestHumanApproval(string reason, Dictionary<string, object> context)
        {
            var request = new ApprovalRequest
            {
                RequestId = $"approval-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}",
                Reason = reason,
                Context = context ?? new Dictionary<string, object>(),
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            };

            // Determine category from reason
            if (reason.ToLowerInvariant().Contains("budget"))
                request.Category = "budget";
            else if (reason.ToLowerInvariant().Contains("iteration"))
                request.Category = "iteration";
            else if (reason.ToLowerInvariant().Contains("rollback"))
                request.Category = "rollback";

            _approvalRequests[request.RequestId] = request;
            SaveApprovals();

            Debug.Log($"[SafetyControl] Created approval request: {request.RequestId} - {reason}");

            return request;
        }

        public ApprovalStatus GetApprovalStatus(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
                return null;

            if (!_approvalRequests.TryGetValue(requestId, out var request))
            {
                return new ApprovalStatus
                {
                    RequestId = requestId,
                    Status = "not_found"
                };
            }

            // Check for expiry
            if (request.Status == "pending" && DateTime.UtcNow > request.CreatedAt.Add(ApprovalExpiryTime))
            {
                request.Status = "expired";
                request.ResolvedAt = DateTime.UtcNow;
                SaveApprovals();
            }

            return new ApprovalStatus
            {
                RequestId = request.RequestId,
                Status = request.Status,
                ReviewerNotes = request.ReviewerNotes,
                ResolvedAt = request.ResolvedAt,
                TimeRemaining = request.Status == "pending"
                    ? request.CreatedAt.Add(ApprovalExpiryTime) - DateTime.UtcNow
                    : null
            };
        }

        public List<ApprovalRequest> ListPendingApprovals()
        {
            // Check for expired requests
            var now = DateTime.UtcNow;
            foreach (var request in _approvalRequests.Values.Where(r => r.Status == "pending"))
            {
                if (now > request.CreatedAt.Add(ApprovalExpiryTime))
                {
                    request.Status = "expired";
                    request.ResolvedAt = now;
                }
            }
            SaveApprovals();

            return _approvalRequests.Values
                .Where(r => r.Status == "pending")
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.CreatedAt)
                .ToList();
        }

        public void ResolveApproval(string requestId, bool approved, string reviewerNotes = null)
        {
            if (string.IsNullOrEmpty(requestId))
                return;

            if (_approvalRequests.TryGetValue(requestId, out var request))
            {
                request.Status = approved ? "approved" : "rejected";
                request.ResolvedAt = DateTime.UtcNow;
                request.ReviewerNotes = reviewerNotes;

                SaveApprovals();

                Debug.Log($"[SafetyControl] Approval {requestId} {request.Status}: {reviewerNotes ?? "(no notes)"}");

                // Handle special cases
                if (approved)
                {
                    // If budget approval, unpause
                    if (request.Category == "budget" && _budgetInfo?.IsPaused == true)
                    {
                        _budgetInfo.IsPaused = false;
                        _budgetInfo.PauseReason = null;
                        SaveBudget();
                    }

                    // If iteration approval, reset iterations for the task
                    if (request.Category == "iteration" && request.TaskId != null)
                    {
                        ResetIterations(request.TaskId);
                    }
                }
            }
        }

        #endregion

        #region State Persistence

        private void LoadState()
        {
            LoadBudget();
            LoadSafetyState();
            LoadApprovals();
        }

        private void LoadBudget()
        {
            lock (_budgetLock)
            {
                if (File.Exists(_budgetPath))
                {
                    try
                    {
                        var json = File.ReadAllText(_budgetPath);
                        _budgetInfo = JsonConvert.DeserializeObject<BudgetInfo>(json, _jsonSettings);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[SafetyControl] Failed to load budget state: {e.Message}");
                    }
                }

                // Ensure valid budget info
                if (_budgetInfo == null)
                {
                    _budgetInfo = new BudgetInfo
                    {
                        HourlyLimit = MaxApiCostPerHour,
                        SpentThisHour = 0,
                        HourWindowStart = DateTime.UtcNow,
                        TotalSpent = 0,
                        RecentCosts = new List<CostEntry>()
                    };
                }
            }
        }

        private void SaveBudget()
        {
            lock (_fileLock)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(_budgetInfo, _jsonSettings);
                    WriteFileAtomic(_budgetPath, json);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SafetyControl] Failed to save budget: {e.Message}");
                }
            }
        }

        private void LoadSafetyState()
        {
            if (!File.Exists(_safetyStatePath))
                return;

            try
            {
                var json = File.ReadAllText(_safetyStatePath);
                var state = JsonConvert.DeserializeObject<SafetyState>(json, _jsonSettings);

                if (state?.Iterations != null)
                {
                    foreach (var kvp in state.Iterations)
                        _iterationCounts[kvp.Key] = kvp.Value;
                }

                if (state?.ActiveAgents != null)
                {
                    foreach (var kvp in state.ActiveAgents)
                        _activeAgents[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SafetyControl] Failed to load safety state: {e.Message}");
            }
        }

        private void SaveState()
        {
            lock (_fileLock)
            {
                try
                {
                    var state = new SafetyState
                    {
                        Iterations = _iterationCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        ActiveAgents = _activeAgents.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        LastUpdated = DateTime.UtcNow
                    };

                    var json = JsonConvert.SerializeObject(state, _jsonSettings);
                    WriteFileAtomic(_safetyStatePath, json);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SafetyControl] Failed to save safety state: {e.Message}");
                }
            }
        }

        private void LoadApprovals()
        {
            if (!File.Exists(_approvalsPath))
                return;

            try
            {
                var json = File.ReadAllText(_approvalsPath);

                // Try to load as a list first (new format)
                try
                {
                    var requests = JsonConvert.DeserializeObject<List<ApprovalRequest>>(json, _jsonSettings);
                    if (requests != null)
                    {
                        foreach (var request in requests)
                        {
                            if (!string.IsNullOrEmpty(request.RequestId))
                                _approvalRequests[request.RequestId] = request;
                        }
                        return;
                    }
                }
                catch { /* Try single object format */ }

                // Try single object format (old format)
                var singleRequest = JsonConvert.DeserializeObject<ApprovalRequest>(json, _jsonSettings);
                if (singleRequest != null && !string.IsNullOrEmpty(singleRequest.RequestId))
                {
                    _approvalRequests[singleRequest.RequestId] = singleRequest;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SafetyControl] Failed to load approvals: {e.Message}");
            }
        }

        private void SaveApprovals()
        {
            lock (_fileLock)
            {
                try
                {
                    var requests = _approvalRequests.Values.ToList();
                    var json = JsonConvert.SerializeObject(requests, _jsonSettings);
                    WriteFileAtomic(_approvalsPath, json);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SafetyControl] Failed to save approvals: {e.Message}");
                }
            }
        }

        private void WriteFileAtomic(string path, string content)
        {
            var tempPath = path + ".tmp";
            try
            {
                File.WriteAllText(tempPath, content);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tempPath, path);
            }
            catch
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
                throw;
            }
        }

        #endregion

        /// <summary>
        /// Internal state class for persistence.
        /// </summary>
        private class SafetyState
        {
            public Dictionary<string, IterationInfo> Iterations { get; set; }
            public Dictionary<string, ActiveAgent> ActiveAgents { get; set; }
            public DateTime LastUpdated { get; set; }
        }
    }
}
