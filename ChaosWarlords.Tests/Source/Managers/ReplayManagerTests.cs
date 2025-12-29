using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Core.Data.Recording;
using System.Collections.Generic;
using System;

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
            var manager = new ReplayManager();
            var action = new ReplayAction
            {
                Sequence = 1,
                Timestamp = DateTime.Now,
                CommandType = "TestCommand",
                CommandJson = "{ \"foo\": \"bar\" }"
            };

            // Act
            manager.RecordAction(action);
            string json = manager.GetRecordingJson();
            
            // Simulate new session
            var newManager = new ReplayManager();
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
            var manager = new ReplayManager();
            
            // Act
            manager.StartReplay("INVALID JSON");

            // Assert
            Assert.IsFalse(manager.IsReplaying);
        }
    }
}
