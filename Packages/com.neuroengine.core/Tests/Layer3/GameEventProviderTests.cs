using System;
using System.Collections.Generic;
using NUnit.Framework;
using NeuroEngine.Core;
using UnityEngine;

namespace NeuroEngine.Tests.Layer3
{
    /// <summary>
    /// Tests for Layer 3: Game Event Provider Interface.
    /// Verifies event capture and game state snapshot structures.
    /// </summary>
    [TestFixture]
    public class GameEventProviderTests
    {
        #region GameEvent Structure

        [Test]
        public void GameEvent_Constructor_SetsBasicProperties()
        {
            var evt = new GameEvent("test_event", "Test prompt", 1);

            Assert.AreEqual("test_event", evt.Type);
            Assert.AreEqual("Test prompt", evt.Prompt);
            Assert.AreEqual(1, evt.Priority);
        }

        [Test]
        public void GameEvent_Constructor_SetsTimestamp()
        {
            var beforeCreate = DateTime.UtcNow;
            var evt = new GameEvent("test", "prompt");
            var afterCreate = DateTime.UtcNow;

            Assert.IsNotNull(evt.Timestamp);
            Assert.DoesNotThrow(() => DateTime.Parse(evt.Timestamp));

            var eventTime = DateTime.Parse(evt.Timestamp);
            Assert.GreaterOrEqual(eventTime, beforeCreate.AddSeconds(-1));
            Assert.LessOrEqual(eventTime, afterCreate.AddSeconds(1));
        }

        [Test]
        public void GameEvent_Constructor_InitializesContext()
        {
            var evt = new GameEvent("test", "prompt");

            Assert.IsNotNull(evt.Context);
            Assert.IsInstanceOf<Dictionary<string, object>>(evt.Context);
        }

        [Test]
        public void GameEvent_DefaultPriority_IsZero()
        {
            var evt = new GameEvent("test", "prompt");

            Assert.AreEqual(0, evt.Priority);
        }

        [Test]
        public void GameEvent_Context_CanStoreVariousTypes()
        {
            var evt = new GameEvent("test", "prompt");

            evt.Context["int_value"] = 42;
            evt.Context["float_value"] = 3.14f;
            evt.Context["string_value"] = "hello";
            evt.Context["bool_value"] = true;
            evt.Context["array_value"] = new int[] { 1, 2, 3 };

            Assert.AreEqual(42, evt.Context["int_value"]);
            Assert.AreEqual(3.14f, evt.Context["float_value"]);
            Assert.AreEqual("hello", evt.Context["string_value"]);
            Assert.AreEqual(true, evt.Context["bool_value"]);
            Assert.AreEqual(new int[] { 1, 2, 3 }, evt.Context["array_value"]);
        }

        [Test]
        public void GameEvent_SuggestedTool_CanBeSet()
        {
            var evt = new GameEvent("action_required", "Player needs help");
            evt.SuggestedTool = "interact_with_ui";

            Assert.AreEqual("interact_with_ui", evt.SuggestedTool);
        }

        [Test]
        public void GameEvent_Priority_Levels()
        {
            var infoEvent = new GameEvent("info", "Info message", 0);
            var suggestedEvent = new GameEvent("suggested", "Suggested action", 1);
            var requiredEvent = new GameEvent("required", "Required action", 2);

            Assert.AreEqual(0, infoEvent.Priority);
            Assert.AreEqual(1, suggestedEvent.Priority);
            Assert.AreEqual(2, requiredEvent.Priority);
        }

        #endregion

        #region GameStateSnapshot Structure

        [Test]
        public void GameStateSnapshot_Constructor_InitializesData()
        {
            var snapshot = new GameStateSnapshot();

            Assert.IsNotNull(snapshot.Data);
            Assert.IsInstanceOf<Dictionary<string, object>>(snapshot.Data);
        }

        [Test]
        public void GameStateSnapshot_Constructor_SetsTimestamp()
        {
            var beforeCreate = DateTime.UtcNow;
            var snapshot = new GameStateSnapshot();
            var afterCreate = DateTime.UtcNow;

            Assert.IsNotNull(snapshot.Timestamp);
            Assert.DoesNotThrow(() => DateTime.Parse(snapshot.Timestamp));

            var snapshotTime = DateTime.Parse(snapshot.Timestamp);
            Assert.GreaterOrEqual(snapshotTime, beforeCreate.AddSeconds(-1));
            Assert.LessOrEqual(snapshotTime, afterCreate.AddSeconds(1));
        }

        [Test]
        public void GameStateSnapshot_Phase_CanBeSet()
        {
            var snapshot = new GameStateSnapshot();
            snapshot.Phase = "MainMenu";

            Assert.AreEqual("MainMenu", snapshot.Phase);
        }

        [Test]
        public void GameStateSnapshot_IsActive_CanBeSet()
        {
            var snapshot = new GameStateSnapshot();
            snapshot.IsActive = true;

            Assert.IsTrue(snapshot.IsActive);

            snapshot.IsActive = false;
            Assert.IsFalse(snapshot.IsActive);
        }

        [Test]
        public void GameStateSnapshot_Data_CanStoreGameSpecificState()
        {
            var snapshot = new GameStateSnapshot();

            snapshot.Data["player_health"] = 100;
            snapshot.Data["player_score"] = 1500;
            snapshot.Data["current_level"] = "Level 3";
            snapshot.Data["enemies_remaining"] = 5;

            Assert.AreEqual(100, snapshot.Data["player_health"]);
            Assert.AreEqual(1500, snapshot.Data["player_score"]);
            Assert.AreEqual("Level 3", snapshot.Data["current_level"]);
            Assert.AreEqual(5, snapshot.Data["enemies_remaining"]);
        }

