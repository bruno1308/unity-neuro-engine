using System;
using System.Collections.Generic;
using System.IO;
using NeuroEngine.Core;
using NeuroEngine.Services;
using NUnit.Framework;
using UnityEngine;

namespace NeuroEngine.Tests.Layer6
{
    /// <summary>
    /// Tests for Layer 6 SafetyControlService.
    /// Verifies iteration limits, budget tracking, agent management, and approval workflow.
    /// </summary>
    [TestFixture]
    public class SafetyControlTests
    {
        private SafetyControlService _service;
        private string _testHooksPath;

        [SetUp]
        public void SetUp()
        {
            // Create a temporary hooks directory for testing
            _testHooksPath = Path.Combine(Path.GetTempPath(), $"neuro-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testHooksPath);
            Directory.CreateDirectory(Path.Combine(_testHooksPath, "orchestration"));
            Directory.CreateDirectory(Path.Combine(_testHooksPath, "reviews"));

            // Create service
            _service = new SafetyControlService();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test directory
            if (Directory.Exists(_testHooksPath))
            {
                try
                {
                    Directory.Delete(_testHooksPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region Iteration Limit Tests

        [Test]
        public void CheckIterationLimit_NewTask_ReturnsTrue()
        {
            // Arrange
            var taskId = "test-task-001";

            // Act
            var result = _service.CheckIterationLimit(taskId);

            // Assert
            Assert.IsTrue(result, "New task should be safe to continue");
        }

        [Test]
        public void IncrementIteration_IncrementsCount()
        {
            // Arrange
            var taskId = "test-task-002";

            // Act
            _service.IncrementIteration(taskId);
            var info = _service.GetIterationInfo(taskId);

            // Assert
            Assert.AreEqual(1, info.CurrentIteration);
            Assert.AreEqual(SafetyControlService.MaxIterationsPerTask, info.MaxIterations);
            Assert.AreEqual(SafetyControlService.MaxIterationsPerTask - 1, info.RemainingIterations);
        }

        [Test]
        public void CheckIterationLimit_AtLimit_ReturnsFalse()
        {
            // Arrange
            var taskId = "test-task-003";

            // Act - increment to limit
            for (int i = 0; i < SafetyControlService.MaxIterationsPerTask; i++)
            {
                _service.IncrementIteration(taskId);
            }

            var result = _service.CheckIterationLimit(taskId);

            // Assert
            Assert.IsFalse(result, "Task at iteration limit should not be safe to continue");
        }

        [Test]
        public void ResetIterations_ResetsCount()
        {
            // Arrange
            var taskId = "test-task-004";
            _service.IncrementIteration(taskId);
            _service.IncrementIteration(taskId);

            // Act
            _service.ResetIterations(taskId);
            var info = _service.GetIterationInfo(taskId);

            // Assert
            Assert.AreEqual(0, info.CurrentIteration);
        }

        #endregion

        #region Budget Tests

        [Test]
        public void CheckBudget_UnderLimit_ReturnsTrue()
        {
            // Arrange
            decimal costEstimate = 1.00m;

            // Act
            var result = _service.CheckBudget(costEstimate);

            // Assert
            Assert.IsTrue(result, "Cost under limit should be allowed");
        }

        [Test]
        public void RecordCost_UpdatesSpentAmount()
        {
            // Arrange
            decimal amount = 0.50m;

            // Act
            _service.RecordCost(amount, "Test API call");
            var budget = _service.GetBudgetStatus();

            // Assert
            Assert.AreEqual(amount, budget.SpentThisHour);
            Assert.AreEqual(SafetyControlService.MaxApiCostPerHour - amount, budget.RemainingBudget);
        }

        [Test]
        public void GetBudgetStatus_ReturnsValidInfo()
        {
            // Act
            var budget = _service.GetBudgetStatus();

            // Assert
            Assert.IsNotNull(budget);
            Assert.AreEqual(SafetyControlService.MaxApiCostPerHour, budget.HourlyLimit);
            Assert.GreaterOrEqual(budget.RemainingBudget, 0);
        }

        #endregion

        #region Agent Limit Tests

        [Test]
        public void CheckParallelAgents_UnderLimit_ReturnsTrue()
        {
            // Act
            var result = _service.CheckParallelAgents();

            // Assert
            Assert.IsTrue(result, "Should be able to spawn agents under limit");
        }

        [Test]
        public void RegisterAgent_IncrementsCount()
        {
            // Act
            _service.RegisterAgent("agent-001", "script");
            var count = _service.GetActiveAgentCount();

            // Assert
            Assert.AreEqual(1, count);
        }

        [Test]
        public void UnregisterAgent_DecrementsCount()
        {
            // Arrange
            _service.RegisterAgent("agent-001", "script");

            // Act
            _service.UnregisterAgent("agent-001");
            var count = _service.GetActiveAgentCount();

            // Assert
            Assert.AreEqual(0, count);
        }

        [Test]
        public void CheckParallelAgents_AtLimit_ReturnsFalse()
        {
            // Arrange - register max agents
            for (int i = 0; i < SafetyControlService.MaxParallelAgents; i++)
            {
                _service.RegisterAgent($"agent-{i}", "script");
            }

            // Act
            var result = _service.CheckParallelAgents();

            // Assert
            Assert.IsFalse(result, "Should not be able to spawn more agents at limit");

            // Cleanup
            for (int i = 0; i < SafetyControlService.MaxParallelAgents; i++)
            {
                _service.UnregisterAgent($"agent-{i}");
            }
        }

        #endregion

        #region Approval Request Tests

        [Test]
        public void RequestHumanApproval_CreatesRequest()
        {
            // Arrange
            var reason = "Test approval request";
            var context = new Dictionary<string, object>
            {
                { "task_id", "task-001" },
                { "details", "Some details" }
            };

            // Act
            var request = _service.RequestHumanApproval(reason, context);

            // Assert
            Assert.IsNotNull(request);
            Assert.IsNotNull(request.RequestId);
            Assert.AreEqual(reason, request.Reason);
            Assert.AreEqual("pending", request.Status);
        }

        [Test]
        public void GetApprovalStatus_ReturnsCorrectStatus()
        {
            // Arrange
            var request = _service.RequestHumanApproval("Test", null);

            // Act
            var status = _service.GetApprovalStatus(request.RequestId);

            // Assert
            Assert.IsNotNull(status);
            Assert.AreEqual(request.RequestId, status.RequestId);
            Assert.AreEqual("pending", status.Status);
            Assert.IsFalse(status.IsResolved);
        }

        [Test]
        public void ResolveApproval_Approved_UpdatesStatus()
        {
            // Arrange
            var request = _service.RequestHumanApproval("Test", null);

            // Act
            _service.ResolveApproval(request.RequestId, true, "Looks good");
            var status = _service.GetApprovalStatus(request.RequestId);

            // Assert
            Assert.AreEqual("approved", status.Status);
            Assert.IsTrue(status.IsApproved);
            Assert.IsTrue(status.IsResolved);
            Assert.AreEqual("Looks good", status.ReviewerNotes);
        }

        [Test]
        public void ResolveApproval_Rejected_UpdatesStatus()
        {
            // Arrange
            var request = _service.RequestHumanApproval("Test", null);

            // Act
            _service.ResolveApproval(request.RequestId, false, "Not approved");
            var status = _service.GetApprovalStatus(request.RequestId);

            // Assert
            Assert.AreEqual("rejected", status.Status);
            Assert.IsFalse(status.IsApproved);
            Assert.IsTrue(status.IsResolved);
        }

        [Test]
        public void ListPendingApprovals_ReturnsPendingOnly()
        {
            // Arrange
            var request1 = _service.RequestHumanApproval("Pending 1", null);
            var request2 = _service.RequestHumanApproval("Pending 2", null);
            _service.ResolveApproval(request2.RequestId, true, "Approved");

            // Act
            var pending = _service.ListPendingApprovals();

            // Assert
            Assert.AreEqual(1, pending.Count);
            Assert.AreEqual(request1.RequestId, pending[0].RequestId);
        }

        #endregion

        #region Interface Tests

        [Test]
        public void Service_ImplementsISafetyControl()
        {
            // Assert
            Assert.IsInstanceOf<ISafetyControl>(_service);
        }

        [Test]
        public void Constants_MatchMayorMdLimits()
        {
            // Assert - verify limits match mayor.md specification
            Assert.AreEqual(50, SafetyControlService.MaxIterationsPerTask, "Max iterations should be 50");
            Assert.AreEqual(10.00m, SafetyControlService.MaxApiCostPerHour, "Max API cost should be $10/hour");
            Assert.AreEqual(5, SafetyControlService.MaxParallelAgents, "Max parallel agents should be 5");
        }

        #endregion
    }
}
