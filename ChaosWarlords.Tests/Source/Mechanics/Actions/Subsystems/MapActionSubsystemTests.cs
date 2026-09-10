using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Mechanics.Actions.Subsystems;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Mechanics.Actions.Subsystems
{
    // Isolated unit coverage for MapActionSubsystem (split out of ActionSystem 2026-09-10, see
    // planning.txt TIER 2 item 12) - mirrors SpySubsystemTests/DevourSubsystemTests' pattern
    // (fully mocked deps, construct the real subsystem directly). The existing functional/
    // scenario test suite already exercises this same logic end to end through real cards and a
    // real CommandDispatcher; this file targets the subsystem's own branches directly.
    [TestClass]
    [TestCategory("Unit")]
    public class MapActionSubsystemTests
    {
        private MapActionSubsystem _subsystem = null!;
        private IMapManager _mapManager = null!;
        private ITurnManager _turnManager = null!;
        private IGameLogger _logger = null!;
        private IPlayerStateManager _playerStateManager = null!;
        private IActionSystem _actionSystem = null!;
        private IDevourSubsystem _devourSubsystem = null!;
        private IMatchManager _matchManager = null!;

        private Player _activePlayer = null!;
        private MapNode _node = null!;

        [TestInitialize]
        public void Setup()
        {
            _mapManager = Substitute.For<IMapManager>();
            _turnManager = Substitute.For<ITurnManager>();
            _logger = Substitute.For<IGameLogger>();
            _playerStateManager = Substitute.For<IPlayerStateManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _devourSubsystem = Substitute.For<IDevourSubsystem>();
            _matchManager = Substitute.For<IMatchManager>();

            _activePlayer = TestData.Players.RedPlayer();
            _turnManager.ActivePlayer.Returns(_activePlayer);

            _node = TestData.MapNodes.Node1();
            _node.Occupant = PlayerColor.Blue;

            _subsystem = new MapActionSubsystem(_mapManager, _turnManager, _logger, _playerStateManager, _actionSystem, _devourSubsystem);
            _subsystem.SetMatchManager(_matchManager);
        }

        #region PerformAssassinate

        [TestMethod]
        public void PerformAssassinate_NotPaidByCard_SpendsPower()
        {
            _subsystem.PerformAssassinate(_node, cardId: null);

            _playerStateManager.Received(1).TrySpendPower(_activePlayer, GameConstants.AssassinatePowerCost);
            _mapManager.Received(1).Assassinate(_node, _activePlayer);
            _actionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void PerformAssassinate_PaidByCard_DoesNotSpendPower()
        {
            _subsystem.PerformAssassinate(_node, cardId: "some_card");

            _playerStateManager.DidNotReceive().TrySpendPower(Arg.Any<Player>(), Arg.Any<int>());
            _actionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void PerformAssassinate_CapturesPendingAffectedPlayerColor_BeforeMutation()
        {
            _subsystem.PerformAssassinate(_node, cardId: null);

            _actionSystem.Received(1).SetPendingAffectedPlayerColor(PlayerColor.Blue);
        }

        [TestMethod]
        public void PerformAssassinate_WithDevourCardId_ConsumesFromHandAndClearsDevourState()
        {
            var cardToDevour = new Card("devour_me", "Devour Me", 0, CardAspect.Neutral, 0, 0, 0);
            _activePlayer.AddToHand(cardToDevour);

            _subsystem.PerformAssassinate(_node, cardId: null, devourCardId: "devour_me");

            _matchManager.Received(1).DevourCard(cardToDevour);
            _devourSubsystem.Received(1).ClearState();
        }

        [TestMethod]
        public void PerformAssassinate_NoDevourCardIdButPendingDevourCardSet_ConsumesPendingDevourCard()
        {
            var pendingCard = new Card("pending_devour", "Pending Devour", 0, CardAspect.Neutral, 0, 0, 0);
            _actionSystem.PendingDevourCard.Returns(pendingCard);

            _subsystem.PerformAssassinate(_node, cardId: null);

            _matchManager.Received(1).DevourCard(pendingCard);
            _devourSubsystem.Received(1).ClearState();
        }

        [TestMethod]
        public void PerformAssassinate_NoDevourCardIdAndNoPendingDevourCard_NeverCallsDevourCard()
        {
            _subsystem.PerformAssassinate(_node, cardId: null);

            _matchManager.DidNotReceive().DevourCard(Arg.Any<Card>());
            _devourSubsystem.DidNotReceive().ClearState();
        }

        [TestMethod]
        public void PerformAssassinate_DevourCardIdNotInHand_StillClearsDevourStateButNeverCallsDevourCard()
        {
            // devourCardId is explicit and authoritative, but the referenced card isn't actually
            // in hand (e.g. a stale/forged replay payload) - ConsumePendingDevour must not throw
            // or fall back to PendingDevourCard, and still clears the devour subsystem's state.
            _subsystem.PerformAssassinate(_node, cardId: null, devourCardId: "not_in_hand");

            _matchManager.DidNotReceive().DevourCard(Arg.Any<Card>());
            _devourSubsystem.Received(1).ClearState();
        }

        [TestMethod]
        public void PerformAssassinate_DevourCardIdCombinedWithRestrictRepeatsToFirstTargetSite_BothApplyIndependently()
        {
            // Wight's Devour->Supplant chain shape doesn't combine with Minotaur Skeleton's
            // RestrictRepeatsToFirstTargetSite on any shipped card, but nothing about
            // PerformAssassinate's implementation should make them interfere if they ever did.
            var cardToDevour = new Card("devour_me", "Devour Me", 0, CardAspect.Neutral, 0, 0, 0);
            _activePlayer.AddToHand(cardToDevour);
            var site = TestData.Sites.NeutralSite();
            _actionSystem.PendingSite.Returns((Site?)null);
            _actionSystem.CurrentSourceEffect.Returns(new CardEffect(EffectType.Assassinate, 3) { RestrictRepeatsToFirstTargetSite = true });
            _mapManager.GetSiteForNode(_node).Returns(site);

            _subsystem.PerformAssassinate(_node, cardId: null, devourCardId: "devour_me");

            _matchManager.Received(1).DevourCard(cardToDevour);
            _actionSystem.Received(1).SetPendingSiteForChain(site);
        }

        [TestMethod]
        public void PerformAssassinate_RestrictRepeatsToFirstTargetSite_BindsPendingSiteOnFirstRepeat()
        {
            var site = TestData.Sites.NeutralSite();
            _actionSystem.PendingSite.Returns((Site?)null);
            _actionSystem.CurrentSourceEffect.Returns(new CardEffect(EffectType.Assassinate, 3) { RestrictRepeatsToFirstTargetSite = true });
            _mapManager.GetSiteForNode(_node).Returns(site);

            _subsystem.PerformAssassinate(_node, cardId: null);

            _actionSystem.Received(1).SetPendingSiteForChain(site);
        }

        [TestMethod]
        public void PerformAssassinate_RestrictRepeatsToFirstTargetSite_DoesNotRebindOnLaterRepeat()
        {
            var alreadyBoundSite = TestData.Sites.NeutralSite();
            _actionSystem.PendingSite.Returns(alreadyBoundSite);
            _actionSystem.CurrentSourceEffect.Returns(new CardEffect(EffectType.Assassinate, 3) { RestrictRepeatsToFirstTargetSite = true });

            _subsystem.PerformAssassinate(_node, cardId: null);

            _actionSystem.DidNotReceive().SetPendingSiteForChain(Arg.Any<Site>());
        }

        [TestMethod]
        public void PerformAssassinate_WithoutRestrictRepeatsToFirstTargetSite_NeverBindsPendingSite()
        {
            _actionSystem.PendingSite.Returns((Site?)null);
            _actionSystem.CurrentSourceEffect.Returns(new CardEffect(EffectType.Assassinate, 1));

            _subsystem.PerformAssassinate(_node, cardId: null);

            _actionSystem.DidNotReceive().SetPendingSiteForChain(Arg.Any<Site>());
        }

        [TestMethod]
        public void PerformAssassinate_GainResourcePerRepeatInfluence_GrantsInfluenceOnEachCall()
        {
            _actionSystem.CurrentSourceEffect.Returns(new CardEffect(EffectType.Assassinate, 3) { GainResourcePerRepeat = ResourceType.Influence });

            _subsystem.PerformAssassinate(_node, cardId: null);

            _playerStateManager.Received(1).AddInfluence(_activePlayer, 1);
        }

        [TestMethod]
        public void PerformAssassinate_GainResourcePerRepeatPower_GrantsPowerOnEachCall()
        {
            _actionSystem.CurrentSourceEffect.Returns(new CardEffect(EffectType.Assassinate, 3) { GainResourcePerRepeat = ResourceType.Power });

            _subsystem.PerformAssassinate(_node, cardId: null);

            _playerStateManager.Received(1).AddPower(_activePlayer, 1);
        }

        [TestMethod]
        public void PerformAssassinate_GainResourcePerRepeatVictoryPoints_GrantsVictoryPointsOnEachCall()
        {
            _actionSystem.CurrentSourceEffect.Returns(new CardEffect(EffectType.Assassinate, 3) { GainResourcePerRepeat = ResourceType.VictoryPoints });

            _subsystem.PerformAssassinate(_node, cardId: null);

            _playerStateManager.Received(1).AddVictoryPoints(_activePlayer, 1);
        }

        [TestMethod]
        public void PerformAssassinate_GainResourcePerRepeatUnwiredResourceType_LogsWarningAndGrantsNothing()
        {
            // Troops is deliberately unsupported here (CardEffectApplier.ApplyGainResource
            // credits PendingFreeTroops instead) - see GrantResourcePerRepeat's own doc comment.
            _actionSystem.CurrentSourceEffect.Returns(new CardEffect(EffectType.Assassinate, 3) { GainResourcePerRepeat = ResourceType.Troops });

            _subsystem.PerformAssassinate(_node, cardId: null);

            _logger.Received(1).Log(Arg.Is<string>(s => s.Contains("no case wired for ResourceType")), LogChannel.Warning);
            _playerStateManager.DidNotReceive().AddPower(Arg.Any<Player>(), Arg.Any<int>());
            _playerStateManager.DidNotReceive().AddInfluence(Arg.Any<Player>(), Arg.Any<int>());
            _playerStateManager.DidNotReceive().AddVictoryPoints(Arg.Any<Player>(), Arg.Any<int>());
        }

        [TestMethod]
        public void PerformAssassinate_GainResourcePerRepeatNone_GrantsNothing()
        {
            _actionSystem.CurrentSourceEffect.Returns(new CardEffect(EffectType.Assassinate, 3));

            _subsystem.PerformAssassinate(_node, cardId: null);

            _playerStateManager.DidNotReceive().AddInfluence(Arg.Any<Player>(), Arg.Any<int>());
            _playerStateManager.DidNotReceive().AddVictoryPoints(Arg.Any<Player>(), Arg.Any<int>());
        }

        #endregion

        #region PerformReturnTroop

        [TestMethod]
        public void PerformReturnTroop_CallsMapManagerAndCompletesAction()
        {
            _subsystem.PerformReturnTroop(_node, cardId: null);

            _mapManager.Received(1).ReturnTroop(_node, _activePlayer);
            _actionSystem.Received(1).CompleteAction();
        }

        #endregion

        #region PerformSupplant

        [TestMethod]
        public void PerformSupplant_CapturesPendingAffectedPlayerColorAndCompletesAction()
        {
            _subsystem.PerformSupplant(_node, cardId: null);

            _actionSystem.Received(1).SetPendingAffectedPlayerColor(PlayerColor.Blue);
            _mapManager.Received(1).Supplant(_node, _activePlayer);
            _actionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void PerformSupplant_WithDevourCardId_ConsumesFromHand()
        {
            var cardToDevour = new Card("devour_me", "Devour Me", 0, CardAspect.Neutral, 0, 0, 0);
            _activePlayer.AddToHand(cardToDevour);

            _subsystem.PerformSupplant(_node, cardId: null, devourCardId: "devour_me");

            _matchManager.Received(1).DevourCard(cardToDevour);
        }

        #endregion

        #region PerformDeployFromTrophyHall

        [TestMethod]
        public void PerformDeployFromTrophyHall_TrophyRemoved_DeploysAndCompletesAction()
        {
            var sourcePlayer = TestData.Players.BluePlayer();
            _turnManager.GetPlayerByColor(PlayerColor.Blue).Returns(sourcePlayer);
            _playerStateManager.RemoveTrophy(sourcePlayer, PlayerColor.Neutral).Returns(true);

            _subsystem.PerformDeployFromTrophyHall(_node, PlayerColor.Blue, PlayerColor.Neutral, cardId: null);

            _mapManager.Received(1).DeployFromTrophyHall(_node, _activePlayer);
            _actionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void PerformDeployFromTrophyHall_TrophyNotRemoved_StillCompletesActionButDoesNotDeploy()
        {
            var sourcePlayer = TestData.Players.BluePlayer();
            _turnManager.GetPlayerByColor(PlayerColor.Blue).Returns(sourcePlayer);
            _playerStateManager.RemoveTrophy(sourcePlayer, PlayerColor.Neutral).Returns(false);

            _subsystem.PerformDeployFromTrophyHall(_node, PlayerColor.Blue, PlayerColor.Neutral, cardId: null);

            _mapManager.DidNotReceive().DeployFromTrophyHall(Arg.Any<MapNode>(), Arg.Any<Player>());
            _actionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void PerformDeployFromTrophyHall_UnknownSourcePlayer_StillCompletesActionButDoesNotDeploy()
        {
            _turnManager.GetPlayerByColor(PlayerColor.Blue).Returns((Player?)null);

            _subsystem.PerformDeployFromTrophyHall(_node, PlayerColor.Blue, PlayerColor.Neutral, cardId: null);

            _mapManager.DidNotReceive().DeployFromTrophyHall(Arg.Any<MapNode>(), Arg.Any<Player>());
            _actionSystem.Received(1).CompleteAction();
        }

        #endregion

        #region PerformDeployTroop

        [TestMethod]
        public void PerformDeployTroop_Success_CreditsPendingFreeTroopAndRecordsNode()
        {
            int pendingBefore = _activePlayer.PendingFreeTroops;
            _mapManager.TryDeploy(_activePlayer, _node).Returns(true);

            _subsystem.PerformDeployTroop(_node, cardId: null);

            Assert.AreEqual(pendingBefore + 1, _activePlayer.PendingFreeTroops);
            _actionSystem.Received(1).AddPendingDeployedNode(_node);
            _actionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void PerformDeployTroop_TryDeployFails_LogsWarningButStillCompletesAction()
        {
            _mapManager.TryDeploy(_activePlayer, _node).Returns(false);

            _subsystem.PerformDeployTroop(_node, cardId: null);

            _logger.Received(1).Log(Arg.Is<string>(s => s.Contains("TryDeploy unexpectedly failed")), LogChannel.Warning);
            _actionSystem.Received(1).AddPendingDeployedNode(_node);
            _actionSystem.Received(1).CompleteAction();
        }

        #endregion

        #region PerformMoveTroop

        [TestMethod]
        public void PerformMoveTroop_CallsMapManagerAndCompletesAction()
        {
            var dest = TestData.MapNodes.Node2();

            _subsystem.PerformMoveTroop(_node, dest, cardId: null);

            _mapManager.Received(1).MoveTroop(_node, dest, _activePlayer);
            _actionSystem.Received(1).CompleteAction();
        }

        #endregion
    }
}