        [Test]
        public void GameStateSnapshot_Data_SupportsNestedStructures()
        {
            var snapshot = new GameStateSnapshot();

            var playerData = new Dictionary<string, object>
            {
                { "name", "Player1" },
                { "health", 80 },
                { "position", new float[] { 10.5f, 0, 20.3f } }
            };
            snapshot.Data["player"] = playerData;

            var retrievedPlayer = snapshot.Data["player"] as Dictionary<string, object>;
            Assert.IsNotNull(retrievedPlayer);
            Assert.AreEqual("Player1", retrievedPlayer["name"]);
        }

        #endregion

        #region Mock Implementation Tests

        /// <summary>
        /// A simple mock implementation to test the interface contract.
        /// </summary>
        private class MockGameEventProvider : IGameEventProvider
        {
            private readonly List<GameEvent> _events = new List<GameEvent>();
            private GameStateSnapshot _snapshot;

            public MockGameEventProvider()
            {
                _snapshot = new GameStateSnapshot
                {
                    Phase = "Testing",
                    IsActive = true
                };
            }

            public void QueueEvent(GameEvent evt)
            {
                _events.Add(evt);
            }

            public void SetSnapshot(GameStateSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public IReadOnlyList<GameEvent> GetEvents(bool clearAfterRead = true, string sinceTimestamp = null)
            {
                var result = new List<GameEvent>();

                foreach (var evt in _events)
                {
                    if (sinceTimestamp != null)
                    {
                        var since = DateTime.Parse(sinceTimestamp);
                        var eventTime = DateTime.Parse(evt.Timestamp);
                        if (eventTime <= since) continue;
                    }
                    result.Add(evt);
                }

                if (clearAfterRead)
                {
                    _events.Clear();
                }

                return result;
            }

            public GameStateSnapshot GetStateSnapshot()
            {
                return _snapshot;
            }
        }

        [Test]
        public void MockProvider_GetEvents_ReturnsQueuedEvents()
        {
            var provider = new MockGameEventProvider();
            provider.QueueEvent(new GameEvent("event1", "First event"));
            provider.QueueEvent(new GameEvent("event2", "Second event"));

            var events = provider.GetEvents(clearAfterRead: false);

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual("event1", events[0].Type);
            Assert.AreEqual("event2", events[1].Type);
        }

        [Test]
        public void MockProvider_GetEvents_ClearAfterRead()
        {
            var provider = new MockGameEventProvider();
            provider.QueueEvent(new GameEvent("event1", "Event"));

            var firstRead = provider.GetEvents(clearAfterRead: true);
            Assert.AreEqual(1, firstRead.Count);

            var secondRead = provider.GetEvents(clearAfterRead: false);
            Assert.AreEqual(0, secondRead.Count);
        }

        [Test]
        public void MockProvider_GetEvents_KeepsEventsWhenNotClearing()
        {
            var provider = new MockGameEventProvider();
            provider.QueueEvent(new GameEvent("event1", "Event"));

            var firstRead = provider.GetEvents(clearAfterRead: false);
            Assert.AreEqual(1, firstRead.Count);

            var secondRead = provider.GetEvents(clearAfterRead: false);
            Assert.AreEqual(1, secondRead.Count);
        }

        [Test]
        public void MockProvider_GetStateSnapshot_ReturnsSnapshot()
        {
            var provider = new MockGameEventProvider();

            var snapshot = provider.GetStateSnapshot();

            Assert.IsNotNull(snapshot);
            Assert.AreEqual("Testing", snapshot.Phase);
            Assert.IsTrue(snapshot.IsActive);
        }

        [Test]
        public void MockProvider_GetEvents_FiltersBySinceTimestamp()
        {
            var provider = new MockGameEventProvider();

            // Queue an old event (simulated by setting timestamp manually)
            var oldEvent = new GameEvent("old_event", "Old");

            // Wait a tiny bit then queue new event
            System.Threading.Thread.Sleep(10);
            var sinceTime = DateTime.UtcNow.ToString("o");

            System.Threading.Thread.Sleep(10);
            provider.QueueEvent(new GameEvent("new_event", "New"));

            var events = provider.GetEvents(clearAfterRead: false, sinceTimestamp: sinceTime);

            // Only the new event should be returned
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("new_event", events[0].Type);
        }

        #endregion

        #region InputSimulationResult Structure

        [Test]
        public void InputSimulationResult_Constructor_SetsProperties()
        {
            var result = new InputSimulationResult(true, "KeyPress", "Pressed Space");

            Assert.IsTrue(result.Success);
            Assert.AreEqual("KeyPress", result.Action);
            Assert.AreEqual("Pressed Space", result.Details);
        }

        [Test]
        public void InputSimulationResult_Constructor_SetsTimestamp()
        {
            var beforeCreate = DateTime.UtcNow;
            var result = new InputSimulationResult(true, "Test");
            var afterCreate = DateTime.UtcNow;

            Assert.IsNotNull(result.Timestamp);
            Assert.DoesNotThrow(() => DateTime.Parse(result.Timestamp));

            var resultTime = DateTime.Parse(result.Timestamp);
            Assert.GreaterOrEqual(resultTime, beforeCreate.AddSeconds(-1));
            Assert.LessOrEqual(resultTime, afterCreate.AddSeconds(1));
        }

        [Test]
        public void InputSimulationResult_FailedResult()
        {
            var result = new InputSimulationResult(false, "MouseClick", "Element not found");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("MouseClick", result.Action);
            Assert.AreEqual("Element not found", result.Details);
        }

        [Test]
        public void InputSimulationResult_NullDetails()
        {
            var result = new InputSimulationResult(true, "SimpleAction");

            Assert.IsNull(result.Details);
        }

        #endregion
    }
}
