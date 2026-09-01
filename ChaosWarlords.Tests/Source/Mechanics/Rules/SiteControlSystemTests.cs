using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Mechanics.Rules;

using ChaosWarlords.Source.Core.Interfaces.Services;
using NSubstitute;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]

    [TestCategory("Unit")]
    public class SiteControlSystemTests
    {
        private SiteControlSystem _system = null!;
        private Player _player1 = null!;
        private Player _player2 = null!;
        private Site _siteA = null!;
        private MapNode _node1 = null!, _node2 = null!;
        private IPlayerStateManager _stateManager = null!;

        [TestInitialize]
        public void Setup()
        {
            _stateManager = Substitute.For<IPlayerStateManager>();
            _system = new SiteControlSystem(_stateManager, Utilities.TestLogger.Instance);
            _player1 = TestData.Players.RedPlayer();
            _player1.SpendPower(_player1.Power);
            _player1.SpendInfluence(_player1.Influence);

            _player2 = TestData.Players.BluePlayer();
            _player2.SpendPower(_player2.Power);
            _player2.SpendInfluence(_player2.Influence);

            // Setup Site with 2 nodes
            _node1 = TestData.MapNodes.Node1();
            _node2 = TestData.MapNodes.Node2();
            _siteA = TestData.Sites.CitySite();
            _siteA.AddNode(_node1);
            _siteA.AddNode(_node2);
        }

        [TestMethod]
        public void Recalculate_AssignsOwner_Majority_TroopsOnly()
        {
            _node1.Occupant = _player1.Color;

            // Spy should NOT count for control
            _siteA.Spies.Add(_player2.Color);

            _system.RecalculateSiteState(_siteA, _player1);

            // Red has 1 Troop, Blue has 0 Troops (1 Spy). Red should control.
            Assert.AreEqual(_player1.Color, _siteA.Owner);
        }

        [TestMethod]
        public void Recalculate_GrantsTotalControl_IfNoEnemyPresence()
        {
            _node1.Occupant = _player1.Color;
            // Node 2 is empty. Standard Tyrants rules: Total Control = Control + No Enemies.
            // Empty nodes do not prevent Total Control.

            _system.RecalculateSiteState(_siteA, _player1);

            Assert.IsTrue(_siteA.HasTotalControl);
        }

        [TestMethod]
        public void Recalculate_BlocksTotalControl_IfEnemySpyPresent()
        {
            _node1.Occupant = _player1.Color;
            // Enemy Spy present
            _siteA.Spies.Add(_player2.Color);

            _system.RecalculateSiteState(_siteA, _player1);

            Assert.AreEqual(_player1.Color, _siteA.Owner); // Still Owner
            Assert.IsFalse(_siteA.HasTotalControl); // But No Total Control
        }

        [TestMethod]
        public void Recalculate_BlocksTotalControl_IfEnemyTroopPresent()
        {
            _node1.Occupant = _player1.Color;
            _node2.Occupant = _player2.Color;
            // 1 vs 1 -> Tie -> No Owner usually.
            // Let's add another node for Red to ensure they own it but enemy exists.
            var node3 = TestData.MapNodes.Node3();
            node3.Occupant = _player1.Color;
            _siteA.AddNode(node3);

            _system.RecalculateSiteState(_siteA, _player1);

            Assert.AreEqual(_player1.Color, _siteA.Owner); // 2 vs 1
            Assert.IsFalse(_siteA.HasTotalControl); // Enemy troop exists
        }

        [TestMethod]
        public void Recalculate_AssignsOwner_ToBlackPlayer_WhenBlackHasMajority()
        {
            // Regression test: CalculateSiteOwner used to only count Red/Blue troops, so a
            // Black or Orange player's troops were never counted toward majority at all -
            // silently unreachable in 3-4 player matches (once those exist - see
            // MatchFactory.Build's playerColors parameter). See planning.txt.
            var black = TestData.Players.BlackPlayer();
            _node1.Occupant = black.Color;
            _node2.Occupant = _player1.Color; // 1 Red vs 1 Black at a 2-node site: no majority yet

            var node3 = TestData.MapNodes.Node3();
            node3.Occupant = black.Color;
            _siteA.AddNode(node3);

            _system.RecalculateSiteState(_siteA, black);

            Assert.AreEqual(black.Color, _siteA.Owner, "Black should control the site with 2 troops vs Red's 1.");
        }

        [TestMethod]
        public void Recalculate_AssignsOwner_ToOrangePlayer_WhenOrangeHasMajority()
        {
            var orange = TestData.Players.OrangePlayer();
            _node1.Occupant = orange.Color;
            _node2.Occupant = _player1.Color;

            var node3 = TestData.MapNodes.Node3();
            node3.Occupant = orange.Color;
            _siteA.AddNode(node3);

            _system.RecalculateSiteState(_siteA, orange);

            Assert.AreEqual(orange.Color, _siteA.Owner, "Orange should control the site with 2 troops vs Red's 1.");
        }

        [TestMethod]
        public void Recalculate_NoOwner_WhenFourColorsAreAllTied()
        {
            var black = TestData.Players.BlackPlayer();
            var orange = TestData.Players.OrangePlayer();

            _node1.Occupant = _player1.Color; // Red
            _node2.Occupant = _player2.Color; // Blue
            var node3 = TestData.MapNodes.Node3();
            node3.Occupant = black.Color;
            _siteA.AddNode(node3);
            var node4 = TestData.MapNodes.Node4();
            node4.Occupant = orange.Color;
            _siteA.AddNode(node4);

            _system.RecalculateSiteState(_siteA, _player1);

            Assert.AreEqual(ChaosWarlords.Source.Utilities.PlayerColor.None, _siteA.Owner, "A 4-way tie should have no owner.");
        }

        [TestMethod]
        public void Recalculate_NoOwner_WhenNeutralHoldsTheOutrightMajority()
        {
            // Rulebook: white/unaligned (Neutral) troops block a majority but never themselves
            // "control" a site - matches the pre-fix behavior of never returning Neutral.
            _node1.Occupant = ChaosWarlords.Source.Utilities.PlayerColor.Neutral;
            _node2.Occupant = _player1.Color;
            var node3 = TestData.MapNodes.Node3();
            node3.Occupant = ChaosWarlords.Source.Utilities.PlayerColor.Neutral;
            _siteA.AddNode(node3);

            _system.RecalculateSiteState(_siteA, _player1);

            Assert.AreEqual(ChaosWarlords.Source.Utilities.PlayerColor.None, _siteA.Owner, "Neutral holding the majority should mean no player controls the site.");
        }

        [TestMethod]
        public void DistributeRewards_City_GivesIncome()
        {
            _siteA.IsCity = true;
            _siteA.Owner = _player1.Color;
            _player1.AddPower(0);

            var sites = new List<Site> { _siteA };
            _system.DistributeStartOfTurnRewards(sites, _player1);

            _stateManager.Received(1).AddPower(_player1, 1);
        }

        [TestMethod]
        public void DistributeRewards_NonCity_GivesZero()
        {
            _siteA.IsCity = false;
            _siteA.Owner = _player1.Color;
            _player1.AddPower(0);

            var sites = new List<Site> { _siteA };
            _system.DistributeStartOfTurnRewards(sites, _player1);

            _stateManager.DidNotReceive().AddPower(Arg.Any<Player>(), Arg.Any<int>());
        }

        [TestMethod]
        public void DistributeRewards_Additive_TotalControl()
        {
            _siteA.IsCity = true;
            _siteA.Owner = _player1.Color;
            _siteA.HasTotalControl = true;
            _player1.AddPower(0);
            _player1.VictoryPoints = 0;

            // Control = 1 Power, Total = 5 VP (TestData.Sites.CitySite has 1 VP)
            var sites = new List<Site> { _siteA };
            _system.DistributeStartOfTurnRewards(sites, _player1);

            _stateManager.Received(1).AddPower(_player1, 1);
            _stateManager.Received(1).AddVictoryPoints(_player1, 1);
        }
    }
}


