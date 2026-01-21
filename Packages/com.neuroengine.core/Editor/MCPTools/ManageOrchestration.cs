#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using NeuroEngine.Services;
using Newtonsoft.Json.Linq;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool for Layer 6 Agent Orchestration.
    ///
    /// Enables the Mayor agent to:
    /// - Create and manage tasks
    /// - Assign tasks to specialized agents (Polecats)
    /// - Track task status and iteration counts
    /// - Complete or fail tasks with results
    ///
    /// Tasks are persisted to hooks/tasks/{taskId}.json for session survival.
    /// </summary>
    [McpForUnityTool("manage_orchestration", Description = "Layer 6 Agent Orchestration. Create tasks, assign to agents (ScriptPolecat, ScenePolecat, AssetPolecat, EyesPolecat), track status, complete/fail. Tasks persist to hooks/tasks/.")]
    public static class ManageOrchestration
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();

            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse("Required parameter 'action' is missing. Use: create_task, assign_task, start_task, complete_task, fail_task, retry_task, get_status, list_tasks, increment_iteration");
            }

            var orchestration = EditorServiceLocator.Get<IOrchestration>();

            try
            {
                switch (action)
                {
                    case "create_task":
                        return HandleCreateTask(@params, orchestration);

                    case "assign_task":
                        return HandleAssignTask(@params, orchestration);

                    case "start_task":
                        return HandleStartTask(@params, orchestration);

                    case "complete_task":
                        return HandleCompleteTask(@params, orchestration);

                    case "fail_task":
                        return HandleFailTask(@params, orchestration);

                    case "retry_task":
                        return HandleRetryTask(@params, orchestration);

                    case "get_status":
                        return HandleGetStatus(@params, orchestration);

                    case "list_tasks":
                        return HandleListTasks(@params, orchestration);

                    case "increment_iteration":
                        return HandleIncrementIteration(@params, orchestration);

                    default:
                        return new ErrorResponse($"Unknown action '{action}'. Use: create_task, assign_task, start_task, complete_task, fail_task, retry_task, get_status, list_tasks, increment_iteration");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Orchestration error: {e.Message}");
            }
        }

        private static object HandleCreateTask(JObject @params, IOrchestration orchestration)
        {
            string name = @params["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
            {
                return new ErrorResponse("Required parameter 'name' is missing.");
            }

            var config = new TaskConfig
            {
                Name = name,
                Description = @params["description"]?.ToString(),
                Iteration = @params["iteration"]?.ToString(),
                ConvoyId = @params["convoy_id"]?.ToString(),
                Priority = @params["priority"]?.Value<int>() ?? 1,
                Deliverable = @params["deliverable"]?.ToString(),
                EstimatedMinutes = @params["estimated_minutes"]?.Value<int>() ?? 15,
                MaxIterations = @params["max_iterations"]?.Value<int>() ?? 50
            };

            // Parse dependencies
            var deps = @params["dependencies"]?.ToObject<string[]>();
            if (deps != null)
                config.Dependencies = deps.ToList();

            // Parse success criteria
            var criteria = @params["success_criteria"]?.ToObject<string[]>();
            if (criteria != null)
                config.SuccessCriteria = criteria.ToList();

            // Parse specification
            var spec = @params["specification"]?.ToObject<Dictionary<string, object>>();
            if (spec != null)
                config.Specification = spec;

            var task = orchestration.CreateTask(config);

            return new SuccessResponse($"Created task {task.Id}", new
            {
                task_id = task.Id,
                name = task.Name,
                status = task.Status.ToString(),
                convoy_id = task.ConvoyId,
                priority = task.Priority,
                created_at = task.CreatedAt
            });
        }

        private static object HandleAssignTask(JObject @params, IOrchestration orchestration)
        {
            string taskId = @params["task_id"]?.ToString();
            string agentTypeStr = @params["agent_type"]?.ToString();

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }
            if (string.IsNullOrEmpty(agentTypeStr))
            {
                return new ErrorResponse("Required parameter 'agent_type' is missing. Use: ScriptPolecat, ScenePolecat, AssetPolecat, EyesPolecat, Evaluator");
            }

            if (!TryParseAgentType(agentTypeStr, out var agentType))
            {
                return new ErrorResponse($"Invalid agent_type '{agentTypeStr}'. Use: ScriptPolecat, ScenePolecat, AssetPolecat, EyesPolecat, Evaluator");
            }

            if (agentType == AgentType.Mayor)
            {
                return new ErrorResponse("Mayor should orchestrate, not execute. Assign to a Polecat instead.");
            }

            orchestration.AssignTask(taskId, agentType);

            var task = orchestration.GetTaskStatus(taskId);
            return new SuccessResponse($"Task {taskId} assigned to {agentType}", new
            {
                task_id = taskId,
                agent_type = agentType.ToString(),
                status = task?.Status.ToString(),
                assigned_at = task?.AssignedAt
            });
        }

        private static object HandleStartTask(JObject @params, IOrchestration orchestration)
        {
            string taskId = @params["task_id"]?.ToString();

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            // Cast to access StartTask method
            if (orchestration is OrchestrationService svc)
            {
                svc.StartTask(taskId);
            }
            else
            {
                return new ErrorResponse("StartTask not available on this orchestration implementation.");
            }

            var task = orchestration.GetTaskStatus(taskId);
            return new SuccessResponse($"Task {taskId} started", new
            {
                task_id = taskId,
                status = task?.Status.ToString(),
                started_at = task?.StartedAt,
                agent_type = task?.AssignedAgent?.ToString()
            });
        }

        private static object HandleCompleteTask(JObject @params, IOrchestration orchestration)
        {
            string taskId = @params["task_id"]?.ToString();

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            var result = new TaskCompletionResult
            {
                Success = true,
                Summary = @params["summary"]?.ToString() ?? "Task completed"
            };

            // Parse files created
            var filesCreated = @params["files_created"]?.ToObject<string[]>();
            if (filesCreated != null)
                result.FilesCreated = filesCreated.ToList();

            // Parse files modified
            var filesModified = @params["files_modified"]?.ToObject<string[]>();
            if (filesModified != null)
                result.FilesModified = filesModified.ToList();

            // Parse warnings
            var warnings = @params["warnings"]?.ToObject<string[]>();
            if (warnings != null)
                result.Warnings = warnings.ToList();

            // Parse verification
            var verificationJson = @params["verification"];
            if (verificationJson != null)
            {
                result.Verification = new VerificationResult
                {
                    Passed = verificationJson["passed"]?.Value<bool>() ?? true,
                    VerifiedAt = DateTime.UtcNow.ToString("o")
                };

                var errors = verificationJson["errors"]?.ToObject<string[]>();
                if (errors != null)
                    result.Verification.Errors = errors.ToList();

                var vWarnings = verificationJson["warnings"]?.ToObject<string[]>();
                if (vWarnings != null)
                    result.Verification.Warnings = vWarnings.ToList();

                var missingRefs = verificationJson["missing_references"]?.ToObject<string[]>();
                if (missingRefs != null)
                    result.Verification.MissingReferences = missingRefs.ToList();
            }

            orchestration.CompleteTask(taskId, result);

            return new SuccessResponse($"Task {taskId} completed", new
            {
                task_id = taskId,
                status = "Completed",
                summary = result.Summary,
                files_created = result.FilesCreated,
                files_modified = result.FilesModified
            });
        }

        private static object HandleFailTask(JObject @params, IOrchestration orchestration)
        {
            string taskId = @params["task_id"]?.ToString();
            string reason = @params["reason"]?.ToString();

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }
            if (string.IsNullOrEmpty(reason))
            {
                return new ErrorResponse("Required parameter 'reason' is missing.");
            }

            orchestration.FailTask(taskId, reason);

            var task = orchestration.GetTaskStatus(taskId);
            return new SuccessResponse($"Task {taskId} failed", new
            {
                task_id = taskId,
                status = "Failed",
                reason = reason,
                iteration_count = task?.IterationCount,
                max_iterations = task?.MaxIterations,
                escalation_required = task != null && task.IterationCount >= task.MaxIterations
            });
        }

        private static object HandleRetryTask(JObject @params, IOrchestration orchestration)
        {
            string taskId = @params["task_id"]?.ToString();

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            // Cast to access RetryTask method
            if (orchestration is OrchestrationService svc)
            {
                svc.RetryTask(taskId);
            }
            else
            {
                return new ErrorResponse("RetryTask not available on this orchestration implementation.");
            }

            var task = orchestration.GetTaskStatus(taskId);
            return new SuccessResponse($"Task {taskId} reset for retry", new
            {
                task_id = taskId,
                status = task?.Status.ToString(),
                iteration_count = task?.IterationCount,
                max_iterations = task?.MaxIterations
            });
        }

        private static object HandleGetStatus(JObject @params, IOrchestration orchestration)
        {
            string taskId = @params["task_id"]?.ToString();

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            var task = orchestration.GetTaskStatus(taskId);

            if (task == null)
            {
                return new ErrorResponse($"Task '{taskId}' not found.");
            }

            return new SuccessResponse($"Task {taskId} status", new
            {
                task_id = task.Id,
                name = task.Name,
                description = task.Description,
                iteration = task.Iteration,
                convoy_id = task.ConvoyId,
                status = task.Status.ToString(),
                assigned_agent = task.AssignedAgent?.ToString(),
                priority = task.Priority,
                deliverable = task.Deliverable,
                success_criteria = task.SuccessCriteria,
                iteration_count = task.IterationCount,
                max_iterations = task.MaxIterations,
                dependencies = task.Dependencies,
                created_at = task.CreatedAt,
                assigned_at = task.AssignedAt,
                started_at = task.StartedAt,
                completed_at = task.CompletedAt,
                error_message = task.ErrorMessage,
                result = task.Result != null ? new
                {
                    success = task.Result.Success,
                    summary = task.Result.Summary,
                    files_created = task.Result.FilesCreated,
                    files_modified = task.Result.FilesModified,
                    warnings = task.Result.Warnings
                } : null,
                history = task.History?.Select(h => new
                {
                    timestamp = h.Timestamp,
                    from_status = h.FromStatus?.ToString(),
                    to_status = h.ToStatus.ToString(),
                    agent = h.Agent?.ToString(),
                    message = h.Message
                }).ToList()
            });
        }

        private static object HandleListTasks(JObject @params, IOrchestration orchestration)
        {
            var filter = new TaskStatusFilter();

            // Parse status filter
            var statusStr = @params["status"]?.ToString();
            if (!string.IsNullOrEmpty(statusStr) && TryParseTaskStatus(statusStr, out var status))
            {
                filter.Status = status;
            }

            // Parse agent type filter
            var agentTypeStr = @params["agent_type"]?.ToString();
            if (!string.IsNullOrEmpty(agentTypeStr) && TryParseAgentType(agentTypeStr, out var agentType))
            {
                filter.AgentType = agentType;
            }

            filter.ConvoyId = @params["convoy_id"]?.ToString();
            filter.Iteration = @params["iteration"]?.ToString();
            filter.IncludeCompleted = @params["include_completed"]?.Value<bool>() ?? false;
            filter.Limit = @params["limit"]?.Value<int>();

            var tasks = orchestration.GetTasksAsync(filter).GetAwaiter().GetResult();

            return new SuccessResponse($"Found {tasks.Count} tasks", new
            {
                total_count = tasks.Count,
                tasks = tasks.Select(t => new
                {
                    task_id = t.Id,
                    name = t.Name,
                    status = t.Status.ToString(),
                    assigned_agent = t.AssignedAgent?.ToString(),
                    convoy_id = t.ConvoyId,
                    priority = t.Priority,
                    iteration_count = t.IterationCount,
                    created_at = t.CreatedAt
                }).ToList()
            });
        }

        private static object HandleIncrementIteration(JObject @params, IOrchestration orchestration)
        {
            string taskId = @params["task_id"]?.ToString();

            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            int newCount = orchestration.IncrementTaskIteration(taskId);
            var task = orchestration.GetTaskStatus(taskId);

            return new SuccessResponse($"Task {taskId} iteration incremented", new
            {
                task_id = taskId,
                iteration_count = newCount,
                max_iterations = task?.MaxIterations,
                escalation_required = task != null && newCount >= task.MaxIterations
            });
        }

        private static bool TryParseAgentType(string value, out AgentType result)
        {
            result = AgentType.ScriptPolecat;

            switch (value?.ToLowerInvariant())
            {
                case "scriptpolecat":
                case "script_polecat":
                case "script":
                    result = AgentType.ScriptPolecat;
                    return true;
                case "scenepolecat":
                case "scene_polecat":
                case "scene":
                    result = AgentType.ScenePolecat;
                    return true;
                case "assetpolecat":
                case "asset_polecat":
                case "asset":
                    result = AgentType.AssetPolecat;
                    return true;
                case "eyespolecat":
                case "eyes_polecat":
                case "eyes":
                    result = AgentType.EyesPolecat;
                    return true;
                case "evaluator":
                    result = AgentType.Evaluator;
                    return true;
                case "mayor":
                    result = AgentType.Mayor;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseTaskStatus(string value, out TaskStatus result)
        {
            result = TaskStatus.Pending;

            switch (value?.ToLowerInvariant())
            {
                case "pending":
                    result = TaskStatus.Pending;
                    return true;
                case "assigned":
                    result = TaskStatus.Assigned;
                    return true;
                case "in_progress":
                case "inprogress":
                    result = TaskStatus.InProgress;
                    return true;
                case "completed":
                    result = TaskStatus.Completed;
                    return true;
                case "failed":
                    result = TaskStatus.Failed;
                    return true;
                case "cancelled":
                case "canceled":
                    result = TaskStatus.Cancelled;
                    return true;
                case "blocked":
                    result = TaskStatus.Blocked;
                    return true;
                default:
                    return false;
            }
        }
    }
}
#endif
