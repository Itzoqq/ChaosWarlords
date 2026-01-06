using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Integration.Input.Modes
{
    [TestClass]
    [TestCategory("Integration")]
    public class MarketInputModeTests
    {
        private MarketInputMode _inputMode = null!;
        private MockInputProvider _mockInput = null!;
        private IInputManager _inputManager = null!;

        // Concrete Fake
        private TestGameplayState _stateFake = null!;

        // Substitutes
        private IMarketManager _marketSub = null!;
        private IUIManager _mockUI = null!;
        private Player _activePlayer = null!;
        private IMapManager _mapSub = null!;
        private IActionSystem _actionSub = null!;
        private ICardDatabase _cardDbSub = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInput = new MockInputProvider();
            _inputManager = new InputManager(_mockInput);

            _marketSub = Substitute.For<IMarketManager>();
            _mapSub = Substitute.For<IMapManager>();
            _actionSub = Substitute.For<IActionSystem>();
            _cardDbSub = Substitute.For<ICardDatabase>();
            _mockUI = Substitute.For<IUIManager>();

            var turnSub = Substitute.For<ITurnManager>();
            _activePlayer = TestData.Players.RedPlayer();

            // Initialize Fake State
            _stateFake = new TestGameplayState
            {
                MapManager = _mapSub,
                TurnManager = turnSub,
                ActionSystem = _actionSub,
                MarketManager = _marketSub,
                UIManager = _mockUI,
                MatchContext = new MatchContext(
                    turnSub,
                    _mapSub,
                    _marketSub,
                    _actionSub,
                    _cardDbSub,
                    new PlayerStateManager(ChaosWarlords.Tests.Utilities.TestLogger.Instance),
                    null, ChaosWarlords.Tests.Utilities.TestLogger.Instance)
            };
            
            // Ensure IsMarketOpen starts true for market tests if needed, or default false
            _stateFake.MarketStateManager.OpenForBrowsing(); 

            _inputMode = new MarketInputMode(_stateFake, _inputManager, _stateFake.MatchContext);

            // Pump update loop to clear startup cooldown (COOLDOWN_FRAMES)
            for (int i = 0; i < 10; i++)
            {
                _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);
            }
        }

        [TestMethod]
        public void HandleInput_ClickingCard_ReturnsBuyCardCommand()
        {
            // 1. Arrange
            var card = TestData.Cards.PowerCard();

            // Mock the State to say "Yes, the mouse is hovering this card"
            _stateFake.HoveredMarketCard = card;

            // Simulate Click
            InputTestHelpers.SimulateLeftClick(_mockInput, _inputManager, 110, 110);

            // 2. Act
            var result = _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            // 3. Assert
            Assert.IsNotNull(result, "Clicking a market card should return a command.");
            Assert.IsInstanceOfType(result, typeof(BuyCardCommand), "Should return a BuyCardCommand.");
        }

        [TestMethod]
        public void HandleInput_ClickingEmptySpace_ClosesMarket()
        {
            // 1. Arrange
            // Mock State to say "Nothing is hovered"
            _stateFake.HoveredMarketCard = null;
            _stateFake.MarketStateManager.OpenForBrowsing(); // Ensure it's open initially

            // Simulate Click
            InputTestHelpers.SimulateLeftClick(_mockInput, _inputManager, 10, 10);

            // 2. Act
            var result = _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            // 3. Assert
            Assert.IsNull(result);
            Assert.IsFalse(_stateFake.IsMarketOpen, "Market should be closed after clicking empty space.");
        }

        [TestMethod]
        public void HandleInput_ClickingMarketButton_DoesNotCloseMarket()
        {
            _mockUI.IsMarketHovered.Returns(true);
            _stateFake.MarketStateManager.OpenForBrowsing();

            // Simulate Click
            InputTestHelpers.SimulateLeftClick(_mockInput, _inputManager, 10, 10);

            var result = _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            Assert.IsNull(result);
            Assert.IsTrue(_stateFake.IsMarketOpen, "Market should remain open if UI button is clicked.");
        }
        
        [TestMethod]
        public void HandleInput_WithCallback_InvokesCallback_WhenCardClicked()
        {
            // 1. Arrange
            Card? callbackInvokedCard = null;
            Func<Card, IGameCommand?> onCardSelected = (c) => { callbackInvokedCard = c; return null; };

            // Set up state with callback
            _stateFake.MarketStateManager.OpenForDevour(onCardSelected);

            // Re-initialize (parameterless)
            // Re-initialize (parameterless)
            _inputMode = new MarketInputMode(_stateFake, _inputManager, _stateFake.MatchContext);
            for(int i=0; i<10; i++) _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            var card = TestData.Cards.PowerCard();
            _stateFake.HoveredMarketCard = card;

            // Simulate Click
            InputTestHelpers.SimulateLeftClick(_mockInput, _inputManager, 110, 110);

            // 2. Act
            var result = _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            // 3. Assert
            Assert.IsNull(result, "Should return null command when callback is handled.");
            Assert.AreEqual(card, callbackInvokedCard, "The callback should be invoked with the clicked card.");
        }
    }
}
