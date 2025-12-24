using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;
using NSubstitute;

namespace ChaosWarlords.Tests.States.Input
{
    [TestClass]
    public class PromoteInputModeTests
    {
        private PromoteInputMode _inputMode = null!;
        private MockInputProvider _mockInput = null!;
        private InputManager _inputManager = null!;
        private IGameplayState _stateSub = null!;
        private IActionSystem _actionSub = null!;
        private Player _activePlayer = null!;
        private TurnContext _realTurnContext = null!;

        // Dummy dependencies required for HandleInput signature
        private IMarketManager _marketSub = null!;
        private IMapManager _mapSub = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInput = new MockInputProvider();
            _inputManager = new InputManager(_mockInput);

            _stateSub = Substitute.For<IGameplayState>();
            _actionSub = Substitute.For<IActionSystem>();
            _marketSub = Substitute.For<IMarketManager>();
            _mapSub = Substitute.For<IMapManager>();

            // Setup Player and Context
            _activePlayer = new Player(PlayerColor.Red);
            _realTurnContext = new TurnContext(_activePlayer);

            // Setup MatchContext hierarchy for the InputMode to access TurnContext
            var turnManagerSub = Substitute.For<ITurnManager>();
            turnManagerSub.CurrentTurnContext.Returns(_realTurnContext);
            turnManagerSub.ActivePlayer.Returns(_activePlayer);

            var matchContext = new MatchContext(
                turnManagerSub,
                _mapSub,
                _marketSub,
                _actionSub,
                Substitute.For<ICardDatabase>()
            );

            // Link MatchContext to State
            _stateSub.MatchContext.Returns(matchContext);

            // Initialize Mode (Promote 1 card)
            _inputMode = new PromoteInputMode(_stateSub, _inputManager, _actionSub, 1);
        }

        [TestMethod]
        public void HandleInput_ClickingSelfPromote_DoesNothing()
        {
            // Arrange
            var card = new Card("A", "Promoter", 1, CardAspect.Order, 0, 0, 0);
            _activePlayer.PlayedCards.Add(card);

            // Set credit coming ONLY from this card
            _realTurnContext.AddPromotionCredit(card, 1);

            // Mock hovering this card
            _stateSub.GetHoveredPlayedCard().Returns(card);

            // Simulate Left Click
            SimulateLeftClick();

            // Act
            _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            Assert.Contains(card, _activePlayer.PlayedCards, "Card should remain in played pile (invalid target).");
            Assert.AreEqual(1, _realTurnContext.PendingPromotionsCount, "Credit should not be consumed.");
            _stateSub.DidNotReceive().EndTurn();
        }

        [TestMethod]
        public void HandleInput_ClickingValidTarget_PromotesAndEndsTurn()
        {
            // Arrange
            var sourceCard = new Card("A", "Source", 1, CardAspect.Order, 0, 0, 0);
            var targetCard = new Card("B", "Target", 1, CardAspect.Order, 0, 0, 0);

            _activePlayer.PlayedCards.Add(sourceCard);
            _activePlayer.PlayedCards.Add(targetCard);

            // Credit comes from Source, so Target is valid
            _realTurnContext.AddPromotionCredit(sourceCard, 1);

            // Mock hovering target
            _stateSub.GetHoveredPlayedCard().Returns(targetCard);

            // Simulate Left Click
            SimulateLeftClick();

            // Act
            _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            Assert.DoesNotContain(targetCard, _activePlayer.PlayedCards, "Target should be removed from Played.");
            Assert.Contains(targetCard, _activePlayer.InnerCircle, "Target should be in Inner Circle.");
            Assert.AreEqual(0, _realTurnContext.PendingPromotionsCount, "Credit should be consumed.");

            // Verify EndTurn was called since we promoted the 1 required card
            _stateSub.Received().EndTurn();
            _actionSub.Received().CancelTargeting();
        }

        private void SimulateLeftClick()
        {
            _mockInput.SetMouseState(new MouseState(100, 100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(100, 100, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
        }
    }
}