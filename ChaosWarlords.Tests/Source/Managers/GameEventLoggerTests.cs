using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Managers;
using System.Collections.Generic;
using System.Linq;

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
            var logger = new GameEventLogger(fakeManager);
            
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
            var logger = new GameEventLogger(fakeManager);
            
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
            var logger = new GameEventLogger(fakeManager);
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
            var logger = new GameEventLogger(fakeManager);
            logger.Initialize();
            
            // Act
            logger.Cleanup();
            
            // Assert
            var unsub = fakeManager.Unsubscriptions.FirstOrDefault(s => s.Type == typeof(GameEvent));
            Assert.IsNotNull(unsub.Handler, "Should unsubscribe from GameEvent");
        }

        [TestMethod]
        public void OnStateChanged_CanBeInvokedWithoutException()
        {
            // Arrange
            var fakeManager = new FakeEventManager();
            var logger = new GameEventLogger(fakeManager);
            logger.Initialize();
            
            // Act - Invoke via our fake helper
            var evt = new StateChangeEvent("TestState", 10, 5);
            
            // This effectively calls the private OnStateChanged method via the delegate
            fakeManager.InvokeHandler(evt);
            
            // Assert - Log verification would require mocking dependency of GameLogger or capturing console/static interaction.
            // Since GameLogger is static, we assume if no exception was thrown, the logic executed.
            // In a better architecture, GameLogger would be an interface we could mock.
            // For now, testing that it runs without crashing is the baseline.
        }

        private record TestSimpleEvent : GameEvent { }

        [TestMethod]
        public void OnGenericEvent_CanBeInvokedWithNonStateChangeEvent()
        {
            // Arrange
            var fakeManager = new FakeEventManager();
            var logger = new GameEventLogger(fakeManager);
            logger.Initialize();
            
            var evt = new TestSimpleEvent { Context = "TestEvent" };
            
            // Act
            fakeManager.InvokeHandler<GameEvent>(evt);
            
            // Assert - No crash
        }

        [TestMethod]
        public void OnGenericEvent_WithStateChangeEvent_DoesNotThrow()
        {
            // Arrange
            var fakeManager = new FakeEventManager();
            var logger = new GameEventLogger(fakeManager);
            logger.Initialize();
            
            var evt = new StateChangeEvent("TestState", 10, 5);
            
            // Act
            // Pass StateChangeEvent as a generic GameEvent
            fakeManager.InvokeHandler<GameEvent>(evt);
            
            // Assert - No exception, logic inside OnGenericEvent filters it out
        }
    }
}
