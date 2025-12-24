using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class IntegrationTests
    {
        private TurnManager _turnManager;
        private MapManager _mapManager;
        private MatchContext _matchContext;
        private ActionSystem _actionSystem;
        private MatchController _matchController;

        private Player _player1;
        private Player _player2;
        private Site _siteA;

        [TestInitialize]
        public void Setup()
        {
            // 1. Setup Players
            _player1 = new Player(PlayerColor.Red);
            _player2 = new Player(PlayerColor.Blue);
            var players = new List<Player> { _player1, _player2 };

            // 2. Setup Map
            var node1 = new MapNode(1, Vector2.Zero);
            var node2 = new MapNode(2, Vector2.Zero);
            _siteA = new Site("TestCity", ResourceType.Power, 1, ResourceType.VictoryPoints, 2)
            {
                IsCity = true
            };
            _siteA.AddNode(node1);
            _siteA.AddNode(node2);

            _mapManager = new MapManager(new List<MapNode> { node1, node2 }, new List<Site> { _siteA });

            // 3. Setup Systems
            _turnManager = new TurnManager(players);
            _actionSystem = new ActionSystem(_turnManager, _mapManager);
            
            // Note: MarketManager not needed for this test, can be null
            _matchContext = new MatchContext(_turnManager, _mapManager, null, _actionSystem, null);
            _matchController = new MatchController(_matchContext);
        }

        [TestMethod]
        public void EndTurn_GrantsTotalControlRewards_WhenConditionsMet()
        {
            // Scenario: Player 1 has 2 troops in SiteA (Total Control), Player 2 has no spies.
            // Expected: Player 1 gains Control (1 Power) + Total Control (2 VP).

            // Arrange
            _player1.Power = 0;
            _player1.VictoryPoints = 0;

            // Fill nodes (Backdoor access via MapManager list for setup)
            _mapManager.NodesInternal[0].Occupant = _player1.Color;
            _mapManager.NodesInternal[1].Occupant = _player1.Color;

            // Ensure system knows the state defined above
            _mapManager.RecalculateSiteState(_siteA, _player1);

            // Verify Pre-Conditions
            Assert.AreEqual(_player1.Color, _siteA.Owner, "Player 1 should own the site.");
            Assert.IsTrue(_siteA.HasTotalControl, "Player 1 should have total control.");
            
            // Reset VPs to isolate the End-Turn reward (Establishment reward was given during Recalculate)
            _player1.VictoryPoints = 0;

            // Act
            _matchController.EndTurn();


            // Assert
            // Power is wiped at EndTurn, so we expect 0 (unless we move reward logic).
            // We focus on verifying Total Control (VP) which persists.
            Assert.AreEqual(2, _player1.VictoryPoints, "Should gain 2 VPs from Total Control.");

        }

        [TestMethod]
        public void EndTurn_DeniesTotalControlRewards_WhenEnemySpyPresent()
        {
            // Scenario: Player 1 occupies nodes, but Player 2 has a spy.
            
            // Arrange
            _mapManager.NodesInternal[0].Occupant = _player1.Color;
            _mapManager.NodesInternal[1].Occupant = _player1.Color;
            
            // Add Enemy Spy
            _siteA.Spies.Add(_player2.Color);

            _mapManager.RecalculateSiteState(_siteA, _player1);

            // Act
            _matchController.EndTurn();

            // Assert
            // Power wiped.
            Assert.AreEqual(0, _player1.VictoryPoints, "Should NOT gain Total Control reward due to spy.");

        }
    }
}
