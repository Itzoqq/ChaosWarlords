using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Core.Events;

namespace ChaosWarlords.Tests.Source.Core.Events
{
    [TestClass]
    public class EventManagerTests
    {
        private EventManager _eventManager = null!;
        private bool _eventReceived;
        private GameEvent? _receivedEvent;

        [TestInitialize]
        public void Setup()
        {
            _eventManager = new EventManager();
            _eventReceived = false;
            _receivedEvent = null;
        }

        [TestMethod]
        public void Publish_ShouldNotifySubscriber()
        {
            // Arrange
            _eventManager.Subscribe<TestEvent>(OnTestEvent);
            var testEvent = new TestEvent { Message = "Hello" };

            // Act
            _eventManager.Publish(testEvent);

            // Assert
            Assert.IsTrue(_eventReceived);
            Assert.IsInstanceOfType(_receivedEvent, typeof(TestEvent));
            Assert.AreEqual("Hello", ((TestEvent)_receivedEvent).Message);
        }

        [TestMethod]
        public void Unsubscribe_ShouldStopNotifications()
        {
            // Arrange
            _eventManager.Subscribe<TestEvent>(OnTestEvent);
            _eventManager.Unsubscribe<TestEvent>(OnTestEvent);

            // Act
            _eventManager.Publish(new TestEvent());

            // Assert
            Assert.IsFalse(_eventReceived);
        }

        [TestMethod]
        public void Publish_ShouldNotCrashOnHandlerException()
        {
            // Arrange
            _eventManager.Subscribe<TestEvent>(evt => throw new Exception("Handler Error"));

            // Act & Assert
            try
            {
                _eventManager.Publish(new TestEvent());
            }
            catch
            {
                Assert.Fail("EventManager should swallow handler exceptions.");
            }
        }

        private void OnTestEvent(TestEvent evt)
        {
            _eventReceived = true;
            _receivedEvent = evt;
        }

        private record TestEvent : GameEvent
        {
            public string? Message { get; init; }
        }
    }
}
