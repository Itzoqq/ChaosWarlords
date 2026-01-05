using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities; // For MarketMode, LogChannel
using System;

namespace ChaosWarlords.Tests.Source.Unit.Managers
{
    [TestClass]
    public class MarketStateManagerTests
    {
        private MarketStateManager _manager = null!;
        private IGameLogger _mockLogger = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = Substitute.For<IGameLogger>();
            _manager = new MarketStateManager(_mockLogger);
        }

        [TestMethod]
        public void OpenForBrowsing_SetsModeAndClearsCallback()
        {
            // Arrange
            _manager.OpenForDevour((c) => { }); // Put in dirty state first

            // Act
            _manager.OpenForBrowsing();

            // Assert
            Assert.AreEqual(MarketMode.Browse, _manager.CurrentMode);
            Assert.IsTrue(_manager.IsOpen);
            Assert.IsNull(_manager.DevourCallback);
            _mockLogger.Received().Log(Arg.Is<string>(s => s.Contains("browsing")), LogChannel.Info);
        }

        [TestMethod]
        public void OpenForDevour_SetsModeAndCallback()
        {
            // Arrange
            Action<Card> callback = (c) => { };

            // Act
            _manager.OpenForDevour(callback);

            // Assert
            Assert.AreEqual(MarketMode.DevourTarget, _manager.CurrentMode);
            Assert.IsTrue(_manager.IsOpen);
            Assert.AreEqual(callback, _manager.DevourCallback);
            _mockLogger.Received().Log(Arg.Is<string>(s => s.Contains("devour targeting")), LogChannel.Info);
        }

        [TestMethod]
        public void OpenForDevour_NullCallback_Throws()
        {
             try
             {
                 _manager.OpenForDevour(null!);
                 Assert.Fail("Expected ArgumentNullException");
             }
             catch (ArgumentNullException)
             {
                 // Success
             }
        }

        [TestMethod]
        public void Close_SetsModeClosedAndClearsCallback()
        {
            // Arrange
            _manager.OpenForDevour((c) => { }); 

            // Act
            _manager.Close();

            // Assert
            Assert.AreEqual(MarketMode.Closed, _manager.CurrentMode);
            Assert.IsFalse(_manager.IsOpen);
            Assert.IsNull(_manager.DevourCallback);
            _mockLogger.Received().Log(Arg.Is<string>(s => s.Contains("Closing")), LogChannel.Info);
        }

        [TestMethod]
        public void Events_AreRaisedOnModeChange()
        {
            // Arrange
            bool eventRaised = false;
            MarketMode newMode = MarketMode.Closed;
            _manager.ModeChanged += (sender, mode) => { eventRaised = true; newMode = mode; };

            // Act
            _manager.OpenForBrowsing();

            // Assert
            Assert.IsTrue(eventRaised);
            Assert.AreEqual(MarketMode.Browse, newMode);
        }
    }
}
