using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for recording agent reasoning and actions.
    /// This is Layer 4 (Persistence) - transcripts survive domain reloads and crashes.
    ///
    /// Transcripts capture the full reasoning trace of an agent working on a task,
    /// enabling debugging, replay, and learning from agent behavior.
    /// </summary>
    public interface ITranscriptWriter
    {
        /// <summary>
        /// Start a new transcript for a task.
        /// </summary>
        /// <param name="taskId">Unique task identifier</param>
        /// <param name="agentName">Name of the agent (e.g., "script-polecat-1")</param>
        /// <returns>The created transcript</returns>
        Transcript StartTranscript(string taskId, string agentName);

        /// <summary>
        /// Add a reasoning turn to the transcript.
        /// </summary>
        void AddReasoning(string taskId, string content);

        /// <summary>
        /// Add a tool call turn to the transcript.
        /// </summary>
        void AddToolCall(string taskId, string toolName, object parameters, object result);

        /// <summary>
        /// Add an observation turn to the transcript.
        /// </summary>
        void AddObservation(string taskId, string source, object content);

        /// <summary>
        /// Add an error turn to the transcript.
        /// </summary>
        void AddError(string taskId, string message, string stackTrace = null);

        /// <summary>
        /// Complete the transcript with final outcome.
        /// </summary>
        void CompleteTranscript(string taskId, TranscriptOutcome outcome);

        /// <summary>
        /// Fail the transcript with error information.
        /// </summary>
        void FailTranscript(string taskId, string errorMessage);

        /// <summary>
        /// Get the current transcript for a task.
        /// </summary>
        Transcript GetTranscript(string taskId);

        /// <summary>
        /// Save transcript to disk immediately (normally auto-saved).
        /// </summary>
        Task FlushAsync(string taskId);

        /// <summary>
        /// List all transcripts (optionally filtered by status or agent).
        /// </summary>
        Task<List<TranscriptSummary>> ListTranscriptsAsync(string status = null, string agent = null);
    }

    /// <summary>
    /// A complete transcript of agent reasoning and actions.
    /// </summary>
    public class Transcript
    {
        /// <summary>Unique task identifier</summary>
        public string TaskId;

        /// <summary>Name of the agent that worked on this task</summary>
        public string Agent;

        /// <summary>ISO timestamp when work started</summary>
        public string StartedAt;

        /// <summary>ISO timestamp when work completed (or failed)</summary>
        public string CompletedAt;

        /// <summary>Current status: "in_progress", "success", "failed", "cancelled"</summary>
        public string Status;

        /// <summary>Sequential list of reasoning/action turns</summary>
        public List<TranscriptTurn> Turns = new List<TranscriptTurn>();

        /// <summary>Final outcome (null if still in progress)</summary>
        public TranscriptOutcome Outcome;

        /// <summary>Error message if failed</summary>
        public string ErrorMessage;
    }

    /// <summary>
    /// A single turn in the transcript (reasoning, tool call, or observation).
    /// </summary>
    public class TranscriptTurn
    {
        /// <summary>Sequential turn number (1-indexed)</summary>
        public int Turn;

        /// <summary>Type: "reasoning", "tool_call", "observation", "error"</summary>
        public string Type;

        /// <summary>ISO timestamp of this turn</summary>
        public string Timestamp;

        /// <summary>Content for reasoning turns</summary>
        public string Content;

        /// <summary>Tool name for tool_call turns</summary>
        public string Tool;

        /// <summary>Parameters passed to tool</summary>
        public object Params;

        /// <summary>Result from tool</summary>
        public object Result;

        /// <summary>Source for observation turns (e.g., "compiler", "runtime")</summary>
        public string Source;

        /// <summary>Duration of this turn in milliseconds (for tool calls)</summary>
        public long? DurationMs;
    }

    /// <summary>
    /// Final outcome of a completed transcript.
    /// </summary>
    public class TranscriptOutcome
    {
        /// <summary>List of files created during this task</summary>
        public List<string> FilesCreated = new List<string>();

        /// <summary>List of files modified during this task</summary>
        public List<string> FilesModified = new List<string>();

        /// <summary>Compilation result: "success", "failed", "not_run"</summary>
        public string Compilation;

        /// <summary>Validation result: "pass", "fail", "not_run"</summary>
        public string Validation;

        /// <summary>Test result: "pass", "fail", "not_run"</summary>
        public string Tests;

        /// <summary>Human-readable summary of what was accomplished</summary>
        public string Summary;
    }

    /// <summary>
    /// Lightweight summary of a transcript for listing.
    /// </summary>
    public class TranscriptSummary
    {
        public string TaskId;
        public string Agent;
        public string Status;
        public string StartedAt;
        public string CompletedAt;
        public int TurnCount;
        public string OutcomeSummary;
    }
}
