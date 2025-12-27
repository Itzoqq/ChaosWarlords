using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using NSubstitute;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Tests.Managers
{
    [TestClass]
    public class TurnManagerTests
    {
        [TestMethod]
        public void Constructor_InitializesPlayersAndStartsFirstTurn()
        {
            // Arrange
            var players = new List<Player> 
            { 
                new Player(PlayerColor.Red), 
                new Player(PlayerColor.Blue) 
            };

            // Act
            var manager = new TurnManager(players);

            // Assert
            Assert.IsNotNull(manager.ActivePlayer);
            Assert.IsNotNull(manager.CurrentTurnContext);
            CollectionAssert.Contains(players, manager.ActivePlayer);
        }

        [TestMethod]
        public void Create_ThrowsException_IfPlayerListEmpty()
        {
            try
            {
                new TurnManager(new List<Player>());
                Assert.Fail("Expected ArgumentException was not thrown.");
            }
            catch (System.ArgumentException)
            {
                // Success
            }
        }

        [TestMethod]
        public void EndTurn_SwitchesActivePlayer()
        {
            // Arrange
            var p1 = new Player(PlayerColor.Red);
            var p2 = new Player(PlayerColor.Blue);
            var manager = new TurnManager(new List<Player> { p1, p2 });
            var firstPlayer = manager.ActivePlayer;

            // Act
            manager.EndTurn();

            // Assert
            Assert.AreNotEqual(firstPlayer, manager.ActivePlayer);
        }

        [TestMethod]
        public void EndTurn_FiresOnTurnChangedEvent()
        {
            // Arrange
            var p1 = new Player(PlayerColor.Red);
            var p2 = new Player(PlayerColor.Blue);
            var manager = new TurnManager(new List<Player> { p1, p2 });
            
            bool eventFired = false;
            Player? eventPlayer = null;
            
            manager.OnTurnChanged += (sender, player) => 
            {
                eventFired = true;
                eventPlayer = player;
            };

            // Act
            manager.EndTurn();

            // Assert
            Assert.IsTrue(eventFired, "OnTurnChanged should fire when turn ends.");
            Assert.AreEqual(manager.ActivePlayer, eventPlayer, "Event should pass the NEW active player.");
        }
    }
}
