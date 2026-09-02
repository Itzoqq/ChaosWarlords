using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Entities.Cards
{
    [TestClass]
    [TestCategory("Unit")]
    public class EffectConditionTests
    {
        private MatchContext _context = null!;
        private IMapManager _mapManager = null!;
        private IActionSystem _actionSystem = null!;
        private Player _player = null!;

        [TestInitialize]
        public void Setup()
        {
            var turn = Substitute.For<ITurnManager>();
            _mapManager = Substitute.For<IMapManager>();
            var market = Substitute.For<IMarketManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            var cardDb = Substitute.For<ICardDatabase>();
            var playerState = Substitute.For<IPlayerStateManager>();
            var logger = Substitute.For<IGameLogger>();

            _context = new MatchContext(turn, _mapManager, market, _actionSystem, cardDb, playerState, logger);
            _player = new Player(PlayerColor.Red);

            // Default empty map state
            _mapManager.Sites.Returns(new List<Site>());
            _mapManager.Nodes.Returns(new List<MapNode>());
        }

        [TestMethod]
        public void Evaluate_ConditionNone_ReturnsTrue()
        {
            var condition = new EffectCondition(ConditionType.None);
            Assert.IsTrue(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_ControlsSite_WhenControllingSite_ReturnsTrue()
        {
            // Arrange
            var node = new MapNode(0, ChaosWarlords.Source.Core.Data.LogicVector2.Zero) { Occupant = PlayerColor.Red };
            // Constructor: name, controlRes, controlAmt, totalRes, totalAmt
            var site = new StartingSite("TestSite", ResourceType.Power, 1, ResourceType.VictoryPoints, 2);
            site.NodesInternal.Add(node);

            var list = new List<Site> { site };
            _mapManager.Sites.Returns(list);

            var condition = new EffectCondition(ConditionType.ControlsSite);
            Assert.IsTrue(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_HasTroopsDeployed_WhenHasTroop_ReturnsTrue()
        {
            var node = new MapNode(0, ChaosWarlords.Source.Core.Data.LogicVector2.Zero) { Occupant = PlayerColor.Red };
            _mapManager.Nodes.Returns(new List<MapNode> { node });

            var condition = new EffectCondition(ConditionType.HasTroopsDeployed);
            Assert.IsTrue(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_HasResourceAmount_Power_ReturnsTrueIfThresholdMet()
        {
            _player.AddPower(5);
            var condition = new EffectCondition(ConditionType.HasResourceAmount, 5, ResourceType.Power);
            Assert.IsTrue(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_HasResourceAmount_Power_ReturnsFalseIfBelow()
        {
            _player.AddPower(4);
            var condition = new EffectCondition(ConditionType.HasResourceAmount, 5, ResourceType.Power);
            Assert.IsFalse(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_InnerCircleCount_ReturnsTrueIfMet()
        {
            // How to add to InnerCircle? It's a public property List<Card> usually?
            // Let's check Player.cs
            _player.AddToInnerCircle(new Card("1", "Test", 0, CardAspect.Neutral, 0, 0, 0));

            var condition = new EffectCondition(ConditionType.InnerCircleCount, 1);
            Assert.IsTrue(condition.Evaluate(_context, _player));
        }

        // --- ConditionType.OpponentPresentAtSite (Banshee/Infiltrator - planning.txt TIER 2 #1) ---
        // Reads context.ActionSystem.PendingSite, set by PlaceSpyCommand/SpySubsystem right
        // before this OnSuccess-chained condition is evaluated - see EffectCondition's own
        // doc comment. All of these mock IActionSystem.PendingSite directly rather than
        // going through a real ActionSystem, matching this file's existing Unit-level style
        // (the real end-to-end wiring - PlaceSpyCommand actually setting PendingSite before
        // CompleteAction() - is covered separately by BansheeInfiltratorScenarioTests.cs
        // through the real CommandDispatcher path).

        [TestMethod]
        public void Evaluate_OpponentPresentAtSite_Spy_WhenOtherPlayerSpyPresent_ReturnsTrue()
        {
            // Arrange
            var site = TestData.Sites.StartingSite();
            site.AddSpy(PlayerColor.Blue);
            _actionSystem.PendingSite.Returns(site);

            var condition = new EffectCondition(ConditionType.OpponentPresentAtSite, presenceType: SitePresenceType.Spy);

            // Act & Assert
            Assert.IsTrue(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_OpponentPresentAtSite_Spy_WhenOnlyOwnSpyPresent_ReturnsFalse()
        {
            // Arrange
            var site = TestData.Sites.StartingSite();
            site.AddSpy(_player.Color); // The EVALUATING player's own spy - not "another player".
            _actionSystem.PendingSite.Returns(site);

            var condition = new EffectCondition(ConditionType.OpponentPresentAtSite, presenceType: SitePresenceType.Spy);

            // Act & Assert
            Assert.IsFalse(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_OpponentPresentAtSite_Spy_WhenNoSpyPresent_ReturnsFalse()
        {
            // Arrange
            var site = TestData.Sites.StartingSite();
            _actionSystem.PendingSite.Returns(site);

            var condition = new EffectCondition(ConditionType.OpponentPresentAtSite, presenceType: SitePresenceType.Spy);

            // Act & Assert
            Assert.IsFalse(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_OpponentPresentAtSite_Troop_WhenOtherPlayerTroopPresent_ReturnsTrue()
        {
            // Arrange
            var node = new MapNode(0, ChaosWarlords.Source.Core.Data.LogicVector2.Zero) { Occupant = PlayerColor.Blue };
            var site = TestData.Sites.StartingSite();
            site.NodesInternal.Add(node);
            _actionSystem.PendingSite.Returns(site);

            var condition = new EffectCondition(ConditionType.OpponentPresentAtSite, presenceType: SitePresenceType.Troop);

            // Act & Assert
            Assert.IsTrue(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_OpponentPresentAtSite_Troop_WhenOnlyOwnTroopPresent_ReturnsFalse()
        {
            // Arrange
            var node = new MapNode(0, ChaosWarlords.Source.Core.Data.LogicVector2.Zero) { Occupant = _player.Color };
            var site = TestData.Sites.StartingSite();
            site.NodesInternal.Add(node);
            _actionSystem.PendingSite.Returns(site);

            var condition = new EffectCondition(ConditionType.OpponentPresentAtSite, presenceType: SitePresenceType.Troop);

            // Act & Assert
            Assert.IsFalse(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_OpponentPresentAtSite_WhenPendingSiteIsNull_ReturnsFalseNotThrow()
        {
            // Arrange: default/unset PendingSite (e.g. Substitute's default for a reference
            // type, or ActionSystem.CompleteAction's own reset after a chain finishes).
            _actionSystem.PendingSite.Returns((Site?)null);

            var condition = new EffectCondition(ConditionType.OpponentPresentAtSite, presenceType: SitePresenceType.Spy);

            // Act & Assert - must return false, not throw NullReferenceException.
            Assert.IsFalse(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_OpponentPresentAtSite_Troop_IgnoresOpponentSpyOnly()
        {
            // Arrange: an opponent SPY is present but no opponent TROOP - a Troop-gated
            // condition (Infiltrator) must not be satisfied by this alone.
            var site = TestData.Sites.StartingSite();
            site.AddSpy(PlayerColor.Blue);
            _actionSystem.PendingSite.Returns(site);

            var condition = new EffectCondition(ConditionType.OpponentPresentAtSite, presenceType: SitePresenceType.Troop);

            // Act & Assert
            Assert.IsFalse(condition.Evaluate(_context, _player), "Troop-gated condition must not be satisfied by an opponent's spy alone.");
        }

        [TestMethod]
        public void Evaluate_OpponentPresentAtSite_Spy_IgnoresOpponentTroopOnly()
        {
            // Arrange: symmetric case - an opponent TROOP is present but no opponent SPY - a
            // Spy-gated condition (Banshee) must not be satisfied by this alone.
            var node = new MapNode(0, ChaosWarlords.Source.Core.Data.LogicVector2.Zero) { Occupant = PlayerColor.Blue };
            var site = TestData.Sites.StartingSite();
            site.NodesInternal.Add(node);
            _actionSystem.PendingSite.Returns(site);

            var condition = new EffectCondition(ConditionType.OpponentPresentAtSite, presenceType: SitePresenceType.Spy);

            // Act & Assert
            Assert.IsFalse(condition.Evaluate(_context, _player), "Spy-gated condition must not be satisfied by an opponent's troop alone.");
        }
    }
}
