using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NSubstitute;
using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Commands;
using System.Linq;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Data;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Integration.Input.Modes
{
    [TestClass]
    [TestCategory("Integration")]
    public class PromoteInputModeTests
    {
        private PromoteInputMode _mode = null!;
        private TestGameplayState _stateFake = null!;
        private IInputManager _inputSub = null!;
        private IActionSystem _actionSub = null!;
        private IMarketManager _marketSub = null!;
        private IMapManager _mapSub = null!;
        private Player _activePlayer = null!;
        private ITurnManager _turnManagerSub = null!;
        private TurnContext _realTurnContext = null!;

        [TestInitialize]
        public void Setup()
        {
            _inputSub = Substitute.For<IInputManager>();
            _actionSub = Substitute.For<IActionSystem>();
            _marketSub = Substitute.For<IMarketManager>();
            _mapSub = Substitute.For<IMapManager>();
            
            _activePlayer = TestData.Players.RedPlayer();
            _realTurnContext = new TurnContext(_activePlayer, Utilities.TestLogger.Instance);
            
            _turnManagerSub = Substitute.For<ITurnManager>();
            _turnManagerSub.CurrentTurnContext.Returns(_realTurnContext);
            _turnManagerSub.ActivePlayer.Returns(_activePlayer);

            var cardDb = Substitute.For<ICardDatabase>();
            var ps = new PlayerStateManager(Utilities.TestLogger.Instance);
            
            // Context
            var matchContext = new MatchContext(
                _turnManagerSub,
                _mapSub,
                _marketSub,
                _actionSub,
                cardDb,
                ps,
                Utilities.TestLogger.Instance
            );

            // Fake State
            _stateFake = new TestGameplayState
            {
                MatchContext = matchContext,
                TurnManager = _turnManagerSub,
                InputManager = _inputSub,
                ActionSystem = _actionSub,
                MarketManager = _marketSub,
                MapManager = _mapSub
            };

            // Mode
            _mode = new PromoteInputMode(_stateFake, _inputSub, _actionSub, 1);
        }

        [TestMethod]
        public void HandleInteraction_RightClick_DoesNotSwitchMode_IfMandatory()
        {
            // Arrange - a plain, mandatory credit (core_noble's "promote a card played this
            // turn" shape, isOptional defaults to false) must refuse Right-click/Escape.
            var card = TestData.Cards.CheapCard();
            _realTurnContext.AddPromotionCredit(card, 1);
            var evt = new InputEventArgs(InputEventType.RightClick, Vector2.Zero);

            // Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            Assert.IsNull(result, "Should return null (action ignored).");
            // Verify warning log? (StateFake logger would implicitly capture it, but we can assume logic correctness if result is null)
            _actionSub.DidNotReceive().CancelTargeting();
        }

        [TestMethod]
        public void HandleInteraction_RightClick_DeclinesRemaining_IfAllOutstandingCreditsAreOptional()
        {
            // Cultist of Myrkul/Zuggtmoy's "promote up to 2 other cards played this turn" -
            // Right-click should forfeit the credit(s) and proceed straight to end of turn.
            var card = TestData.Cards.CheapCard();
            _realTurnContext.AddPromotionCredit(card, 2, isOptional: true);
            var evt = new InputEventArgs(InputEventType.RightClick, Vector2.Zero);

            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            Assert.IsInstanceOfType(result, typeof(EndTurnCommand));
            // NOT CancelTargeting() - that would revert the WHOLE redemption session
            // (including any earlier real promotions) via its full-sequence snapshot, not just
            // forfeit the declined remainder. See ActionSystem.DeclineRemainingPromotions.
            _actionSub.Received(1).DeclineRemainingPromotions();
            _actionSub.DidNotReceive().CancelTargeting();
            Assert.AreEqual(0, _realTurnContext.PendingPromotionsCount, "The declined credits must be forfeited.");
        }

        [TestMethod]
        public void HandleInteraction_Escape_StillRefuses_IfOneOutstandingCreditIsMandatory_EvenAlongsideOptionalOnes()
        {
            var mandatorySource = TestData.Cards.CheapCard();
            var optionalSource = TestData.Cards.ExpensiveCard();
            _realTurnContext.AddPromotionCredit(mandatorySource, 1, isOptional: false);
            _realTurnContext.AddPromotionCredit(optionalSource, 2, isOptional: true);
            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.Escape);

            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            Assert.IsNull(result, "A mandatory credit is still outstanding - declining must be refused.");
            _actionSub.DidNotReceive().CancelTargeting();
        }

        [TestMethod]
        public void HandleInteraction_ClickingSelfPromote_DoesNothing()
        {
            // Arrange
            var card = TestData.Cards.CheapCard();
            _activePlayer.AddToPlayed(card);

            // Set credit coming ONLY from this card
            _realTurnContext.AddPromotionCredit(card, 1);

            // Mock hovering this card (Using Fake)
            _stateFake.HoveredPlayedCard = card;

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            // Act
            _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            CollectionAssert.Contains(_activePlayer.PlayedCards.ToList(), card, "Card should remain in played pile (invalid target).");
            Assert.AreEqual(1, _realTurnContext.PendingPromotionsCount, "Credit should not be consumed.");
            Assert.IsFalse(_stateFake.ExecutedCommands.Any(), "No commands should be executed.");
        }

        [TestMethod]
        public void HandleInteraction_ClickingValidTarget_PromotesAndEndsTurn()
        {
            // Arrange
            var sourceCard = TestData.Cards.CheapCard();
            var targetCard = TestData.Cards.CheapCard();

            // Force unique IDs to prevent collision
            try { 
                typeof(Card).GetProperty("Id")?.SetValue(sourceCard, "ID_SOURCE");
                typeof(Card).GetProperty("Id")?.SetValue(targetCard, "ID_TARGET");
            } catch { }

            _activePlayer.AddToPlayed(sourceCard);
            _activePlayer.AddToPlayed(targetCard);

            // Credit comes from Source, so Target is valid
            _realTurnContext.AddPromotionCredit(sourceCard, 1);

            // Mock hovering target (Using Fake)
            _stateFake.HoveredPlayedCard = targetCard;

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            // Act
            var resultCmd = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            // 1. Verify Command Execution via Fake State
            var promoteCmd = _stateFake.ExecutedCommands.OfType<PromoteCommand>().FirstOrDefault();
            Assert.IsNotNull(promoteCmd, "PromoteCommand should have been executed.");
            Assert.AreEqual(targetCard.Id, promoteCmd.CardId, "PromoteCommand should target the correct card.");

            // Verify State
            CollectionAssert.DoesNotContain(_activePlayer.PlayedCards.ToList(), targetCard, "Target should be removed from Played.");
            CollectionAssert.Contains(_activePlayer.InnerCircle.ToList(), targetCard, "Target should be in Inner Circle.");
            Assert.AreEqual(0, _realTurnContext.PendingPromotionsCount, "Credit should be consumed.");

            // Verify EndTurn Command is returned
            Assert.IsInstanceOfType(resultCmd, typeof(EndTurnCommand), "Should return EndTurnCommand");
            // NOT CancelTargeting() - that would revert the WHOLE redemption session
            // (including the promotion just asserted above) via its full-sequence snapshot,
            // not just cleanly finish it. See ActionSystem.DeclineRemainingPromotions.
            _actionSub.Received(1).DeclineRemainingPromotions();
            _actionSub.DidNotReceive().CancelTargeting();
        }

        // --- Aspect-filtered credits (Air/Fire/Water Elemental Myrmidon) ---

        [TestMethod]
        public void HandleInteraction_ClickingAMismatchedAspectTarget_DoesNothing()
        {
            var sourceCard = new CardBuilder().WithName("air_myrmidon").WithAspect(CardAspect.Shadow).Build();
            var targetCard = new CardBuilder().WithName("wrong_aspect_target").WithAspect(CardAspect.Warlord).Build();
            _activePlayer.AddToPlayed(targetCard);

            // Credit is filtered to Order - Warlord is not a valid target even though it's not
            // the credit's own source.
            _realTurnContext.AddPromotionCredit(sourceCard, 1, requiredAspect: CardAspect.Order);
            _stateFake.HoveredPlayedCard = targetCard;

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            CollectionAssert.Contains(_activePlayer.PlayedCards.ToList(), targetCard, "Card should remain in played pile (invalid target).");
            Assert.AreEqual(1, _realTurnContext.PendingPromotionsCount, "Credit should not be consumed.");
            Assert.IsFalse(_stateFake.ExecutedCommands.Any(), "No commands should be executed.");
        }

        [TestMethod]
        public void HandleInteraction_ClickingAMatchingAspectTarget_PromotesAndEndsTurn()
        {
            var sourceCard = new CardBuilder().WithName("air_myrmidon").WithAspect(CardAspect.Shadow).Build();
            var targetCard = new CardBuilder().WithName("order_target").WithAspect(CardAspect.Order).Build();
            _activePlayer.AddToPlayed(targetCard);

            _realTurnContext.AddPromotionCredit(sourceCard, 1, requiredAspect: CardAspect.Order);
            _stateFake.HoveredPlayedCard = targetCard;

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            var resultCmd = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            var promoteCmd = _stateFake.ExecutedCommands.OfType<PromoteCommand>().FirstOrDefault();
            Assert.IsNotNull(promoteCmd, "PromoteCommand should have been executed.");
            Assert.AreEqual(targetCard.Id, promoteCmd.CardId);

            CollectionAssert.DoesNotContain(_activePlayer.PlayedCards.ToList(), targetCard, "Target should be removed from Played.");
            CollectionAssert.Contains(_activePlayer.InnerCircle.ToList(), targetCard, "Target should be in Inner Circle.");
            Assert.AreEqual(0, _realTurnContext.PendingPromotionsCount, "Credit should be consumed.");
            Assert.IsInstanceOfType(resultCmd, typeof(EndTurnCommand));
        }

        [TestMethod]
        public void HandleInteraction_TwoFilteredMandatoryCreditsSharingOneMatchingCard_DoesNotSoftLock()
        {
            // Regression: Air Elemental Myrmidon + Fire Elemental Myrmidon (both mandatory,
            // both Order-filtered) played the same turn, with only ONE Order card also played.
            // Before ForfeitUnsatisfiableCredits, consuming the shared card for one credit left
            // the other one permanently stuck (mandatory, so CanDeclineRemainingPromotions was
            // false, and every further click was rejected by HasValidCreditFor - no path back
            // to Normal at all). A fresh 2-credit PromoteInputMode is built here (the
            // class-level _mode from Setup() is built with amountToPromote=1).
            var airMyrmidon = new CardBuilder().WithName("air_myrmidon").WithAspect(CardAspect.Shadow).Build();
            var fireMyrmidon = new CardBuilder().WithName("fire_myrmidon").WithAspect(CardAspect.Sorcery).Build();
            var orderCard = new CardBuilder().WithName("order_card").WithAspect(CardAspect.Order).Build();
            _activePlayer.AddToPlayed(airMyrmidon);
            _activePlayer.AddToPlayed(fireMyrmidon);
            _activePlayer.AddToPlayed(orderCard);
            _realTurnContext.AddPromotionCredit(airMyrmidon, 1, requiredAspect: CardAspect.Order);
            _realTurnContext.AddPromotionCredit(fireMyrmidon, 1, requiredAspect: CardAspect.Order);

            var twoCreditMode = new PromoteInputMode(_stateFake, _inputSub, _actionSub, 2);
            _stateFake.HoveredPlayedCard = orderCard;
            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            var resultCmd = twoCreditMode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            CollectionAssert.Contains(_activePlayer.InnerCircle.ToList(), orderCard, "The one legal target should have been promoted.");
            Assert.AreEqual(0, _realTurnContext.PendingPromotionsCount, "The stranded sibling credit must have been forfeited, not left outstanding forever.");
            Assert.IsInstanceOfType(resultCmd, typeof(EndTurnCommand), "The redemption must complete and return EndTurn, not soft-lock waiting for an impossible 2nd click.");
        }
    }
}
