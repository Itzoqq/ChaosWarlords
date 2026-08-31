using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Contexts;
using Microsoft.Xna.Framework;
using NSubstitute;
using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Tests.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace ChaosWarlords.Tests.Integration.Input.Modes
{
    /// <summary>
    /// End-to-end regression coverage for the mandatory devour-from-hand "pre-commit" flow
    /// (a card whose Devour effect is NOT optional and targets CardLocation.Hand -
    /// NormalPlayInputMode.ShouldHandleDevourPreCommit / DevourInputMode.HandlePreCommitSelection).
    ///
    /// No shipped card currently has this shape (see planning.txt), so this flow had zero
    /// test coverage before this suite - it was only reachable by hand-tracing the code.
    /// Uses REAL ActionSystem/MatchManager/PlayerStateManager (not mocks) so the assertions
    /// verify actual game-state outcomes, not just which methods got called.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class DevourInputModePreCommitFlowIntegrationTests
    {
        private ActionSystem _actionSystem = null!;
        private MatchManager _matchManager = null!;
        private MatchContext _context = null!;
        private Player _player = null!;
        private TestGameplayState _state = null!;
        private IInputManager _mockInputManager = null!;

        [TestInitialize]
        public void Setup()
        {
            TestLogger.Initialize();
            var logger = TestLogger.Instance;

            _player = new Player(PlayerColor.Red, displayName: "Player 1");
            var p2 = new Player(PlayerColor.Blue, displayName: "Player 2");

            var mapManager = Substitute.For<IMapManager>();
            var marketManager = Substitute.For<IMarketManager>();
            var cardDatabase = Substitute.For<ICardDatabase>();

            var turnManager = new TurnManager(
                new System.Collections.Generic.List<Player> { _player, p2 },
                new ChaosWarlords.Source.Core.Utilities.SeededGameRandom(12345, logger),
                logger);

            // TurnManager shuffles player order in its constructor, so don't assume _player
            // is still ActivePlayer - use whichever player actually ended up active.
            _player = turnManager.ActivePlayer;

            _actionSystem = new ActionSystem(turnManager, mapManager, logger);
            var playerState = new PlayerStateManager(logger);
            _actionSystem.SetPlayerStateManager(playerState);
            _actionSystem.SetMarketManager(marketManager);

            _context = new MatchContext(turnManager, mapManager, marketManager, _actionSystem, cardDatabase, playerState, null, logger);
            _actionSystem.SetMatchContext(_context);

            _matchManager = new MatchManager(_context, logger, Substitute.For<IVictoryManager>());
            _actionSystem.SetMatchManager(_matchManager);

            // Pre-targets auto-execute by raising OnAutoExecuteCommand rather than running
            // inline (see PreTargetHandler.ExecutePreTargetByType/ExecuteDevourPreTarget) -
            // the real dispatch pipeline (GameplayState) wires this to RecordAndExecuteCommand;
            // mirror that here (matches MatchManagerTests.SetupRealDevourSystem's wiring).
            _actionSystem.OnAutoExecuteCommand += cmd => cmd.Execute(_context);

            _mockInputManager = Substitute.For<IInputManager>();

            _state = new TestGameplayState
            {
                ActionSystem = _actionSystem,
                Logger = logger,
            };
            _state.MatchContext = _context;
        }

        private static Card MandatoryHandDevourCard()
        {
            // Mirrors Wight's shape (Devour a card in hand -> Supplant a troop) but
            // mandatory instead of optional - the one combination no shipped card uses
            // today, and the one that drives NormalPlayInputMode into the pre-commit flow.
            var card = new Card("test_mandatory_devour", "Test Mandatory Devour", 3, CardAspect.Oblivion, 1, 3, 0)
            {
                Location = CardLocation.Hand
            };
            card.Effects.Add(new CardEffect(EffectType.GainResource, 2, ResourceType.Power));
            var devour = new CardEffect(EffectType.Devour, 1) { TargetLocation = CardLocation.Hand, IsOptional = false };
            devour.OnSuccess = new CardEffect(EffectType.GainResource, 3, ResourceType.Influence);
            card.Effects.Add(devour);
            return card;
        }

        [TestMethod]
        public void SelectingDevourTarget_PlaysCardExactlyOnce_AndAppliesEffectsExactlyOnce()
        {
            // Arrange
            var sourceCard = MandatoryHandDevourCard();
            var targetCard = TestData.Cards.CheapCard();
            targetCard.Location = CardLocation.Hand;
            var otherCard = TestData.Cards.CheapCard();
            otherCard.Location = CardLocation.Hand;

            _player.AddToHand(sourceCard);
            _player.AddToHand(targetCard);
            _player.AddToHand(otherCard);
            int startingHandCount = _player.Hand.Count;

            // Simulate NormalPlayInputMode.HandleDevourCardClick: mandatory Hand-devour
            // starts targeting BEFORE the card is played (still sits in Hand).
            _actionSystem.TryStartDevourHand(sourceCard);
            Assert.IsTrue(_actionSystem.IsTargeting(), "Should enter targeting - valid Hand targets exist.");
            Assert.AreEqual(CardLocation.Hand, sourceCard.Location, "Card must NOT be played yet at this point.");

            var mode = new DevourInputMode(_state, _mockInputManager, _actionSystem);
            for (int i = 0; i < 15; i++) mode.HandleUpdate(_mockInputManager, mapManager: null!, _player);

            _state.HoveredHandCard = targetCard;
            var evt = new InputEventArgs(InputEventType.LeftClick, Vector2.Zero);

            // Act: click the target card to devour, then dispatch whatever command comes back
            // (mirroring GameplayInputCoordinator.HandleInputEvent's RecordAndExecuteCommand).
            var cmd = mode.HandleInteraction(evt, Substitute.For<IMarketManager>(), Substitute.For<IMapManager>(), _player, _actionSystem);
            Assert.IsNotNull(cmd, "Pre-commit chain should be complete (no OnSuccess targeting) and return a PlayCardCommand.");
            _state.RecordAndExecuteCommand(cmd!);

            // Assert: the whole card played exactly once.
            Assert.AreEqual(CardLocation.Played, sourceCard.Location, "Source card should be Played.");
            CollectionAssert.Contains(_player.PlayedCards.ToList(), sourceCard);
            Assert.AreEqual(startingHandCount - 2, _player.Hand.Count, "Hand should shrink by exactly 2 (source card + devoured target), not more.");
            CollectionAssert.DoesNotContain(_player.Hand.ToList(), targetCard, "Target card should have been devoured out of hand.");
            CollectionAssert.Contains(_player.Hand.ToList(), otherCard, "Uninvolved hand card should be untouched.");
            CollectionAssert.Contains(_context.VoidPile.ToList(), targetCard, "Devoured target should be in the Void pile exactly once.");
            Assert.AreEqual(1, _context.VoidPile.Count(c => c == targetCard), "Target must be in VoidPile exactly once, not duplicated.");

            // Effects applied exactly once (2 Power base, then OnSuccess's 3 Influence after devour).
            Assert.AreEqual(2, _player.Power, "Base GainResource(Power,2) should apply exactly once.");
            Assert.AreEqual(3, _player.Influence, "OnSuccess GainResource(Influence,3) should apply exactly once.");

            // ActionSystem should have returned cleanly to Normal with an empty stack - no
            // leftover targeting mode bleeding into the next card played (see planning.txt's
            // Supplant/ReturnTroop stack-leak entries for the bug shape this guards against).
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
            Assert.IsFalse(_actionSystem.IsTargeting());
        }

        [TestMethod]
        public void SkippingDevourViaSpacebar_PlaysCardExactlyOnce_WithNoDevour()
        {
            // Arrange
            var sourceCard = MandatoryHandDevourCard();
            var otherCard = TestData.Cards.CheapCard();
            otherCard.Location = CardLocation.Hand;

            _player.AddToHand(sourceCard);
            _player.AddToHand(otherCard);
            int startingHandCount = _player.Hand.Count;

            _actionSystem.TryStartDevourHand(sourceCard);
            Assert.IsTrue(_actionSystem.IsTargeting());

            var mode = new DevourInputMode(_state, _mockInputManager, _actionSystem);
            for (int i = 0; i < 15; i++) mode.HandleUpdate(_mockInputManager, mapManager: null!, _player);

            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Microsoft.Xna.Framework.Input.Keys.Space);

            // Act
            var cmd = mode.HandleInteraction(evt, Substitute.For<IMarketManager>(), Substitute.For<IMapManager>(), _player, _actionSystem);
            Assert.IsNotNull(cmd);
            _state.RecordAndExecuteCommand(cmd!);

            // Assert: card played exactly once, no devour occurred (mandatory effect skipped
            // via Space, matching "no valid/desired target" - only the base effect applies).
            Assert.AreEqual(CardLocation.Played, sourceCard.Location);
            Assert.AreEqual(startingHandCount - 1, _player.Hand.Count, "Hand should shrink by exactly 1 (source card only).");
            CollectionAssert.Contains(_player.Hand.ToList(), otherCard);
            Assert.AreEqual(2, _player.Power, "Base effect should still apply exactly once.");
            Assert.AreEqual(0, _player.Influence, "OnSuccess effect should NOT apply when devour is skipped.");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
        }
    }
}
