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
    /// MCP tool to manage AI agent tasks.
    /// Enables creating, tracking, and completing tasks that persist across sessions.
    /// </summary>
    [McpForUnityTool("manage_tasks", Description = "Manages AI agent tasks. Create tasks, update progress, mark complete/failed. Tasks persist to hooks/tasks/ for multi-session continuity.")]
    public static class ManageTasks
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();

            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse("Required parameter 'action' is missing. Use: create, get, start, update_progress, complete, fail, cancel, list, get_next");
            }

            var taskManager = EditorServiceLocator.Get<ITaskManager>();

            try
            {
                switch (action)
                {
                    case "create":
                        return HandleCreate(@params, taskManager);

                    case "get":
                        return HandleGet(@params, taskManager);

                    case "start":
                        return HandleStart(@params, taskManager);

                    case "update_progress":
                        return HandleUpdateProgress(@params, taskManager);

                    case "complete":
                        return HandleComplete(@params, taskManager);

                    case "fail":
                        return HandleFail(@params, taskManager);

                    case "cancel":
                        return HandleCancel(@params, taskManager);

                    case "list":
                        return HandleList(@params, taskManager);

                    case "get_next":
                        return HandleGetNext(@params, taskManager);

                    default:
                        return new ErrorResponse($"Unknown action '{action}'. Use: create, get, start, update_progress, complete, fail, cancel, list, get_next");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error managing tasks: {e.Message}");
            }
        }

        private static object HandleCreate(JObject @params, ITaskManager taskManager)
        {
            string description = @params["description"]?.ToString();
            if (string.IsNullOrEmpty(description))
            {
                return new ErrorResponse("Required parameter 'description' is missing.");
            }

            var assignment = new TaskAssignment
            {
                Description = description,
                AgentType = @params["agent_type"]?.ToString() ?? "script",
                Priority = @params["priority"]?.Value<int>() ?? 1,
                ParentTaskId = @params["parent_task_id"]?.ToString(),
                ConvoyId = @params["convoy_id"]?.ToString()
            };

            // Parse input files
            var inputFiles = @params["input_files"]?.ToObject<string[]>();
            if (inputFiles != null)
                assignment.InputFiles = inputFiles.ToList();

            // Parse expected outputs
            var expectedOutputs = @params["expected_outputs"]?.ToObject<string[]>();
            if (expectedOutputs != null)
                assignment.ExpectedOutputs = expectedOutputs.ToList();

            // Parse tags
            var tags = @params["tags"]?.ToObject<string[]>();
            if (tags != null)
                assignment.Tags = tags.ToList();

            // Parse context
            var context = @params["context"]?.ToObject<System.Collections.Generic.Dictionary<string, object>>();
            if (context != null)
                assignment.Context = context;

            var task = taskManager.CreateTask(assignment);

            return new SuccessResponse($"Created task {task.TaskId}", new
            {
                task_id = task.TaskId,
                description = task.Assignment.Description,
                agent_type = task.Assignment.AgentType,
                priority = task.Assignment.Priority,
                status = task.Status,
                created_at = task.CreatedAt
            });
        }

        private static object HandleGet(JObject @params, ITaskManager taskManager)
        {
            string taskId = @params["task_id"]?.ToString();
            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            var task = taskManager.GetTask(taskId);
            if (task == null)
            {
                return new ErrorResponse($"Task '{taskId}' not found.");
            }

            return new SuccessResponse($"Task {taskId}", new
            {
                task_id = task.TaskId,
                description = task.Assignment?.Description,
                agent_type = task.Assignment?.AgentType,
                priority = task.Assignment?.Priority,
                status = task.Status,
                assigned_agent = task.AssignedAgent,
                created_at = task.CreatedAt,
                started_at = task.StartedAt,
                completed_at = task.CompletedAt,
                progress = task.Progress != null ? new
                {
                    current_step = task.Progress.CurrentStep,
                    percent_complete = task.Progress.PercentComplete,
                    steps_completed = task.Progress.StepsCompleted,
                    total_steps = task.Progress.TotalSteps,
                    blockers = task.Progress.Blockers
                } : null,
                result = task.Result != null ? new
                {
                    success = task.Result.Success,
                    summary = task.Result.Summary,
                    files_created = task.Result.FilesCreated,
                    files_modified = task.Result.FilesModified,
                    warnings = task.Result.Warnings
                } : null,
                error_message = task.ErrorMessage,
                transcript_path = task.TranscriptPath
            });
        }

        private static object HandleStart(JObject @params, ITaskManager taskManager)
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

            taskManager.StartTask(taskId, agentName);

            return new SuccessResponse($"Task {taskId} started by {agentName}", new
            {
                task_id = taskId,
                agent_name = agentName,
                status = "in_progress"
            });
        }

        private static object HandleUpdateProgress(JObject @params, ITaskManager taskManager)
        {
            string taskId = @params["task_id"]?.ToString();
            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            var progress = new TaskProgress
            {
                CurrentStep = @params["current_step"]?.ToString() ?? "Working",
                PercentComplete = @params["percent_complete"]?.Value<int>() ?? 0,
                StepsCompleted = @params["steps_completed"]?.Value<int>() ?? 0,
                TotalSteps = @params["total_steps"]?.Value<int>(),
                Blockers = @params["blockers"]?.ToString()
            };

            taskManager.UpdateProgress(taskId, progress);

            return new SuccessResponse($"Updated progress for {taskId}", new
            {
                task_id = taskId,
                current_step = progress.CurrentStep,
                percent_complete = progress.PercentComplete
            });
        }

        private static object HandleComplete(JObject @params, ITaskManager taskManager)
        {
            string taskId = @params["task_id"]?.ToString();
            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            var result = new TaskResult
            {
                Success = true,
                Summary = @params["summary"]?.ToString() ?? "Task completed"
            };

            var filesCreated = @params["files_created"]?.ToObject<string[]>();
            if (filesCreated != null)
                result.FilesCreated = filesCreated.ToList();

            var filesModified = @params["files_modified"]?.ToObject<string[]>();
            if (filesModified != null)
                result.FilesModified = filesModified.ToList();

            var warnings = @params["warnings"]?.ToObject<string[]>();
            if (warnings != null)
                result.Warnings = warnings.ToList();

            var followUps = @params["suggested_follow_ups"]?.ToObject<string[]>();
            if (followUps != null)
                result.SuggestedFollowUps = followUps.ToList();

            taskManager.CompleteTask(taskId, result);

            return new SuccessResponse($"Task {taskId} completed", new
            {
                task_id = taskId,
                status = "completed",
                summary = result.Summary
            });
        }

        private static object HandleFail(JObject @params, ITaskManager taskManager)
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

            taskManager.FailTask(taskId, errorMessage);

            return new SuccessResponse($"Task {taskId} marked as failed", new
            {
                task_id = taskId,
                status = "failed",
                error_message = errorMessage
            });
        }

        private static object HandleCancel(JObject @params, ITaskManager taskManager)
        {
            string taskId = @params["task_id"]?.ToString();
            string reason = @params["reason"]?.ToString() ?? "Cancelled by user";

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            taskManager.CancelTask(taskId, reason);

            return new SuccessResponse($"Task {taskId} cancelled", new
            {
                task_id = taskId,
                status = "cancelled",
                reason = reason
            });
        }

        private static object HandleList(JObject @params, ITaskManager taskManager)
        {
            var filter = new TaskFilter
            {
                Status = @params["status"]?.ToString(),
                AgentType = @params["agent_type"]?.ToString(),
                AssignedAgent = @params["assigned_agent"]?.ToString(),
                ConvoyId = @params["convoy_id"]?.ToString(),
                IncludeCompleted = @params["include_completed"]?.Value<bool>() ?? false,
                Limit = @params["limit"]?.Value<int>()
            };

            var tags = @params["tags"]?.ToObject<string[]>();
            if (tags != null)
                filter.Tags = tags.ToList();

            var tasks = taskManager.ListTasksAsync(filter).GetAwaiter().GetResult();

            return new SuccessResponse($"Found {tasks.Count} tasks", new
            {
                total_count = tasks.Count,
                tasks = tasks.Select(t => new
                {
                    task_id = t.TaskId,
                    description = t.Assignment?.Description,
                    agent_type = t.Assignment?.AgentType,
                    priority = t.Assignment?.Priority,
                    status = t.Status,
                    assigned_agent = t.AssignedAgent,
                    created_at = t.CreatedAt,
                    percent_complete = t.Progress?.PercentComplete
                }).ToList()
            });
        }

        private static object HandleGetNext(JObject @params, ITaskManager taskManager)
        {
            string agentType = @params["agent_type"]?.ToString() ?? "script";

            var task = taskManager.GetNextTask(agentType);

            if (task == null)
            {
                return new SuccessResponse($"No pending tasks for agent type '{agentType}'", new
                {
                    agent_type = agentType,
                    task = (object)null
                });
            }

            return new SuccessResponse($"Next task for {agentType}: {task.TaskId}", new
            {
                agent_type = agentType,
                task = new
                {
                    task_id = task.TaskId,
                    description = task.Assignment?.Description,
                    priority = task.Assignment?.Priority,
                    input_files = task.Assignment?.InputFiles,
                    expected_outputs = task.Assignment?.ExpectedOutputs,
                    context = task.Assignment?.Context
                }
            });
        }
    }
}
#endif
