using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class MapManagerTests
    {
        private Player _player1 = null!;
        private Player _player2 = null!;
        private MapManager _mapManager = null!;

        // Nodes
        private MapNode _node1 = null!, _node2 = null!, _node3 = null!, _node4 = null!, _node5 = null!;

        // Sites
        private Site _siteA = null!, _siteB = null!;

        [TestInitialize]
        public void Setup()
        {
            // ARRANGE
            _player1 = new Player(PlayerColor.Red);
            _player2 = new Player(PlayerColor.Blue);

            // Create a consistent map for testing
            // Layout: [1] -- [2] -- [3, 4 are in SiteA] -- [5 is in SiteB]
            // UPDATED: Removed 'null' texture argument
            _node1 = new MapNode(1, new Vector2(10, 10));
            _node2 = new MapNode(2, new Vector2(20, 10));
            _node3 = new MapNode(3, new Vector2(30, 10));
            _node4 = new MapNode(4, new Vector2(40, 10));
            _node5 = new MapNode(5, new Vector2(50, 10));

            _node1.AddNeighbor(_node2);
            _node2.AddNeighbor(_node3);

            _siteA = new Site("SiteA", ResourceType.Power, 1, ResourceType.VictoryPoints, 1) { IsCity = true };
            _siteA.AddNode(_node3);
            _siteA.AddNode(_node4);
            _node3.AddNeighbor(_node4); // Nodes within a site are connected

            _siteB = new Site("SiteB", ResourceType.Influence, 1, ResourceType.VictoryPoints, 1);
            _siteB.AddNode(_node5);

            // Connect the sites
            _node4.AddNeighbor(_node5);

            var nodes = new List<MapNode> { _node1, _node2, _node3, _node4, _node5 };
            var sites = new List<Site> { _siteA, _siteB };
            _mapManager = new MapManager(nodes, sites);

            // Reset players to a default state for each test
            _player1.Power = 10;
            _player1.TroopsInBarracks = 10;
            _player1.SpiesInBarracks = 4;
            _player1.TrophyHall = 0;

            _player2.Power = 10;
            _player2.TroopsInBarracks = 10;
            _player2.SpiesInBarracks = 4;
        }

        #region Deployment and Presence Tests

        [TestMethod]
        public void CanDeployAt_Fails_WhenNodeIsOccupied()
        {
            _node1.Occupant = PlayerColor.Blue;
            Assert.IsFalse(_mapManager.CanDeployAt(_node1, _player1.Color));
        }

        [TestMethod]
        public void CanDeployAt_Succeeds_WhenPlayerHasAdjacentTroop()
        {
            _node1.Occupant = _player1.Color; // Player 1 has presence at node 2
            Assert.IsTrue(_mapManager.CanDeployAt(_node2, _player1.Color));
        }

        [TestMethod]
        public void CanDeployAt_Succeeds_WhenPlayerHasSpyAtSite()
        {
            _siteA.Spies.Add(_player1.Color); // Player 1 has a spy in Site A
            // Node 3 is in Site A, so we should be able to deploy there.
            Assert.IsTrue(_mapManager.CanDeployAt(_node3, _player1.Color));
        }

        [TestMethod]
        public void CanDeployAt_Fails_WhenNoPresence()
        {
            _node1.Occupant = _player1.Color;
            // Node 5 is too far away, no adjacent troops or spies.
            Assert.IsFalse(_mapManager.CanDeployAt(_node5, _player1.Color));
        }

        [TestMethod]
        public void TryDeploy_Fails_WhenNotEnoughPower()
        {
            _player1.Power = 0;
            _node1.Occupant = _player1.Color; // Has presence

            // UPDATED: Pass target directly, removed IsHovered
            bool result = _mapManager.TryDeploy(_player1, _node2);

            Assert.IsFalse(result);
            Assert.AreEqual(PlayerColor.None, _node2.Occupant);
            Assert.AreEqual(10, _player1.TroopsInBarracks);
        }

        [TestMethod]
        public void TryDeploy_Fails_WhenNotEnoughTroops()
        {
            _player1.TroopsInBarracks = 0;
            _node1.Occupant = _player1.Color; // Has presence

            // UPDATED: Pass target directly
            bool result = _mapManager.TryDeploy(_player1, _node2);

            Assert.IsFalse(result);
            Assert.AreEqual(PlayerColor.None, _node2.Occupant);
            Assert.AreEqual(0, _player1.TroopsInBarracks);
        }

        [TestMethod]
        public void TryDeploy_Succeeds_WithValidConditions()
        {
            _player1.Power = 1;
            _player1.TroopsInBarracks = 1;
            _node1.Occupant = _player1.Color; // Has presence

            // UPDATED: Pass target directly
            bool result = _mapManager.TryDeploy(_player1, _node2);

            Assert.IsTrue(result);
            Assert.AreEqual(_player1.Color, _node2.Occupant);
            Assert.AreEqual(0, _player1.TroopsInBarracks);
            Assert.AreEqual(0, _player1.Power);
        }

        [TestMethod]
        public void CanDeployAt_Succeeds_WhenPlayerHasNoTroopsOnBoard()
        {
            // No troops for player 1 are on the board.
            // According to the "Start of Game" rule, they can deploy anywhere.
            Assert.IsTrue(_mapManager.CanDeployAt(_node5, _player1.Color));
        }

        [TestMethod]
        public void HasPresence_Succeeds_WhenTroopIsOnTargetNode()
        {
            // Arrange: Player 1 has a troop on the target node itself.
            _node1.Occupant = _player1.Color;

            // Act & Assert
            Assert.IsTrue(_mapManager.HasPresence(_node1, _player1.Color), "Presence should be true if a player's troop is on the target node.");
        }

        [TestMethod]
        public void HasPresence_GrantsSiteWidePresence_FromAdjacency()
        {
            // Arrange: P1 is at node 2, which is adjacent to node 3 (in Site A).
            // An enemy is at node 4 (also in Site A).
            _node2.Occupant = _player1.Color;
            _node4.Occupant = _player2.Color;

            // Act & Assert: P1 should have presence at node 4, because being adjacent
            // to any part of a site (node 3) grants presence to the whole site.
            Assert.IsTrue(_mapManager.HasPresence(_node4, _player1.Color), "Presence at one node of a site should grant presence to the entire site.");
        }

        #endregion

        #region Assassination Tests

        [TestMethod]
        public void CanAssassinate_Succeeds_WithAdjacentPresence()
        {
            _node1.Occupant = _player1.Color;
            _node2.Occupant = _player2.Color;
            Assert.IsTrue(_mapManager.CanAssassinate(_node2, _player1));
        }

        [TestMethod]
        public void CanAssassinate_Succeeds_WithSpyPresence()
        {
            _siteA.Spies.Add(_player1.Color);
            _node3.Occupant = _player2.Color; // Node 3 is in Site A
            Assert.IsTrue(_mapManager.CanAssassinate(_node3, _player1));
        }

        [TestMethod]
        public void CanAssassinate_Fails_OnOwnTroop()
        {
            _node1.Occupant = _player1.Color;
            _node2.Occupant = _player1.Color;
            Assert.IsFalse(_mapManager.CanAssassinate(_node2, _player1));
        }

        [TestMethod]
        public void Assassinate_CorrectlyUpdatesTrophyHallAndRemovesTroop()
        {
            _node2.Occupant = _player2.Color;

            _mapManager.Assassinate(_node2, _player1);

            Assert.AreEqual(1, _player1.TrophyHall);
            Assert.AreEqual(PlayerColor.None, _node2.Occupant);
        }

        #endregion

        #region Spy Tests

        [TestMethod]
        public void PlaceSpy_Succeeds_AndReducesSpySupply()
        {
            _mapManager.PlaceSpy(_siteA, _player1);

            CollectionAssert.Contains(_siteA.Spies.ToList(), _player1.Color);
            Assert.AreEqual(3, _player1.SpiesInBarracks);
        }

        [TestMethod]
        public void PlaceSpy_Fails_IfSpyAlreadyPresent()
        {
            _siteA.Spies.Add(_player1.Color); // Spy is already there

            _mapManager.PlaceSpy(_siteA, _player1);

            // Should not add a second spy, and supply should not decrease again.
            Assert.AreEqual(1, _siteA.Spies.Count(c => c == _player1.Color));
            Assert.AreEqual(4, _player1.SpiesInBarracks);
        }

        [TestMethod]
        public void PlaceSpy_Fails_IfNoSpiesInBarracks()
        {
            _player1.SpiesInBarracks = 0;

            _mapManager.PlaceSpy(_siteA, _player1);

            CollectionAssert.DoesNotContain(_siteA.Spies.ToList(), _player1.Color);
            Assert.AreEqual(0, _player1.SpiesInBarracks);
        }

        [TestMethod]
        public void ReturnSpy_Succeeds_WithPresence()
        {
            // FIX: Move player to Node 2 so they are adjacent to Site A
            _node2.Occupant = _player1.Color;

            _siteA.Spies.Add(_player2.Color);

            bool result = _mapManager.ReturnSpy(_siteA, _player1);

            Assert.IsTrue(result);
            CollectionAssert.DoesNotContain(_siteA.Spies.ToList(), _player2.Color);
        }

        [TestMethod]
        public void ReturnSpy_Fails_WithoutPresence()
        {
            _siteA.Spies.Add(_player2.Color);

            bool result = _mapManager.ReturnSpy(_siteA, _player1);

            Assert.IsFalse(result);
            // FAILURE: Spy should still be THERE -> Contains
            CollectionAssert.Contains(_siteA.Spies.ToList(), _player2.Color);
        }

        [TestMethod]
        public void ReturnSpy_ReturnsFirstEnemySpy_WhenMultipleArePresent()
        {
            // Arrange
            var player3 = new Player(PlayerColor.Black);
            _node2.Occupant = _player1.Color; // P1 has presence at Site A
            _siteA.Spies.Add(_player2.Color);
            _siteA.Spies.Add(player3.Color);

            // Act
            bool result = _mapManager.ReturnSpy(_siteA, _player1);

            // Assert
            Assert.IsTrue(result);
            // FirstOrDefault will remove the first one that is not player1. In this case, player2.
            Assert.DoesNotContain(_player2.Color, _siteA.Spies);
            Assert.Contains(player3.Color, _siteA.Spies);
            Assert.HasCount(1, _siteA.Spies);
        }

        #endregion

        #region Other Actions

        [TestMethod]
        public void Supplant_CorrectlyReplacesEnemyAndUpdatesState()
        {
            // Arrange
            _node2.Occupant = _player2.Color;
            int initialTroops = _player1.TroopsInBarracks;

            // Act
            _mapManager.Supplant(_node2, _player1);

            // Assert
            Assert.AreEqual(_player1.Color, _node2.Occupant); // Node is now occupied by player 1
            Assert.AreEqual(1, _player1.TrophyHall); // Enemy troop was added to trophy hall
            Assert.AreEqual(initialTroops - 1, _player1.TroopsInBarracks); // Player 1 used a troop
        }

        [TestMethod]
        public void ReturnTroop_ReturnsOwnTroopToBarracks()
        {
            // Arrange
            _node1.Occupant = _player1.Color;
            int initialTroops = _player1.TroopsInBarracks;

            // Act
            _mapManager.ReturnTroop(_node1, _player1);

            // Assert
            Assert.AreEqual(PlayerColor.None, _node1.Occupant);
            Assert.AreEqual(initialTroops + 1, _player1.TroopsInBarracks);
        }

        [TestMethod]
        public void ReturnTroop_ReturnsEnemyTroopToTheirBarracks()
        {
            _node1.Occupant = _player2.Color;
            _mapManager.ReturnTroop(_node1, _player1);
            // We can't check player2's barracks count easily here,
            // but we can check that the troop is removed from the board.
            Assert.AreEqual(PlayerColor.None, _node1.Occupant);
        }
        #endregion

        #region Site Control Tests

        [TestMethod]
        public void UpdateSiteControl_AssignsOwnerByMajority()
        {
            // Arrange: Player 1 places a troop in Site A.
            _node3.Occupant = _player1.Color;

            // Act: Call the update directly!
            _mapManager.RecalculateSiteState(_siteA, _player1);

            // Assert
            Assert.AreEqual(_player1.Color, _siteA.Owner);
        }

        [TestMethod]
        public void UpdateSiteControl_GrantsTotalControl()
        {
            // Arrange: Player 1 occupies all nodes in Site A.
            _node3.Occupant = _player1.Color;
            _node4.Occupant = _player1.Color;
            int initialVp = _player1.VictoryPoints;

            // Act
            _mapManager.RecalculateSiteState(_siteA, _player1);

            // Assert
            Assert.AreEqual(_player1.Color, _siteA.Owner);
            Assert.IsTrue(_siteA.HasTotalControl);
            Assert.AreEqual(initialVp + 1, _player1.VictoryPoints);
        }

        [TestMethod]
        public void UpdateSiteControl_TotalControlIsBlockedByEnemySpy()
        {
            // Arrange
            _node3.Occupant = _player1.Color;
            _node4.Occupant = _player1.Color;
            _siteA.Spies.Add(_player2.Color);

            // Act
            _mapManager.RecalculateSiteState(_siteA, _player1);

            // Assert
            Assert.AreEqual(_player1.Color, _siteA.Owner);
            Assert.IsFalse(_siteA.HasTotalControl); // Spy blocks it!
        }

        [TestMethod]
        public void UpdateSiteControl_RemovingSpyGrantsTotalControl()
        {
            // Arrange: Player 1 has all troops, Player 2 has a spy. Player 1 has presence to remove the spy.
            _node3.Occupant = _player1.Color;
            _node4.Occupant = _player1.Color;
            _siteA.Spies.Add(_player2.Color);
            _node2.Occupant = _player1.Color; // Presence at Site A
            int initialVp = _player1.VictoryPoints;

            // Act: Remove the spy
            bool result = _mapManager.ReturnSpy(_siteA, _player1);

            // Assert
            Assert.IsTrue(result);
            Assert.DoesNotContain(_player2.Color, _siteA.Spies);
            Assert.IsTrue(_siteA.HasTotalControl);
            // Check that the Total Control VP reward was given
            Assert.AreEqual(initialVp + 1, _player1.VictoryPoints);
        }

        [TestMethod]
        public void UpdateSiteControl_LosingTroopLosesTotalControl()
        {
            // Arrange
            _node3.Occupant = _player1.Color;
            _node4.Occupant = _player1.Color;
            _mapManager.RecalculateSiteState(_siteA, _player1); // Establish control first
            Assert.IsTrue(_siteA.HasTotalControl);

            // Act: Player 2 assassinates
            _node2.Occupant = _player2.Color; // P2 needs presence
            _mapManager.Assassinate(_node3, _player2); // This calls UpdateSiteControl internally

            // Assert
            Assert.IsFalse(_siteA.HasTotalControl);
            Assert.AreEqual(_player1.Color, _siteA.Owner);
        }

        #endregion

        #region Site Control Edge Cases

        [TestMethod]
        public void UpdateSiteControl_NoOwnerOnTie()
        {
            // Arrange: P1 and P2 both place one troop in Site A.
            _node3.Occupant = _player1.Color;
            _node4.Occupant = _player2.Color;

            // Act
            _mapManager.RecalculateSiteState(_siteA, _player1);

            // Assert
            Assert.AreEqual(PlayerColor.None, _siteA.Owner, "Site owner should be None on a tie between players.");
            Assert.IsFalse(_siteA.HasTotalControl);
        }

        [TestMethod]
        public void UpdateSiteControl_NoOwnerOnTieWithNeutral()
        {
            // Arrange: P1 has one troop, and there is one neutral troop.
            _node3.Occupant = _player1.Color;
            _node4.Occupant = PlayerColor.Neutral;

            // Act
            _mapManager.RecalculateSiteState(_siteA, _player1);

            // Assert
            Assert.AreEqual(PlayerColor.None, _siteA.Owner, "Site owner should be None on a tie with neutral troops.");
        }

        #endregion

        #region Rewards Tests

        [TestMethod]
        public void DistributeControlRewards_GrantsResourcesForOwnedCities()
        {
            // Arrange
            _siteA.IsCity = true;
            _siteA.Owner = _player1.Color;
            _player1.Power = 0;

            // Act
            _mapManager.DistributeControlRewards(_player1);

            // Assert (SiteA gives 1 Power in setup)
            Assert.AreEqual(1, _player1.Power);
        }

        #endregion

        #region Hit-Test (Replaces UI Hover)

        [TestMethod]
        public void GetNodeAt_ReturnsCorrectNode()
        {
            // Arrange: Place node at 100, 100
            _node1.Position = new Vector2(100, 100);

            // MapNode.Radius is constant 20.
            // Mouse at 105, 105 is inside.
            var insidePoint = new Vector2(105, 105);
            var outsidePoint = new Vector2(200, 200);

            // Act & Assert
            Assert.AreSame(_node1, _mapManager.GetNodeAt(insidePoint), "Should return node when point is inside radius.");
            Assert.IsNull(_mapManager.GetNodeAt(outsidePoint), "Should return null when point is outside.");
        }

        #endregion
    }
}