#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using Newtonsoft.Json.Linq;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool to manage Layer 6 Safety Controls.
    /// Enables checking limits, recording costs, managing approvals, and triggering rollbacks.
    ///
    /// Actions:
    /// - check_limits: Check all safety limits (iterations, budget, agents)
    /// - check_iteration: Check iteration limit for a specific task
    /// - increment_iteration: Increment iteration count for a task
    /// - reset_iterations: Reset iteration count for a task
    /// - check_budget: Check if a cost can be incurred
    /// - record_cost: Record an API cost
    /// - get_budget: Get current budget status
    /// - check_agents: Check if more agents can be spawned
    /// - register_agent: Register an active agent
    /// - unregister_agent: Unregister an agent
    /// - request_approval: Request human approval
    /// - get_approval: Get approval request status
    /// - list_approvals: List pending approvals
    /// - resolve_approval: Approve or reject a request
    /// - trigger_rollback: Trigger a git rollback
    /// </summary>
    [McpForUnityTool("manage_safety", Description = "Manages Layer 6 safety controls. Check limits, record costs, request approvals, trigger rollbacks. Enforces iteration limits (50/task), budget limits ($10/hour), and parallel agent limits (5).")]
    public static class ManageSafety
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();

            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse(
                    "Required parameter 'action' is missing. Use: check_limits, check_iteration, increment_iteration, " +
                    "reset_iterations, check_budget, record_cost, get_budget, check_agents, register_agent, " +
                    "unregister_agent, request_approval, get_approval, list_approvals, resolve_approval, trigger_rollback");
            }

            var safetyControl = EditorServiceLocator.Get<ISafetyControl>();

            try
            {
                switch (action)
                {
                    case "check_limits":
                        return HandleCheckLimits(@params, safetyControl);

                    case "check_iteration":
                        return HandleCheckIteration(@params, safetyControl);

                    case "increment_iteration":
                        return HandleIncrementIteration(@params, safetyControl);

                    case "reset_iterations":
                        return HandleResetIterations(@params, safetyControl);

                    case "check_budget":
                        return HandleCheckBudget(@params, safetyControl);

                    case "record_cost":
                        return HandleRecordCost(@params, safetyControl);

                    case "get_budget":
                        return HandleGetBudget(safetyControl);

                    case "check_agents":
                        return HandleCheckAgents(safetyControl);

                    case "register_agent":
                        return HandleRegisterAgent(@params, safetyControl);

                    case "unregister_agent":
                        return HandleUnregisterAgent(@params, safetyControl);

                    case "request_approval":
                        return HandleRequestApproval(@params, safetyControl);

                    case "get_approval":
                        return HandleGetApproval(@params, safetyControl);

                    case "list_approvals":
                        return HandleListApprovals(safetyControl);

                    case "resolve_approval":
                        return HandleResolveApproval(@params, safetyControl);

                    case "trigger_rollback":
                        return HandleTriggerRollback(@params, safetyControl);

                    default:
                        return new ErrorResponse(
                            $"Unknown action '{action}'. Use: check_limits, check_iteration, increment_iteration, " +
                            "reset_iterations, check_budget, record_cost, get_budget, check_agents, register_agent, " +
                            "unregister_agent, request_approval, get_approval, list_approvals, resolve_approval, trigger_rollback");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error managing safety controls: {e.Message}");
            }
        }

        private static object HandleCheckLimits(JObject @params, ISafetyControl safetyControl)
        {
            string taskId = @params["task_id"]?.ToString();
            decimal costEstimate = @params["cost_estimate"]?.Value<decimal>() ?? 0m;

            var budget = safetyControl.GetBudgetStatus();
            var iterationInfo = !string.IsNullOrEmpty(taskId) ? safetyControl.GetIterationInfo(taskId) : null;
            var canSpawnAgent = safetyControl.CheckParallelAgents();
            var activeAgentCount = safetyControl.GetActiveAgentCount();

            var allClear = true;
            var issues = new List<string>();

            // Check iteration limit
            if (iterationInfo != null && iterationInfo.LimitReached)
            {
                allClear = false;
                issues.Add($"Task {taskId} has reached iteration limit ({iterationInfo.MaxIterations})");
            }

            // Check budget
            if (budget.IsPaused)
            {
                allClear = false;
                issues.Add($"Budget is paused: {budget.PauseReason}");
            }
            else if (costEstimate > 0 && !safetyControl.CheckBudget(costEstimate))
            {
                allClear = false;
                issues.Add($"Estimated cost ${costEstimate:F4} would exceed remaining budget ${budget.RemainingBudget:F4}");
            }

            // Check agent limit
            if (!canSpawnAgent)
            {
                allClear = false;
                issues.Add($"Parallel agent limit reached ({activeAgentCount}/5)");
            }

            return new SuccessResponse(allClear ? "All safety checks passed" : "Safety limits reached", new
            {
                all_clear = allClear,
                issues = issues,
                iteration = iterationInfo != null ? new
                {
                    task_id = iterationInfo.TaskId,
                    current = iterationInfo.CurrentIteration,
                    max = iterationInfo.MaxIterations,
                    remaining = iterationInfo.RemainingIterations,
                    limit_reached = iterationInfo.LimitReached
                } : null,
                budget = new
                {
                    hourly_limit = budget.HourlyLimit,
                    spent_this_hour = budget.SpentThisHour,
                    remaining = budget.RemainingBudget,
                    is_paused = budget.IsPaused,
                    pause_reason = budget.PauseReason
                },
                agents = new
                {
                    active_count = activeAgentCount,
                    max_parallel = 5,
                    can_spawn_more = canSpawnAgent
                }
            });
        }

        private static object HandleCheckIteration(JObject @params, ISafetyControl safetyControl)
        {
            string taskId = @params["task_id"]?.ToString();
            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            var canContinue = safetyControl.CheckIterationLimit(taskId);
            var info = safetyControl.GetIterationInfo(taskId);

            return new SuccessResponse(canContinue ? "Safe to continue" : "Iteration limit reached", new
            {
                task_id = taskId,
                can_continue = canContinue,
                current_iteration = info.CurrentIteration,
                max_iterations = info.MaxIterations,
                remaining = info.RemainingIterations,
                limit_reached = info.LimitReached,
                first_iteration = info.FirstIteration.ToString("o"),
                last_iteration = info.LastIteration.ToString("o")
            });
        }

        private static object HandleIncrementIteration(JObject @params, ISafetyControl safetyControl)
        {
            string taskId = @params["task_id"]?.ToString();
            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            safetyControl.IncrementIteration(taskId);
            var info = safetyControl.GetIterationInfo(taskId);

            return new SuccessResponse($"Iteration incremented for {taskId}", new
            {
                task_id = taskId,
                current_iteration = info.CurrentIteration,
                max_iterations = info.MaxIterations,
                remaining = info.RemainingIterations,
                limit_reached = info.LimitReached
            });
        }

        private static object HandleResetIterations(JObject @params, ISafetyControl safetyControl)
        {
            string taskId = @params["task_id"]?.ToString();
            if (string.IsNullOrEmpty(taskId))
            {
                return new ErrorResponse("Required parameter 'task_id' is missing.");
            }

            safetyControl.ResetIterations(taskId);
            var info = safetyControl.GetIterationInfo(taskId);

            return new SuccessResponse($"Iterations reset for {taskId}", new
            {
                task_id = taskId,
                current_iteration = info.CurrentIteration,
                max_iterations = info.MaxIterations
            });
        }

        private static object HandleCheckBudget(JObject @params, ISafetyControl safetyControl)
        {
            decimal costEstimate = @params["cost_estimate"]?.Value<decimal>() ?? 0m;

            if (costEstimate <= 0)
            {
                return new ErrorResponse("Required parameter 'cost_estimate' must be a positive number.");
            }

            var canAfford = safetyControl.CheckBudget(costEstimate);
            var budget = safetyControl.GetBudgetStatus();

            return new SuccessResponse(canAfford ? "Budget allows" : "Budget would be exceeded", new
            {
                can_afford = canAfford,
                cost_estimate = costEstimate,
                remaining_budget = budget.RemainingBudget,
                spent_this_hour = budget.SpentThisHour,
                hourly_limit = budget.HourlyLimit,
                is_paused = budget.IsPaused
            });
        }

        private static object HandleRecordCost(JObject @params, ISafetyControl safetyControl)
        {
            decimal amount = @params["amount"]?.Value<decimal>() ?? 0m;
            string description = @params["description"]?.ToString();

            if (amount <= 0)
            {
                return new ErrorResponse("Required parameter 'amount' must be a positive number.");
            }

            if (string.IsNullOrEmpty(description))
            {
                description = "Unspecified cost";
            }

            safetyControl.RecordCost(amount, description);
            var budget = safetyControl.GetBudgetStatus();

            return new SuccessResponse($"Recorded cost: ${amount:F4}", new
            {
                amount = amount,
                description = description,
                spent_this_hour = budget.SpentThisHour,
                remaining_budget = budget.RemainingBudget,
                hourly_limit = budget.HourlyLimit,
                is_paused = budget.IsPaused,
                total_spent = budget.TotalSpent
            });
        }

        private static object HandleGetBudget(ISafetyControl safetyControl)
        {
            var budget = safetyControl.GetBudgetStatus();

            return new SuccessResponse("Budget status", new
            {
                hourly_limit = budget.HourlyLimit,
                spent_this_hour = budget.SpentThisHour,
                remaining_budget = budget.RemainingBudget,
                hour_window_start = budget.HourWindowStart.ToString("o"),
                hour_window_end = budget.HourWindowEnd.ToString("o"),
                total_spent = budget.TotalSpent,
                is_paused = budget.IsPaused,
                pause_reason = budget.PauseReason,
                recent_costs = budget.RecentCosts?.Select(c => new
                {
                    amount = c.Amount,
                    description = c.Description,
                    timestamp = c.Timestamp.ToString("o"),
                    task_id = c.TaskId,
                    agent_id = c.AgentId
                }).ToList()
            });
        }

        private static object HandleCheckAgents(ISafetyControl safetyControl)
        {
            var canSpawn = safetyControl.CheckParallelAgents();
            var activeCount = safetyControl.GetActiveAgentCount();

            return new SuccessResponse(canSpawn ? "Can spawn more agents" : "Agent limit reached", new
            {
                can_spawn_more = canSpawn,
                active_count = activeCount,
                max_parallel = 5,
                available_slots = Math.Max(0, 5 - activeCount)
            });
        }

        private static object HandleRegisterAgent(JObject @params, ISafetyControl safetyControl)
        {
            string agentId = @params["agent_id"]?.ToString();
            string agentType = @params["agent_type"]?.ToString() ?? "unknown";

            if (string.IsNullOrEmpty(agentId))
            {
                return new ErrorResponse("Required parameter 'agent_id' is missing.");
            }

            safetyControl.RegisterAgent(agentId, agentType);
            var activeCount = safetyControl.GetActiveAgentCount();
            var canSpawnMore = safetyControl.CheckParallelAgents();

            return new SuccessResponse($"Registered agent: {agentId}", new
            {
                agent_id = agentId,
                agent_type = agentType,
                active_count = activeCount,
                max_parallel = 5,
                can_spawn_more = canSpawnMore
            });
        }

        private static object HandleUnregisterAgent(JObject @params, ISafetyControl safetyControl)
        {
            string agentId = @params["agent_id"]?.ToString();

            if (string.IsNullOrEmpty(agentId))
            {
                return new ErrorResponse("Required parameter 'agent_id' is missing.");
            }

            safetyControl.UnregisterAgent(agentId);
            var activeCount = safetyControl.GetActiveAgentCount();

            return new SuccessResponse($"Unregistered agent: {agentId}", new
            {
                agent_id = agentId,
                active_count = activeCount,
                max_parallel = 5,
                can_spawn_more = true
            });
        }

        private static object HandleRequestApproval(JObject @params, ISafetyControl safetyControl)
        {
            string reason = @params["reason"]?.ToString();

            if (string.IsNullOrEmpty(reason))
            {
                return new ErrorResponse("Required parameter 'reason' is missing.");
            }

            var context = @params["context"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();

            // Add optional parameters to context
            if (@params["task_id"] != null)
                context["task_id"] = @params["task_id"].ToString();
            if (@params["agent_id"] != null)
                context["agent_id"] = @params["agent_id"].ToString();
            if (@params["priority"] != null)
                context["priority"] = @params["priority"].Value<int>();
            if (@params["category"] != null)
                context["category"] = @params["category"].ToString();

            var request = safetyControl.RequestHumanApproval(reason, context);

            return new SuccessResponse($"Created approval request: {request.RequestId}", new
            {
                request_id = request.RequestId,
                reason = request.Reason,
                status = request.Status,
                category = request.Category,
                priority = request.Priority,
                created_at = request.CreatedAt.ToString("o"),
                context = request.Context,
                note = "Approval requests are saved to hooks/reviews/pending-approval.json. " +
                       "Human can approve by calling manage_safety with action=resolve_approval"
            });
        }

        private static object HandleGetApproval(JObject @params, ISafetyControl safetyControl)
        {
            string requestId = @params["request_id"]?.ToString();

            if (string.IsNullOrEmpty(requestId))
            {
                return new ErrorResponse("Required parameter 'request_id' is missing.");
            }

            var status = safetyControl.GetApprovalStatus(requestId);

            if (status == null || status.Status == "not_found")
            {
                return new ErrorResponse($"Approval request '{requestId}' not found.");
            }

            return new SuccessResponse($"Approval status: {status.Status}", new
            {
                request_id = status.RequestId,
                status = status.Status,
                is_resolved = status.IsResolved,
                is_approved = status.IsApproved,
                reviewer_notes = status.ReviewerNotes,
                resolved_at = status.ResolvedAt?.ToString("o"),
                time_remaining = status.TimeRemaining?.TotalMinutes
            });
        }

        private static object HandleListApprovals(ISafetyControl safetyControl)
        {
            var pending = safetyControl.ListPendingApprovals();

            return new SuccessResponse($"Found {pending.Count} pending approvals", new
            {
                count = pending.Count,
                approvals = pending.Select(r => new
                {
                    request_id = r.RequestId,
                    reason = r.Reason,
                    category = r.Category,
                    priority = r.Priority,
                    created_at = r.CreatedAt.ToString("o"),
                    task_id = r.TaskId,
                    agent_id = r.AgentId
                }).ToList()
            });
        }

        private static object HandleResolveApproval(JObject @params, ISafetyControl safetyControl)
        {
            string requestId = @params["request_id"]?.ToString();
            bool? approved = @params["approved"]?.Value<bool>();
            string notes = @params["notes"]?.ToString();

            if (string.IsNullOrEmpty(requestId))
            {
                return new ErrorResponse("Required parameter 'request_id' is missing.");
            }

            if (!approved.HasValue)
            {
                return new ErrorResponse("Required parameter 'approved' (true/false) is missing.");
            }

            safetyControl.ResolveApproval(requestId, approved.Value, notes);
            var status = safetyControl.GetApprovalStatus(requestId);

            return new SuccessResponse($"Approval {requestId} {status.Status}", new
            {
                request_id = status.RequestId,
                status = status.Status,
                is_approved = status.IsApproved,
                reviewer_notes = status.ReviewerNotes,
                resolved_at = status.ResolvedAt?.ToString("o")
            });
        }

        private static object HandleTriggerRollback(JObject @params, ISafetyControl safetyControl)
        {
            string reason = @params["reason"]?.ToString();

            if (string.IsNullOrEmpty(reason))
            {
                return new ErrorResponse("Required parameter 'reason' is missing.");
            }

            var result = safetyControl.TriggerRollback(reason);

            if (result.Success)
            {
                return new SuccessResponse($"Rollback successful", new
                {
                    success = true,
                    rolled_back_from = result.RolledBackFromCommit,
                    rolled_back_to = result.RolledBackToCommit,
                    commits_rolled_back = result.CommitsRolledBack,
                    reason = result.Reason,
                    timestamp = result.Timestamp.ToString("o"),
                    affected_files = result.AffectedFiles,
                    note = "Unity may need to reimport assets after rollback. Consider calling refresh_unity."
                });
            }
            else
            {
                return new ErrorResponse($"Rollback failed: {result.ErrorMessage}");
            }
        }
    }
}
#endif
