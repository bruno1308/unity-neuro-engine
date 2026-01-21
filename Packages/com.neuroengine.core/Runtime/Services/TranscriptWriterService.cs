using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NeuroEngine.Core;
using Newtonsoft.Json;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Writes agent transcripts to the hooks/tasks/{taskId}/ directory.
    /// Transcripts are auto-saved after each turn to survive crashes.
    /// </summary>
    public class TranscriptWriterService : ITranscriptWriter
    {
        private readonly string _tasksRoot;
        private readonly ConcurrentDictionary<string, Transcript> _activeTranscripts = new ConcurrentDictionary<string, Transcript>();
        private readonly ConcurrentDictionary<string, Stopwatch> _turnTimers = new ConcurrentDictionary<string, Stopwatch>();
        private readonly object _fileLock = new object();

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        public TranscriptWriterService(IEnvConfig config)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var hooksPath = config.HooksPath;

            if (hooksPath.StartsWith("./"))
                hooksPath = hooksPath.Substring(2);

            _tasksRoot = Path.Combine(projectRoot, hooksPath, "tasks");
            Directory.CreateDirectory(_tasksRoot);
        }

        /// <summary>
        /// Parameterless constructor for editor/standalone use.
        /// </summary>
        public TranscriptWriterService()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            _tasksRoot = Path.Combine(projectRoot, "hooks", "tasks");
            Directory.CreateDirectory(_tasksRoot);
        }

        public Transcript StartTranscript(string taskId, string agentName)
        {
            var transcript = new Transcript
            {
                TaskId = taskId,
                Agent = agentName,
                StartedAt = DateTime.UtcNow.ToString("o"),
                Status = "in_progress",
                Turns = new List<TranscriptTurn>()
            };

            _activeTranscripts.TryAdd(taskId, transcript);

            // Create task directory
            var taskDir = GetTaskDirectory(taskId);
            Directory.CreateDirectory(taskDir);
            Directory.CreateDirectory(Path.Combine(taskDir, "artifacts"));

            // Save initial transcript
            SaveTranscriptSync(taskId);

            Debug.Log($"[TranscriptWriter] Started transcript for task {taskId} (agent: {agentName})");
            return transcript;
        }

        public void AddReasoning(string taskId, string content)
        {
            if (!_activeTranscripts.TryGetValue(taskId, out var transcript))
            {
                Debug.LogWarning($"[TranscriptWriter] No active transcript for task {taskId}");
                return;
            }

            var turn = new TranscriptTurn
            {
                Turn = transcript.Turns.Count + 1,
                Type = "reasoning",
                Timestamp = DateTime.UtcNow.ToString("o"),
                Content = content
            };

            transcript.Turns.Add(turn);
            SaveTranscriptSync(taskId);
        }

        public void AddToolCall(string taskId, string toolName, object parameters, object result)
        {
            if (!_activeTranscripts.TryGetValue(taskId, out var transcript))
            {
                Debug.LogWarning($"[TranscriptWriter] No active transcript for task {taskId}");
                return;
            }

            // Calculate duration if timer was running
            long? durationMs = null;
            if (_turnTimers.TryRemove(taskId, out var timer))
            {
                timer.Stop();
                durationMs = timer.ElapsedMilliseconds;
            }

            var turn = new TranscriptTurn
            {
                Turn = transcript.Turns.Count + 1,
                Type = "tool_call",
                Timestamp = DateTime.UtcNow.ToString("o"),
                Tool = toolName,
                Params = parameters,
                Result = result,
                DurationMs = durationMs
            };

            transcript.Turns.Add(turn);
            SaveTranscriptSync(taskId);
        }

        public void AddObservation(string taskId, string source, object content)
        {
            if (!_activeTranscripts.TryGetValue(taskId, out var transcript))
            {
                Debug.LogWarning($"[TranscriptWriter] No active transcript for task {taskId}");
                return;
            }

            var turn = new TranscriptTurn
            {
                Turn = transcript.Turns.Count + 1,
                Type = "observation",
                Timestamp = DateTime.UtcNow.ToString("o"),
                Source = source,
                Content = content?.ToString()
            };

            transcript.Turns.Add(turn);
            SaveTranscriptSync(taskId);
        }

        public void AddError(string taskId, string message, string stackTrace = null)
        {
            if (!_activeTranscripts.TryGetValue(taskId, out var transcript))
            {
                Debug.LogWarning($"[TranscriptWriter] No active transcript for task {taskId}");
                return;
            }

            var turn = new TranscriptTurn
            {
                Turn = transcript.Turns.Count + 1,
                Type = "error",
                Timestamp = DateTime.UtcNow.ToString("o"),
                Content = stackTrace != null ? $"{message}\n{stackTrace}" : message
            };

            transcript.Turns.Add(turn);
            SaveTranscriptSync(taskId);
        }

        public void CompleteTranscript(string taskId, TranscriptOutcome outcome)
        {
            if (!_activeTranscripts.TryGetValue(taskId, out var transcript))
            {
                Debug.LogWarning($"[TranscriptWriter] No active transcript for task {taskId}");
                return;
            }

            transcript.Status = "success";
            transcript.CompletedAt = DateTime.UtcNow.ToString("o");
            transcript.Outcome = outcome;

            SaveTranscriptSync(taskId);
            _activeTranscripts.TryRemove(taskId, out _);

            Debug.Log($"[TranscriptWriter] Completed transcript for task {taskId}");
        }

        public void FailTranscript(string taskId, string errorMessage)
        {
            if (!_activeTranscripts.TryGetValue(taskId, out var transcript))
            {
                Debug.LogWarning($"[TranscriptWriter] No active transcript for task {taskId}");
                return;
            }

            transcript.Status = "failed";
            transcript.CompletedAt = DateTime.UtcNow.ToString("o");
            transcript.ErrorMessage = errorMessage;

            SaveTranscriptSync(taskId);
            _activeTranscripts.TryRemove(taskId, out _);

            Debug.Log($"[TranscriptWriter] Failed transcript for task {taskId}: {errorMessage}");
        }

        public Transcript GetTranscript(string taskId)
        {
            // Check active transcripts first
            if (_activeTranscripts.TryGetValue(taskId, out var transcript))
            {
                return transcript;
            }

            // Try to load from disk
            var path = GetTranscriptPath(taskId);
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<Transcript>(json, _jsonSettings);
            }

            return null;
        }

        public async Task FlushAsync(string taskId)
        {
            if (_activeTranscripts.TryGetValue(taskId, out var transcript))
            {
                var path = GetTranscriptPath(taskId);
                var json = JsonConvert.SerializeObject(transcript, _jsonSettings);
                await WriteFileAtomicAsync(path, json);
            }
        }

        /// <summary>
        /// Write file atomically using temp file + rename pattern (async version).
        /// </summary>
        private async Task WriteFileAtomicAsync(string path, string content)
        {
            var tempPath = path + ".tmp";
            try
            {
                // Write to temp file first
                await File.WriteAllTextAsync(tempPath, content);

                // Atomic rename (overwrite if exists)
                lock (_fileLock)
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    File.Move(tempPath, path);
                }
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

        public async Task<List<TranscriptSummary>> ListTranscriptsAsync(string status = null, string agent = null)
        {
            var summaries = new List<TranscriptSummary>();

            if (!Directory.Exists(_tasksRoot))
                return summaries;

            var taskDirs = Directory.GetDirectories(_tasksRoot);

            foreach (var taskDir in taskDirs)
            {
                var transcriptPath = Path.Combine(taskDir, "transcript.json");
                if (!File.Exists(transcriptPath))
                    continue;

                try
                {
                    var json = await File.ReadAllTextAsync(transcriptPath);
                    var transcript = JsonConvert.DeserializeObject<Transcript>(json, _jsonSettings);

                    // Apply filters
                    if (!string.IsNullOrEmpty(status) && transcript.Status != status)
                        continue;
                    if (!string.IsNullOrEmpty(agent) && transcript.Agent != agent)
                        continue;

                    summaries.Add(new TranscriptSummary
                    {
                        TaskId = transcript.TaskId,
                        Agent = transcript.Agent,
                        Status = transcript.Status,
                        StartedAt = transcript.StartedAt,
                        CompletedAt = transcript.CompletedAt,
                        TurnCount = transcript.Turns?.Count ?? 0,
                        OutcomeSummary = transcript.Outcome?.Summary ?? transcript.ErrorMessage
                    });
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[TranscriptWriter] Failed to load transcript from {transcriptPath}: {e.Message}");
                }
            }

            // Sort by start time descending
            return summaries.OrderByDescending(s => s.StartedAt).ToList();
        }

        /// <summary>
        /// Start timing a turn (call before tool execution).
        /// </summary>
        public void StartTurnTimer(string taskId)
        {
            var timer = new Stopwatch();
            timer.Start();
            _turnTimers.AddOrUpdate(taskId, timer, (_, __) => timer);
        }

        private string GetTaskDirectory(string taskId)
        {
            return Path.Combine(_tasksRoot, taskId);
        }

        private string GetTranscriptPath(string taskId)
        {
            return Path.Combine(GetTaskDirectory(taskId), "transcript.json");
        }

        private void SaveTranscriptSync(string taskId)
        {
            if (!_activeTranscripts.TryGetValue(taskId, out var transcript))
                return;

            try
            {
                var path = GetTranscriptPath(taskId);
                var json = JsonConvert.SerializeObject(transcript, _jsonSettings);
                WriteFileAtomic(path, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TranscriptWriter] Failed to save transcript for {taskId}: {e.Message}");
            }
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
    }
}
