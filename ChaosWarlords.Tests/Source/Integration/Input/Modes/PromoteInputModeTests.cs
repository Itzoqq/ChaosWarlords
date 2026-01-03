using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;
using NSubstitute;

namespace ChaosWarlords.Tests.Integration.Input.Modes
{
    [TestClass]

    [TestCategory("Integration")]
    public class PromoteInputModeTests
    {
        private PromoteInputMode _inputMode = null!;
        private MockInputProvider _mockInput = null!;
        private IInputManager _inputManager = null!;
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
            _activePlayer = TestData.Players.RedPlayer();
            _realTurnContext = new TurnContext(_activePlayer, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            // Setup MatchContext hierarchy for the InputMode to access TurnContext
            var turnManagerSub = Substitute.For<ITurnManager>();
            turnManagerSub.CurrentTurnContext.Returns(_realTurnContext);
            turnManagerSub.ActivePlayer.Returns(_activePlayer);

            var matchContext = new MatchContext(
                turnManagerSub,
                _mapSub,
                _marketSub,
                _actionSub,
                Substitute.For<ICardDatabase>(),
                new PlayerStateManager(ChaosWarlords.Tests.Utilities.TestLogger.Instance),
                null, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            // Link MatchContext to State
            _stateSub.MatchContext.Returns(matchContext);
            _stateSub.TurnManager.Returns(turnManagerSub);
            _stateSub.Logger.Returns(ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            // CRITICAL FIX: Configure mock to actually execute commands!
            _stateSub.When(x => x.RecordAndExecuteCommand(Arg.Any<IGameCommand>()))
                     .Do(x => x.Arg<IGameCommand>().Execute(_stateSub));

            // Initialize Mode (Promote 1 card)
            _inputMode = new PromoteInputMode(_stateSub, _inputManager, _actionSub, 1);
        }

        [TestMethod]
        public void HandleInput_ClickingSelfPromote_DoesNothing()
        {
            // Arrange
            var card = TestData.Cards.CheapCard();
            _activePlayer.PlayedCards.Add(card);

            // Set credit coming ONLY from this card
            _realTurnContext.AddPromotionCredit(card, 1);

            // Mock hovering this card
            _stateSub.GetHoveredPlayedCard().Returns(card);

            // Simulate Left Click
            InputTestHelpers.SimulateLeftClick(_mockInput, _inputManager, 100, 100);

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
            var sourceCard = TestData.Cards.CheapCard();
            var targetCard = TestData.Cards.CheapCard();

            // Force unique IDs to prevent collision
            try { 
                typeof(Card).GetProperty("Id")?.SetValue(sourceCard, "ID_SOURCE");
                typeof(Card).GetProperty("Id")?.SetValue(targetCard, "ID_TARGET");
            } catch {}

            _activePlayer.PlayedCards.Add(sourceCard);
            _activePlayer.PlayedCards.Add(targetCard);

            // Credit comes from Source, so Target is valid
            _realTurnContext.AddPromotionCredit(sourceCard, 1);

            // Mock hovering target
            _stateSub.GetHoveredPlayedCard().Returns(targetCard);

            // Simulate Left Click
            InputTestHelpers.SimulateLeftClick(_mockInput, _inputManager, 100, 100);

            // Act
            var resultCmd = _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            // 1. Verify Command Execution
            _stateSub.Received(1).RecordAndExecuteCommand(Arg.Is<IGameCommand>(cmd => 
                (cmd as ChaosWarlords.Source.Commands.PromoteCommand) != null && 
                ((ChaosWarlords.Source.Commands.PromoteCommand)cmd).CardId == targetCard.Id));

            // Verify State
            Assert.DoesNotContain(targetCard, _activePlayer.PlayedCards, "Target should be removed from Played.");
            Assert.Contains(targetCard, _activePlayer.InnerCircle, "Target should be in Inner Circle.");
            Assert.AreEqual(0, _realTurnContext.PendingPromotionsCount, "Credit should be consumed.");

            // Verify EndTurn Command is returned
            Assert.IsInstanceOfType(resultCmd, typeof(ChaosWarlords.Source.Commands.EndTurnCommand), "Should return EndTurnCommand");
            _actionSub.Received().CancelTargeting();
        }


    }
}


