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
    /// MCP tool for Layer 6 Convoy Management.
    ///
    /// Enables the Mayor agent to:
    /// - Create convoys (groups of related tasks)
    /// - Add/remove tasks from convoys
    /// - Track convoy dependencies and completion
    /// - Get convoy progress and status
    ///
    /// Convoys are persisted to hooks/convoys/{convoyId}.json for session survival.
    /// </summary>
    [McpForUnityTool("manage_convoys", Description = "Layer 6 Convoy Management. Create task groups, track dependencies, monitor progress. Convoys persist to hooks/convoys/. Use for coordinated multi-task delivery.")]
    public static class ManageConvoys
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();

            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse("Required parameter 'action' is missing. Use: create, add_task, remove_task, complete, fail, get_status, list, get_tasks, add_dependency, get_next_ready");
            }

            var orchestration = EditorServiceLocator.Get<IOrchestration>();
            var convoyService = new ConvoyService(orchestration);

            try
            {
                switch (action)
                {
                    case "create":
                        return HandleCreate(@params, orchestration);

                    case "add_task":
                        return HandleAddTask(@params, convoyService);

                    case "remove_task":
                        return HandleRemoveTask(@params, convoyService);

                    case "complete":
                        return HandleComplete(@params, orchestration);

                    case "fail":
                        return HandleFail(@params, orchestration);

                    case "get_status":
                        return HandleGetStatus(@params, orchestration);

                    case "list":
                        return HandleList(@params, orchestration);

                    case "get_tasks":
                        return HandleGetTasks(@params, convoyService);

                    case "add_dependency":
                        return HandleAddDependency(@params, convoyService);

                    case "get_next_ready":
                        return HandleGetNextReady(@params, convoyService);

                    default:
                        return new ErrorResponse($"Unknown action '{action}'. Use: create, add_task, remove_task, complete, fail, get_status, list, get_tasks, add_dependency, get_next_ready");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Convoy error: {e.Message}");
            }
        }

        private static object HandleCreate(JObject @params, IOrchestration orchestration)
        {
            string name = @params["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
            {
                return new ErrorResponse("Required parameter 'name' is missing.");
            }

            var config = new ConvoyConfig
            {
                Name = name,
                Description = @params["description"]?.ToString(),
                Iteration = @params["iteration"]?.ToString(),
                Priority = @params["priority"]?.Value<int>() ?? 1
            };

            // Parse dependencies
            var deps = @params["dependencies"]?.ToObject<string[]>();
            if (deps != null)
                config.Dependencies = deps.ToList();

            // Parse task IDs
            var taskIds = @params["task_ids"]?.ToObject<string[]>();
            if (taskIds != null)
                config.TaskIds = taskIds.ToList();

            // Parse deliverables
            var deliverables = @params["deliverables"]?.ToObject<string[]>();
            if (deliverables != null)
                config.Deliverables = deliverables.ToList();

            // Parse completion criteria
            var criteria = @params["completion_criteria"]?.ToObject<string[]>();
            if (criteria != null)
                config.CompletionCriteria = criteria.ToList();

            // Parse assigned agent
            var agentTypeStr = @params["assigned_agent"]?.ToString();
            if (!string.IsNullOrEmpty(agentTypeStr) && TryParseAgentType(agentTypeStr, out var agentType))
            {
                config.AssignedAgent = agentType;
            }

            var convoy = orchestration.CreateConvoy(config);

            return new SuccessResponse($"Created convoy {convoy.Id}", new
            {
                convoy_id = convoy.Id,
                name = convoy.Name,
                status = convoy.Status.ToString(),
                task_count = convoy.TaskIds.Count,
                dependency_count = convoy.Dependencies.Count,
                created_at = convoy.CreatedAt
            });
        }

        private static object HandleAddTask(JObject @params, ConvoyService convoyService)
        {
            string convoyId = @params["convoy_id"]?.ToString();
            string taskId = @params["task_id"]?.ToString();

            if (string.IsNullOrEmpty(convoyId))
            {
                return new ErrorResponse("Required parameter 'convoy_id' is missing.");
            }
            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            convoyService.AddTask(convoyId, taskId);

            var convoy = convoyService.GetStatus(convoyId);
            return new SuccessResponse($"Added task {taskId} to convoy {convoyId}", new
            {
                convoy_id = convoyId,
                task_id = taskId,
                total_tasks = convoy?.TaskIds.Count,
                progress = convoy?.Progress != null ? new
                {
                    pending = convoy.Progress.PendingTasks,
                    in_progress = convoy.Progress.InProgressTasks,
                    completed = convoy.Progress.CompletedTasks,
                    percent_complete = convoy.Progress.PercentComplete
                } : null
            });
        }

        private static object HandleRemoveTask(JObject @params, ConvoyService convoyService)
        {
            string convoyId = @params["convoy_id"]?.ToString();
            string taskId = @params["task_id"]?.ToString();

            if (string.IsNullOrEmpty(convoyId))
            {
                return new ErrorResponse("Required parameter 'convoy_id' is missing.");
            }
            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            convoyService.RemoveTask(convoyId, taskId);

            var convoy = convoyService.GetStatus(convoyId);
            return new SuccessResponse($"Removed task {taskId} from convoy {convoyId}", new
            {
                convoy_id = convoyId,
                task_id = taskId,
                remaining_tasks = convoy?.TaskIds.Count
            });
        }

        private static object HandleComplete(JObject @params, IOrchestration orchestration)
        {
            string convoyId = @params["convoy_id"]?.ToString();

            if (string.IsNullOrEmpty(convoyId))
            {
                return new ErrorResponse("Required parameter 'convoy_id' is missing.");
            }

            orchestration.CompleteConvoy(convoyId);

            var convoy = orchestration.GetConvoyStatus(convoyId);
            return new SuccessResponse($"Convoy {convoyId} completed", new
            {
                convoy_id = convoyId,
                status = convoy?.Status.ToString(),
                completed_at = convoy?.CompletedAt
            });
        }

        private static object HandleFail(JObject @params, IOrchestration orchestration)
        {
            string convoyId = @params["convoy_id"]?.ToString();
            string reason = @params["reason"]?.ToString();

            if (string.IsNullOrEmpty(convoyId))
            {
                return new ErrorResponse("Required parameter 'convoy_id' is missing.");
            }
            if (string.IsNullOrEmpty(reason))
            {
                return new ErrorResponse("Required parameter 'reason' is missing.");
            }

            orchestration.FailConvoy(convoyId, reason);

            return new SuccessResponse($"Convoy {convoyId} failed", new
            {
                convoy_id = convoyId,
                status = "Failed",
                reason = reason
            });
        }

        private static object HandleGetStatus(JObject @params, IOrchestration orchestration)
        {
            string convoyId = @params["convoy_id"]?.ToString();

            if (string.IsNullOrEmpty(convoyId))
            {
                return new ErrorResponse("Required parameter 'convoy_id' is missing.");
            }

            var convoy = orchestration.GetConvoyStatus(convoyId);

            if (convoy == null)
            {
                return new ErrorResponse($"Convoy '{convoyId}' not found.");
            }

            return new SuccessResponse($"Convoy {convoyId} status", new
            {
                convoy_id = convoy.Id,
                name = convoy.Name,
                description = convoy.Description,
                iteration = convoy.Iteration,
                status = convoy.Status.ToString(),
                assigned_agent = convoy.AssignedAgent?.ToString(),
                priority = convoy.Priority,
                task_ids = convoy.TaskIds,
                dependencies = convoy.Dependencies,
                deliverables = convoy.Deliverables,
                completion_criteria = convoy.CompletionCriteria,
                created_at = convoy.CreatedAt,
                completed_at = convoy.CompletedAt,
                error_message = convoy.ErrorMessage,
                progress = convoy.Progress != null ? new
                {
                    total = convoy.Progress.TotalTasks,
                    pending = convoy.Progress.PendingTasks,
                    assigned = convoy.Progress.AssignedTasks,
                    in_progress = convoy.Progress.InProgressTasks,
                    completed = convoy.Progress.CompletedTasks,
                    failed = convoy.Progress.FailedTasks,
                    blocked = convoy.Progress.BlockedTasks,
                    percent_complete = convoy.Progress.PercentComplete,
                    all_complete = convoy.Progress.AllTasksComplete,
                    has_failures = convoy.Progress.HasFailures
                } : null
            });
        }

        private static object HandleList(JObject @params, IOrchestration orchestration)
        {
            var filter = new ConvoyStatusFilter();

            // Parse status filter
            var statusStr = @params["status"]?.ToString();
            if (!string.IsNullOrEmpty(statusStr) && TryParseConvoyStatus(statusStr, out var status))
            {
                filter.Status = status;
            }

            filter.Iteration = @params["iteration"]?.ToString();
            filter.IncludeCompleted = @params["include_completed"]?.Value<bool>() ?? false;
            filter.Limit = @params["limit"]?.Value<int>();

            var convoys = orchestration.GetConvoysAsync(filter).GetAwaiter().GetResult();

            return new SuccessResponse($"Found {convoys.Count} convoys", new
            {
                total_count = convoys.Count,
                convoys = convoys.Select(c => new
                {
                    convoy_id = c.Id,
                    name = c.Name,
                    status = c.Status.ToString(),
                    priority = c.Priority,
                    task_count = c.TaskIds.Count,
                    dependency_count = c.Dependencies.Count,
                    progress = c.Progress != null ? new
                    {
                        completed = c.Progress.CompletedTasks,
                        total = c.Progress.TotalTasks,
                        percent = c.Progress.PercentComplete
                    } : null,
                    created_at = c.CreatedAt
                }).ToList()
            });
        }

        private static object HandleGetTasks(JObject @params, ConvoyService convoyService)
        {
            string convoyId = @params["convoy_id"]?.ToString();

            if (string.IsNullOrEmpty(convoyId))
            {
                return new ErrorResponse("Required parameter 'convoy_id' is missing.");
            }

            var summary = convoyService.GetTasksSummaryAsync(convoyId).GetAwaiter().GetResult();

            return new SuccessResponse($"Convoy {convoyId} tasks", new
            {
                convoy_id = summary.ConvoyId,
                convoy_name = summary.ConvoyName,
                total_tasks = summary.TotalTasks,
                active_tasks = summary.ActiveTasks,
                pending = summary.Pending.Select(t => new { id = t.Id, name = t.Name }).ToList(),
                assigned = summary.Assigned.Select(t => new { id = t.Id, name = t.Name, agent = t.AssignedAgent?.ToString() }).ToList(),
                in_progress = summary.InProgress.Select(t => new { id = t.Id, name = t.Name, agent = t.AssignedAgent?.ToString() }).ToList(),
                completed = summary.Completed.Select(t => new { id = t.Id, name = t.Name }).ToList(),
                failed = summary.Failed.Select(t => new { id = t.Id, name = t.Name, error = t.ErrorMessage, iterations = t.IterationCount }).ToList(),
                blocked = summary.Blocked.Select(t => new { id = t.Id, name = t.Name, dependencies = t.Dependencies }).ToList()
            });
        }

        private static object HandleAddDependency(JObject @params, ConvoyService convoyService)
        {
            string convoyId = @params["convoy_id"]?.ToString();
            string dependsOn = @params["depends_on"]?.ToString();

            if (string.IsNullOrEmpty(convoyId))
            {
                return new ErrorResponse("Required parameter 'convoy_id' is missing.");
            }
            if (string.IsNullOrEmpty(dependsOn))
            {
                return new ErrorResponse("Required parameter 'depends_on' is missing.");
            }

            convoyService.AddDependency(convoyId, dependsOn);

            var convoy = convoyService.GetStatus(convoyId);
            return new SuccessResponse($"Convoy {convoyId} now depends on {dependsOn}", new
            {
                convoy_id = convoyId,
                depends_on = dependsOn,
                all_dependencies = convoy?.Dependencies,
                status = convoy?.Status.ToString(),
                dependencies_satisfied = convoyService.AreDependenciesSatisfied(convoyId)
            });
        }

        private static object HandleGetNextReady(JObject @params, ConvoyService convoyService)
        {
            string iteration = @params["iteration"]?.ToString();

            var convoy = convoyService.GetNextReadyConvoyAsync(iteration).GetAwaiter().GetResult();

            if (convoy == null)
            {
                return new SuccessResponse("No ready convoys found", new
                {
                    iteration = iteration,
                    convoy = (object)null
                });
            }

            return new SuccessResponse($"Next ready convoy: {convoy.Id}", new
            {
                convoy_id = convoy.Id,
                name = convoy.Name,
                description = convoy.Description,
                priority = convoy.Priority,
                task_count = convoy.TaskIds.Count,
                task_ids = convoy.TaskIds,
                assigned_agent = convoy.AssignedAgent?.ToString()
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

        private static bool TryParseConvoyStatus(string value, out ConvoyStatus result)
        {
            result = ConvoyStatus.Pending;

            switch (value?.ToLowerInvariant())
            {
                case "pending":
                    result = ConvoyStatus.Pending;
                    return true;
                case "blocked":
                    result = ConvoyStatus.Blocked;
                    return true;
                case "in_progress":
                case "inprogress":
                    result = ConvoyStatus.InProgress;
                    return true;
                case "completed":
                    result = ConvoyStatus.Completed;
                    return true;
                case "failed":
                    result = ConvoyStatus.Failed;
                    return true;
                case "cancelled":
                case "canceled":
                    result = ConvoyStatus.Cancelled;
                    return true;
                default:
                    return false;
            }
        }
    }
}
#endif
