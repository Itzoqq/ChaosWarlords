using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Managers
{
    [TestClass]
    [TestCategory("Unit")]
    public class GameEventLoggerTests
    {
        // Manual Mock to avoid NSubstitute generic ambiguity issues
        private class FakeEventManager : IEventManager
        {
            public List<(Type Type, object Handler)> Subscriptions { get; } = new();
            public List<(Type Type, object Handler)> Unsubscriptions { get; } = new();

            public void Publish(GameEvent gameEvent) { }

            public void Subscribe<T>(Action<T> handler) where T : GameEvent
            {
                Subscriptions.Add((typeof(T), handler));
            }

            public void Unsubscribe<T>(Action<T> handler) where T : GameEvent
            {
                Unsubscriptions.Add((typeof(T), handler));
            }

            // Helper to invoke a handler for testing
            public void InvokeHandler<T>(T evt) where T : GameEvent
            {
                foreach (var sub in Subscriptions.Where(s => s.Type == typeof(T)))
                {
                    ((Action<T>)sub.Handler)(evt);
                }
            }
        }

        [TestMethod]
        public void Initialize_SubscribesToStateChangeEvents()
        {
            // Arrange
            var fakeManager = new FakeEventManager();
            var logger = new GameEventLogger(fakeManager, Tests.Utilities.TestLogger.Instance);

            // Act
            logger.Initialize();

            // Assert
            var sub = fakeManager.Subscriptions.FirstOrDefault(s => s.Type == typeof(StateChangeEvent));
            Assert.IsNotNull(sub.Handler, "Should subscribe to StateChangeEvent");
        }

        [TestMethod]
        public void Initialize_SubscribesToGenericGameEvents()
        {
            // Arrange
            var fakeManager = new FakeEventManager();
            var logger = new GameEventLogger(fakeManager, Tests.Utilities.TestLogger.Instance);

            // Act
            logger.Initialize();

            // Assert
            var sub = fakeManager.Subscriptions.FirstOrDefault(s => s.Type == typeof(GameEvent));
            Assert.IsNotNull(sub.Handler, "Should subscribe to GameEvent");
        }

        [TestMethod]
        public void Cleanup_UnsubscribesFromStateChangeEvents()
        {
            // Arrange
            var fakeManager = new FakeEventManager();
            var logger = new GameEventLogger(fakeManager, Tests.Utilities.TestLogger.Instance);
            logger.Initialize();

            // Act
            logger.Cleanup();

            // Assert
            var unsub = fakeManager.Unsubscriptions.FirstOrDefault(s => s.Type == typeof(StateChangeEvent));
            Assert.IsNotNull(unsub.Handler, "Should unsubscribe from StateChangeEvent");
        }

        [TestMethod]
        public void Cleanup_UnsubscribesFromGenericGameEvents()
        {
            // Arrange
            var fakeManager = new FakeEventManager();
            var logger = new GameEventLogger(fakeManager, Tests.Utilities.TestLogger.Instance);
            logger.Initialize();

            // Act
            logger.Cleanup();

            // Assert
            var unsub = fakeManager.Unsubscriptions.FirstOrDefault(s => s.Type == typeof(GameEvent));
            Assert.IsNotNull(unsub.Handler, "Should unsubscribe from GameEvent");
        }

        [TestMethod]
        public void OnStateChanged_LogsStateNameAndOldToNewValue()
        {
            // Arrange
            var fakeManager = new FakeEventManager();
            var mockLogger = Substitute.For<IGameLogger>();
            var logger = new GameEventLogger(fakeManager, mockLogger);
            logger.Initialize();

            // Act - Invoke via our fake helper. StateChangeEvent's ctor is
            // (stateName, newValue, oldValue) - NOT (stateName, oldValue, newValue) - so this
            // means NewValue="10", OldValue="5".
            var evt = new StateChangeEvent("TestState", newValue: 10, oldValue: 5);

            // This effectively calls the private OnStateChanged method via the delegate
            fakeManager.InvokeHandler(evt);

            // Assert - IGameLogger is injectable (see constructor above), so we CAN verify
            // the actual logged content instead of just "didn't throw". Log format is
            // "{OldValue} -> {NewValue}", so this must read "5 -> 10", not "10 -> 5".
            mockLogger.Received(1).Log("[State] TestState: 5 -> 10", LogChannel.Info);
        }

        private record TestSimpleEvent : GameEvent { }

        [TestMethod]
        public void OnGenericEvent_WithNonStateChangeEvent_LogsContext()
        {
            // Arrange
            var fakeManager = new FakeEventManager();
            var mockLogger = Substitute.For<IGameLogger>();
            var logger = new GameEventLogger(fakeManager, mockLogger);
            logger.Initialize();

            var evt = new TestSimpleEvent { Context = "TestEvent" };

            // Act
            fakeManager.InvokeHandler<GameEvent>(evt);

            // Assert
            mockLogger.Received(1).Log("[Event] TestEvent", LogChannel.Debug);
        }

        [TestMethod]
        public void OnGenericEvent_WithStateChangeEvent_DoesNotDoubleLog()
        {
            // Regression test for GameEventLogger.OnGenericEvent's "if (evt is StateChangeEvent)
            // return;" filter (avoids double-logging, since StateChangeEvent inherits GameEvent
            // and both handlers subscribe to the shared event manager). The previous version of
            // this test only checked that invoking the handler didn't throw, which would still
            // pass even if that filter were deleted entirely - it never actually verified the
            // filter did anything.
            var fakeManager = new FakeEventManager();
            var mockLogger = Substitute.For<IGameLogger>();
            var logger = new GameEventLogger(fakeManager, mockLogger);
            logger.Initialize();

            var evt = new StateChangeEvent("TestState", 10, 5);

            // Act - Pass StateChangeEvent through the generic GameEvent handler directly
            // (mirrors what happens for real: IEventManager.Publish notifies every matching
            // subscriber, and StateChangeEvent matches both Subscribe<StateChangeEvent> and
            // Subscribe<GameEvent>).
            fakeManager.InvokeHandler<GameEvent>(evt);

            // Assert - OnGenericEvent's own filter must have suppressed this, not OnStateChanged
            // (which isn't even invoked here - only the generic handler is).
            mockLogger.DidNotReceive().Log(Arg.Any<string>(), Arg.Any<LogChannel>());
        }
    }
}
