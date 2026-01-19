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
                null, 
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
            // Arrange
            var evt = new InputEventArgs(InputEventType.RightClick, Vector2.Zero);

            // Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            Assert.IsNull(result, "Should return null (action ignored).");
            // Verify warning log? (StateFake logger would implicitly capture it, but we can assume logic correctness if result is null)
            _actionSub.DidNotReceive().CancelTargeting();
        }

        [TestMethod]
        public void HandleInteraction_ClickingSelfPromote_DoesNothing()
        {
            // Arrange
            var card = TestData.Cards.CheapCard();
            _activePlayer.PlayedCards.Add(card);

            // Set credit coming ONLY from this card
            _realTurnContext.AddPromotionCredit(card, 1);

            // Mock hovering this card (Using Fake)
            _stateFake.HoveredPlayedCard = card;

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            // Act
            _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            CollectionAssert.Contains(_activePlayer.PlayedCards, card, "Card should remain in played pile (invalid target).");
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

            _activePlayer.PlayedCards.Add(sourceCard);
            _activePlayer.PlayedCards.Add(targetCard);

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
            CollectionAssert.DoesNotContain(_activePlayer.PlayedCards, targetCard, "Target should be removed from Played.");
            CollectionAssert.Contains(_activePlayer.InnerCircle, targetCard, "Target should be in Inner Circle.");
            Assert.AreEqual(0, _realTurnContext.PendingPromotionsCount, "Credit should be consumed.");

            // Verify EndTurn Command is returned
            Assert.IsInstanceOfType(resultCmd, typeof(EndTurnCommand), "Should return EndTurnCommand");
            _actionSub.Received().CancelTargeting();
        }
    }
}
