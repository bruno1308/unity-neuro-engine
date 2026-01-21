#if UNITY_EDITOR
using System;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using Newtonsoft.Json.Linq;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to manage agent transcripts.
    /// Records reasoning, tool calls, and observations for debugging and learning.
    /// </summary>
    [McpForUnityTool("manage_transcripts", Description = "Manages agent reasoning transcripts. Start transcripts, add turns (reasoning, tool calls, observations), and complete with outcomes. Transcripts persist to hooks/tasks/{taskId}/transcript.json")]
    public static class ManageTranscripts
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();

            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse("Required parameter 'action' is missing. Use: start, add_reasoning, add_tool_call, add_observation, add_error, complete, fail, get, list");
            }

            var transcriptWriter = EditorServiceLocator.Get<ITranscriptWriter>();

            try
            {
                switch (action)
                {
                    case "start":
                        return HandleStart(@params, transcriptWriter);

                    case "add_reasoning":
                        return HandleAddReasoning(@params, transcriptWriter);

                    case "add_tool_call":
                        return HandleAddToolCall(@params, transcriptWriter);

                    case "add_observation":
                        return HandleAddObservation(@params, transcriptWriter);

                    case "add_error":
                        return HandleAddError(@params, transcriptWriter);

                    case "complete":
                        return HandleComplete(@params, transcriptWriter);

                    case "fail":
                        return HandleFail(@params, transcriptWriter);

                    case "get":
                        return HandleGet(@params, transcriptWriter);

                    case "list":
                        return HandleList(@params, transcriptWriter);

                    default:
                        return new ErrorResponse($"Unknown action '{action}'. Use: start, add_reasoning, add_tool_call, add_observation, add_error, complete, fail, get, list");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error managing transcripts: {e.Message}");
            }
        }

        private static object HandleStart(JObject @params, ITranscriptWriter writer)
        {
            string taskId = @params["task_id"]?.ToString();
            string agentName = @params["agent_name"]?.ToString();

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }
            if (string.IsNullOrEmpty(agentName))
            {
                return new ErrorResponse("Required parameter 'agent_name' is missing.");
            }

            var transcript = writer.StartTranscript(taskId, agentName);

            return new SuccessResponse($"Started transcript for task {taskId}", new
            {
                task_id = transcript.TaskId,
                agent = transcript.Agent,
                started_at = transcript.StartedAt,
                status = transcript.Status
            });
        }

        private static object HandleAddReasoning(JObject @params, ITranscriptWriter writer)
        {
            string taskId = @params["task_id"]?.ToString();
            string content = @params["content"]?.ToString();

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }
            if (string.IsNullOrEmpty(content))
            {
                return new ErrorResponse("Required parameter 'content' is missing.");
            }

            writer.AddReasoning(taskId, content);

            var transcript = writer.GetTranscript(taskId);
            return new SuccessResponse($"Added reasoning turn to {taskId}", new
            {
                task_id = taskId,
                turn = transcript?.Turns?.Count ?? 0,
                type = "reasoning"
            });
        }

        private static object HandleAddToolCall(JObject @params, ITranscriptWriter writer)
        {
            string taskId = @params["task_id"]?.ToString();
            string toolName = @params["tool_name"]?.ToString();

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }
            if (string.IsNullOrEmpty(toolName))
            {
                return new ErrorResponse("Required parameter 'tool_name' is missing.");
            }

            var toolParams = @params["params"]?.ToObject<object>();
            var result = @params["result"]?.ToObject<object>();

            writer.AddToolCall(taskId, toolName, toolParams, result);

            var transcript = writer.GetTranscript(taskId);
            return new SuccessResponse($"Added tool call to {taskId}", new
            {
                task_id = taskId,
                turn = transcript?.Turns?.Count ?? 0,
                type = "tool_call",
                tool = toolName
            });
        }

        private static object HandleAddObservation(JObject @params, ITranscriptWriter writer)
        {
            string taskId = @params["task_id"]?.ToString();
            string source = @params["source"]?.ToString();

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }
            if (string.IsNullOrEmpty(source))
            {
                return new ErrorResponse("Required parameter 'source' is missing (e.g., 'compiler', 'runtime', 'validation').");
            }

            var content = @params["content"]?.ToObject<object>();
            writer.AddObservation(taskId, source, content);

            var transcript = writer.GetTranscript(taskId);
            return new SuccessResponse($"Added observation to {taskId}", new
            {
                task_id = taskId,
                turn = transcript?.Turns?.Count ?? 0,
                type = "observation",
                source = source
            });
        }

        private static object HandleAddError(JObject @params, ITranscriptWriter writer)
        {
            string taskId = @params["task_id"]?.ToString();
            string message = @params["message"]?.ToString();

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }
            if (string.IsNullOrEmpty(message))
            {
                return new ErrorResponse("Required parameter 'message' is missing.");
            }

            string stackTrace = @params["stack_trace"]?.ToString();
            writer.AddError(taskId, message, stackTrace);

            var transcript = writer.GetTranscript(taskId);
            return new SuccessResponse($"Added error to {taskId}", new
            {
                task_id = taskId,
                turn = transcript?.Turns?.Count ?? 0,
                type = "error"
            });
        }

        private static object HandleComplete(JObject @params, ITranscriptWriter writer)
        {
            string taskId = @params["task_id"]?.ToString();
            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            var outcome = new TranscriptOutcome
            {
                Summary = @params["summary"]?.ToString() ?? "Completed",
                Compilation = @params["compilation"]?.ToString() ?? "not_run",
                Validation = @params["validation"]?.ToString() ?? "not_run",
                Tests = @params["tests"]?.ToString() ?? "not_run"
            };

            var filesCreated = @params["files_created"]?.ToObject<string[]>();
            if (filesCreated != null)
                outcome.FilesCreated = filesCreated.ToList();

            var filesModified = @params["files_modified"]?.ToObject<string[]>();
            if (filesModified != null)
                outcome.FilesModified = filesModified.ToList();

            writer.CompleteTranscript(taskId, outcome);

            return new SuccessResponse($"Completed transcript for {taskId}", new
            {
                task_id = taskId,
                status = "success",
                summary = outcome.Summary
            });
        }

        private static object HandleFail(JObject @params, ITranscriptWriter writer)
        {
            string taskId = @params["task_id"]?.ToString();
            string errorMessage = @params["error_message"]?.ToString();

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }
            if (string.IsNullOrEmpty(errorMessage))
            {
                return new ErrorResponse("Required parameter 'error_message' is missing.");
            }

            writer.FailTranscript(taskId, errorMessage);

            return new SuccessResponse($"Failed transcript for {taskId}", new
            {
                task_id = taskId,
                status = "failed",
                error_message = errorMessage
            });
        }

        private static object HandleGet(JObject @params, ITranscriptWriter writer)
        {
            string taskId = @params["task_id"]?.ToString();
            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            var transcript = writer.GetTranscript(taskId);
            if (transcript == null)
            {
                return new ErrorResponse($"Transcript for task '{taskId}' not found.");
            }

            return new SuccessResponse($"Transcript for {taskId}", new
            {
                task_id = transcript.TaskId,
                agent = transcript.Agent,
                status = transcript.Status,
                started_at = transcript.StartedAt,
                completed_at = transcript.CompletedAt,
                turn_count = transcript.Turns?.Count ?? 0,
                turns = transcript.Turns?.Select(t => new
                {
                    turn = t.Turn,
                    type = t.Type,
                    timestamp = t.Timestamp,
                    content = t.Content,
                    tool = t.Tool,
                    source = t.Source,
                    duration_ms = t.DurationMs
                }).ToList(),
                outcome = transcript.Outcome != null ? new
                {
                    summary = transcript.Outcome.Summary,
                    files_created = transcript.Outcome.FilesCreated,
                    files_modified = transcript.Outcome.FilesModified,
                    compilation = transcript.Outcome.Compilation,
                    validation = transcript.Outcome.Validation,
                    tests = transcript.Outcome.Tests
                } : null,
                error_message = transcript.ErrorMessage
            });
        }

        private static object HandleList(JObject @params, ITranscriptWriter writer)
        {
            string status = @params["status"]?.ToString();
            string agent = @params["agent"]?.ToString();

            var summaries = writer.ListTranscriptsAsync(status, agent).GetAwaiter().GetResult();

            return new SuccessResponse($"Found {summaries.Count} transcripts", new
            {
                total_count = summaries.Count,
                transcripts = summaries.Select(s => new
                {
                    task_id = s.TaskId,
                    agent = s.Agent,
                    status = s.Status,
                    started_at = s.StartedAt,
                    completed_at = s.CompletedAt,
                    turn_count = s.TurnCount,
                    summary = s.OutcomeSummary
                }).ToList()
            });
        }
    }
}
#endif
