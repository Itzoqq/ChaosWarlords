using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Managers;
// using ChaosWarlords.Source.Core.Data.Recording;
using System.Collections.Generic;
using System;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Managers
{
    [TestClass]

    [TestCategory("Unit")]
    public class ReplayManagerTests
    {
        [TestMethod]
        public void RecordAndSerialize_ShouldVerifyRoundTrip()
        {
            // Arrange
            var manager = new ReplayManager(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            var player = new ChaosWarlords.Source.Entities.Actors.Player(ChaosWarlords.Source.Utilities.PlayerColor.Red);
            var card = new ChaosWarlords.Source.Entities.Cards.Card("test_id", "Test", 1, ChaosWarlords.Source.Utilities.CardAspect.Neutral, 0, 0, 0);
            player.Hand.Add(card);

            var command = new ChaosWarlords.Source.Commands.PlayCardCommand(card);

            // Act
            manager.RecordCommand(command, player, 1);
            string json = manager.GetRecordingJson();
            
            // Simulate new session
            var newManager = new ReplayManager(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            newManager.StartReplay(json);
            var serializedBack = newManager.GetRecordingJson();

            // Assert
            Assert.IsTrue(newManager.IsReplaying);
            Assert.AreEqual(json, serializedBack);
        }

        [TestMethod]
        public void StartReplay_WithInvalidJson_ShouldLogAndNotCrash()
        {
            // Arrange
            var manager = new ReplayManager(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            
            // Act
            manager.StartReplay("INVALID JSON");

            // Assert
            Assert.IsFalse(manager.IsReplaying);
        }
    }
}
