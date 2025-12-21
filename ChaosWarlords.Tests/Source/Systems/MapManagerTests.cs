using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;

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
            _node1 = new MapNode(1, new Vector2(10, 10));
            _node2 = new MapNode(2, new Vector2(20, 10));
            _node3 = new MapNode(3, new Vector2(30, 10));
            _node4 = new MapNode(4, new Vector2(40, 10));
            _node5 = new MapNode(5, new Vector2(50, 10));

            _node1.AddNeighbor(_node2);
            _node2.AddNeighbor(_node1);
            _node2.AddNeighbor(_node3);
            _node3.AddNeighbor(_node2);

            _siteA = new Site("SiteA", ResourceType.Power, 1, ResourceType.VictoryPoints, 1) { IsCity = true };
            _siteA.AddNode(_node3);
            _siteA.AddNode(_node4);
            _node3.AddNeighbor(_node4);
            _node4.AddNeighbor(_node3);

            _siteB = new Site("SiteB", ResourceType.Influence, 1, ResourceType.VictoryPoints, 1);
            _siteB.AddNode(_node5);

            // Connect the sites
            _node4.AddNeighbor(_node5);
            _node5.AddNeighbor(_node4);

            var nodes = new List<MapNode> { _node1, _node2, _node3, _node4, _node5 };
            var sites = new List<Site> { _siteA, _siteB };
            _mapManager = new MapManager(nodes, sites);

            // Reset players
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
            _node1.Occupant = _player1.Color;
            Assert.IsTrue(_mapManager.CanDeployAt(_node2, _player1.Color));
        }

        [TestMethod]
        public void CanDeployAt_Succeeds_WhenPlayerHasSpyAtSite()
        {
            _siteA.Spies.Add(_player1.Color);
            Assert.IsTrue(_mapManager.CanDeployAt(_node3, _player1.Color));
        }

        [TestMethod]
        public void CanDeployAt_Fails_WhenNoPresence()
        {
            _node1.Occupant = _player1.Color;
            Assert.IsFalse(_mapManager.CanDeployAt(_node5, _player1.Color));
        }

        [TestMethod]
        public void TryDeploy_Fails_WhenNotEnoughPower()
        {
            _player1.Power = 0;
            _node1.Occupant = _player1.Color;

            bool result = _mapManager.TryDeploy(_player1, _node2);

            Assert.IsFalse(result);
            Assert.AreEqual(PlayerColor.None, _node2.Occupant);
            Assert.AreEqual(10, _player1.TroopsInBarracks);
        }

        [TestMethod]
        public void TryDeploy_Fails_WhenNotEnoughTroops()
        {
            _player1.TroopsInBarracks = 0;
            _node1.Occupant = _player1.Color;

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
            _node1.Occupant = _player1.Color;

            bool result = _mapManager.TryDeploy(_player1, _node2);

            Assert.IsTrue(result);
            Assert.AreEqual(_player1.Color, _node2.Occupant);
            Assert.AreEqual(0, _player1.TroopsInBarracks);
            Assert.AreEqual(0, _player1.Power);
        }

        [TestMethod]
        public void CanDeployAt_Succeeds_WhenPlayerHasNoTroopsOnBoard()
        {
            Assert.IsTrue(_mapManager.CanDeployAt(_node5, _player1.Color));
        }

        [TestMethod]
        public void HasPresence_Succeeds_WhenTroopIsOnTargetNode()
        {
            _node1.Occupant = _player1.Color;
            Assert.IsTrue(_mapManager.HasPresence(_node1, _player1.Color));
        }

        [TestMethod]
        public void HasPresence_GrantsSiteWidePresence_FromAdjacency()
        {
            _node2.Occupant = _player1.Color;
            _node4.Occupant = _player2.Color;
            Assert.IsTrue(_mapManager.HasPresence(_node4, _player1.Color));
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
            _node3.Occupant = _player2.Color;
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
            _siteA.Spies.Add(_player1.Color);
            _mapManager.PlaceSpy(_siteA, _player1);
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
            // Move player to Node 2 so they are adjacent to Site A
            _node2.Occupant = _player1.Color;
            _siteA.Spies.Add(_player2.Color);

            // Explicitly target player 2's spy
            bool result = _mapManager.ReturnSpecificSpy(_siteA, _player1, _player2.Color);

            Assert.IsTrue(result);
            CollectionAssert.DoesNotContain(_siteA.Spies.ToList(), _player2.Color);
        }

        [TestMethod]
        public void ReturnSpy_Fails_WithoutPresence()
        {
            _siteA.Spies.Add(_player2.Color);

            // Explicitly target player 2's spy
            bool result = _mapManager.ReturnSpecificSpy(_siteA, _player1, _player2.Color);

            Assert.IsFalse(result);
            CollectionAssert.Contains(_siteA.Spies.ToList(), _player2.Color);
        }

        [TestMethod]
        public void ReturnSpy_ReturnsTargetedSpy_WhenMultipleArePresent()
        {
            // Arrange
            var player3 = new Player(PlayerColor.Black);
            _node2.Occupant = _player1.Color; // P1 has presence
            _siteA.Spies.Add(_player2.Color);
            _siteA.Spies.Add(player3.Color);

            // Act: Explicitly target Player 3's spy
            bool result = _mapManager.ReturnSpecificSpy(_siteA, _player1, player3.Color);

            // Assert
            Assert.IsTrue(result);
            Assert.DoesNotContain(player3.Color, _siteA.Spies); // P3 spy removed
            Assert.Contains(_player2.Color, _siteA.Spies); // P2 spy remains
        }

        #endregion

        #region Other Actions

        [TestMethod]
        public void Supplant_CorrectlyReplacesEnemyAndUpdatesState()
        {
            _node2.Occupant = _player2.Color;
            int initialTroops = _player1.TroopsInBarracks;
            _mapManager.Supplant(_node2, _player1);
            Assert.AreEqual(_player1.Color, _node2.Occupant);
            Assert.AreEqual(1, _player1.TrophyHall);
            Assert.AreEqual(initialTroops - 1, _player1.TroopsInBarracks);
        }

        [TestMethod]
        public void ReturnTroop_ReturnsOwnTroopToBarracks()
        {
            _node1.Occupant = _player1.Color;
            int initialTroops = _player1.TroopsInBarracks;
            _mapManager.ReturnTroop(_node1, _player1);
            Assert.AreEqual(PlayerColor.None, _node1.Occupant);
            Assert.AreEqual(initialTroops + 1, _player1.TroopsInBarracks);
        }

        [TestMethod]
        public void ReturnTroop_ReturnsEnemyTroopToTheirBarracks()
        {
            _node1.Occupant = _player2.Color;
            _mapManager.ReturnTroop(_node1, _player1);
            Assert.AreEqual(PlayerColor.None, _node1.Occupant);
        }
        #endregion

        #region Site Control Tests

        [TestMethod]
        public void UpdateSiteControl_AssignsOwnerByMajority()
        {
            _node3.Occupant = _player1.Color;
            _mapManager.RecalculateSiteState(_siteA, _player1);
            Assert.AreEqual(_player1.Color, _siteA.Owner);
        }

        [TestMethod]
        public void UpdateSiteControl_GrantsTotalControl()
        {
            _node3.Occupant = _player1.Color;
            _node4.Occupant = _player1.Color;
            int initialVp = _player1.VictoryPoints;
            _mapManager.RecalculateSiteState(_siteA, _player1);
            Assert.AreEqual(_player1.Color, _siteA.Owner);
            Assert.IsTrue(_siteA.HasTotalControl);
            Assert.AreEqual(initialVp + 1, _player1.VictoryPoints);
        }

        [TestMethod]
        public void UpdateSiteControl_TotalControlIsBlockedByEnemySpy()
        {
            _node3.Occupant = _player1.Color;
            _node4.Occupant = _player1.Color;
            _siteA.Spies.Add(_player2.Color);
            _mapManager.RecalculateSiteState(_siteA, _player1);
            Assert.AreEqual(_player1.Color, _siteA.Owner);
            Assert.IsFalse(_siteA.HasTotalControl);
        }

        [TestMethod]
        public void UpdateSiteControl_RemovingSpyGrantsTotalControl()
        {
            // Arrange
            _node3.Occupant = _player1.Color;
            _node4.Occupant = _player1.Color;
            _siteA.Spies.Add(_player2.Color);
            _node2.Occupant = _player1.Color; // Presence at Site A
            int initialVp = _player1.VictoryPoints;

            // Act: Remove the spy (Explicitly targeting P2)
            bool result = _mapManager.ReturnSpecificSpy(_siteA, _player1, _player2.Color);

            // Assert
            Assert.IsTrue(result);
            Assert.DoesNotContain(_player2.Color, _siteA.Spies);
            Assert.IsTrue(_siteA.HasTotalControl);
            Assert.AreEqual(initialVp + 1, _player1.VictoryPoints);
        }

        [TestMethod]
        public void UpdateSiteControl_LosingTroopLosesTotalControl()
        {
            _node3.Occupant = _player1.Color;
            _node4.Occupant = _player1.Color;
            _mapManager.RecalculateSiteState(_siteA, _player1);
            Assert.IsTrue(_siteA.HasTotalControl);

            _node2.Occupant = _player2.Color; // P2 needs presence
            _mapManager.Assassinate(_node3, _player2);

            Assert.IsFalse(_siteA.HasTotalControl);
            Assert.AreEqual(_player1.Color, _siteA.Owner);
        }

        #endregion

        #region Site Control Edge Cases

        [TestMethod]
        public void UpdateSiteControl_NoOwnerOnTie()
        {
            _node3.Occupant = _player1.Color;
            _node4.Occupant = _player2.Color;
            _mapManager.RecalculateSiteState(_siteA, _player1);
            Assert.AreEqual(PlayerColor.None, _siteA.Owner);
            Assert.IsFalse(_siteA.HasTotalControl);
        }

        [TestMethod]
        public void UpdateSiteControl_NoOwnerOnTieWithNeutral()
        {
            _node3.Occupant = _player1.Color;
            _node4.Occupant = PlayerColor.Neutral;
            _mapManager.RecalculateSiteState(_siteA, _player1);
            Assert.AreEqual(PlayerColor.None, _siteA.Owner);
        }

        #endregion

        #region Rewards Tests

        [TestMethod]
        public void DistributeControlRewards_GrantsResourcesForOwnedCities()
        {
            _siteA.IsCity = true;
            _siteA.Owner = _player1.Color;
            _player1.Power = 0;
            _mapManager.DistributeControlRewards(_player1);
            Assert.AreEqual(1, _player1.Power);
        }

        #endregion

        #region Hit-Test (Replaces UI Hover)

        [TestMethod]
        public void GetNodeAt_ReturnsCorrectNode()
        {
            _node1.Position = new Vector2(100, 100);
            var insidePoint = new Vector2(105, 105);
            var outsidePoint = new Vector2(200, 200);

            Assert.AreSame(_node1, _mapManager.GetNodeAt(insidePoint));
            Assert.IsNull(_mapManager.GetNodeAt(outsidePoint));
        }

        #endregion

        #region Move Troop Tests

        [TestMethod]
        public void CanMoveSource_ReturnsTrue_ForEnemyWithPresence()
        {
            // Arrange: Player 1 has presence at Node 1 (via Node 2), Node 1 has Enemy
            _node2.Occupant = _player1.Color;
            _node1.Occupant = _player2.Color;

            // Act & Assert
            Assert.IsTrue(_mapManager.CanMoveSource(_node1, _player1));
        }

        [TestMethod]
        public void CanMoveSource_ReturnsTrue_ForNeutralTroop()
        {
            // Arrange: Player 1 has presence at Node 1
            _node2.Occupant = _player1.Color;
            _node1.Occupant = PlayerColor.Neutral;

            // Act & Assert
            Assert.IsTrue(_mapManager.CanMoveSource(_node1, _player1));
        }

        [TestMethod]
        public void CanMoveSource_ReturnsFalse_ForOwnTroop()
        {
            // Arrange: Own troops cannot be moved by "Move Enemy" effects
            _node1.Occupant = _player1.Color;

            // Act & Assert
            Assert.IsFalse(_mapManager.CanMoveSource(_node1, _player1));
        }

        [TestMethod]
        public void CanMoveSource_ReturnsFalse_ForEnemyWithoutPresence()
        {
            // Arrange: Enemy is at Node 5 (Site B), Player 1 is far away at Node 1
            _node5.Occupant = _player2.Color;
            _node1.Occupant = _player1.Color;

            // Act & Assert
            Assert.IsFalse(_mapManager.CanMoveSource(_node5, _player1));
        }

        [TestMethod]
        public void CanMoveDestination_ReturnsTrue_ForEmptyNode()
        {
            _node5.Occupant = PlayerColor.None;
            Assert.IsTrue(_mapManager.CanMoveDestination(_node5));
        }

        [TestMethod]
        public void CanMoveDestination_ReturnsFalse_ForOccupiedNode()
        {
            _node5.Occupant = _player2.Color;
            Assert.IsFalse(_mapManager.CanMoveDestination(_node5));
        }

        [TestMethod]
        public void MoveTroop_SuccessfullyRelocatesUnit()
        {
            // Arrange
            _node1.Occupant = _player2.Color; // Source
            _node5.Occupant = PlayerColor.None; // Destination

            // Act
            _mapManager.MoveTroop(_node1, _node5);

            // Assert
            Assert.AreEqual(PlayerColor.None, _node1.Occupant, "Source should be empty");
            Assert.AreEqual(_player2.Color, _node5.Occupant, "Destination should have the troop");
        }

        #endregion

        [TestMethod]
        public void CanDeploy_AllowsDeployment_FromSiteAdjacency_WithMixedControl()
        {
            // 1. Setup "City of Gold" simulation
            // Nodes 10-13 are the City. Node 20 is the Route.
            var cityNodeTL = new MapNode(10, Vector2.Zero); // Top-Left (Adjacent to route)
            var cityNodeTR = new MapNode(11, Vector2.Zero);
            var cityNodeDL = new MapNode(12, Vector2.Zero);
            var cityNodeDR = new MapNode(13, Vector2.Zero); // Down-Right (Where Blue is)

            var routeNode = new MapNode(20, Vector2.Zero);

            // 2. Connect Route to Top-Left only
            routeNode.AddNeighbor(cityNodeTL);

            // 3. Create Site and add nodes
            var citySite = new Site("City of Gold", ResourceType.Power, 1, ResourceType.VictoryPoints, 2);
            citySite.AddNode(cityNodeTL);
            citySite.AddNode(cityNodeTR);
            citySite.AddNode(cityNodeDL);
            citySite.AddNode(cityNodeDR);

            // 4. Update MapManager with this new mini-map
            var nodes = new List<MapNode> { cityNodeTL, cityNodeTR, cityNodeDL, cityNodeDR, routeNode };
            var sites = new List<Site> { citySite };
            _mapManager = new MapManager(nodes, sites);

            // 5. SITUATION: Red blocks the exit, Blue is trapped in the back corner
            cityNodeTL.Occupant = PlayerColor.Red;
            cityNodeTR.Occupant = PlayerColor.Red;
            cityNodeDL.Occupant = PlayerColor.Red;

            cityNodeDR.Occupant = PlayerColor.Blue; // Blue is here

            // 6. TEST: Can Blue deploy to the route connected to TL?
            // (Should be TRUE because Blue has presence in the "City of Gold" site via DR)
            bool result = _mapManager.CanDeployAt(routeNode, PlayerColor.Blue);

            Assert.IsTrue(result, "Blue should be able to deploy from the City regardless of which specific node they occupy.");
        }

        [TestMethod]
        public void CanPlaceSpy_ReturnsTrue_EvenWithoutPresence()
        {
            // Rules: "You don't need to have Presence at a site to place a spy there."

            var remoteSite = new Site("Void Portal", ResourceType.Power, 1, ResourceType.Power, 1);
            var remoteNode = new MapNode(99, Vector2.Zero);
            remoteSite.AddNode(remoteNode);

            _mapManager = new MapManager(new List<MapNode> { remoteNode }, new List<Site> { remoteSite });

            // Player has NO troops on the map
            bool result = _mapManager.HasValidPlaceSpyTarget(_player1);

            Assert.IsTrue(result, "Should be able to place a spy on a remote site with zero presence.");
        }

        [TestMethod]
        public void CanDeploy_ReturnsFalse_WhenTargetIsTotallyDisconnected()
        {
            // SETUP: 
            // Player is at Node 1. 
            // Target is Node 99 (far away, no connection).
            var startNode = new MapNode(1, Vector2.Zero);
            var farNode = new MapNode(99, Vector2.Zero);

            // Create a manager with these disconnected nodes
            _mapManager = new MapManager(new List<MapNode> { startNode, farNode }, new List<Site>());

            // Player has a troop at startNode
            startNode.Occupant = _player1.Color;
            _player1.TroopsInBarracks = 5;
            _player1.Power = 5;

            // ACT
            bool result = _mapManager.CanDeployAt(farNode, _player1.Color);

            // ASSERT
            Assert.IsFalse(result, "Should NOT be able to deploy to a node with no physical connection or site presence.");
        }

        [TestMethod]
        public void CanDeploy_ReturnsFalse_WhenAdjacentToEnemySite_ButNoFriendlyPresence()
        {
            // SETUP:
            // Site A contains Node 1 (Enemy occupied).
            // Node 2 is a neighbor to Node 1 (Empty).
            // Player is far away at Node 99.

            var enemyNode = new MapNode(1, Vector2.Zero);
            var targetNode = new MapNode(2, Vector2.Zero);
            var myBaseNode = new MapNode(99, Vector2.Zero);

            // Link target to enemy node
            targetNode.AddNeighbor(enemyNode);

            var siteA = new Site("Enemy Fortress", ResourceType.Power, 1, ResourceType.Power, 1);
            siteA.AddNode(enemyNode);
            siteA.AddNode(targetNode); // Target is inside the enemy site

            _mapManager = new MapManager(
                new List<MapNode> { enemyNode, targetNode, myBaseNode },
                new List<Site> { siteA }
            );

            // Positions
            enemyNode.Occupant = _player2.Color; // Enemy
            myBaseNode.Occupant = _player1.Color; // Me (Far away)

            // ACT
            // I want to deploy at Node 2. It is next to Node 1 (Enemy). 
            // I have NO troops in Site A.
            bool result = _mapManager.CanDeployAt(targetNode, _player1.Color);

            // ASSERT
            Assert.IsFalse(result, "Should NOT be able to deploy based on Enemy presence alone.");
        }

        [TestMethod]
        public void CanDeploy_ReturnsFalse_IfSpyIsAtDifferentSite()
        {
            // SETUP:
            // Site A has my Spy.
            // Site B is far away.
            // I try to deploy at Site B.

            var siteANode = new MapNode(1, Vector2.Zero);
            var siteBNode = new MapNode(2, Vector2.Zero);

            var siteA = new Site("Spy Hub", ResourceType.Power, 1, ResourceType.Power, 1);
            siteA.AddNode(siteANode);

            var siteB = new Site("Target Fort", ResourceType.Power, 1, ResourceType.Power, 1);
            siteB.AddNode(siteBNode);

            _mapManager = new MapManager(
                new List<MapNode> { siteANode, siteBNode },
                new List<Site> { siteA, siteB }
            );

            // I have a spy at Site A
            siteA.Spies.Add(_player1.Color);

            // I have a troop somewhere else just so board isn't "empty" (which allows deploy anywhere)
            var baseNode = new MapNode(99, Vector2.Zero);
            baseNode.Occupant = _player1.Color;
            _mapManager.NodesInternal.Add(baseNode);

            // ACT
            bool result = _mapManager.CanDeployAt(siteBNode, _player1.Color);

            // ASSERT
            Assert.IsFalse(result, "Spy at Site A should NOT grant presence at Site B.");
        }
    }
}