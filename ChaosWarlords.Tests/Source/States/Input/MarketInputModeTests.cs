using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Source.Commands;

namespace ChaosWarlords.Tests.States.Input
{
    [TestClass]
    public class MarketInputModeTests
    {
        private MarketInputMode _inputMode = null!;
        private MockInputProvider _mockInput = null!;
        private InputManager _inputManager = null!;
        private IMarketManager _marketSub = null!;
        private MockUISystem _mockUI = null!;
        private Player _activePlayer = null!;
        private IGameplayState _stateSub = null!;
        private IMapManager _mapSub = null!;
        private IActionSystem _actionSub = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInput = new MockInputProvider();
            _inputManager = new InputManager(_mockInput);

            _marketSub = Substitute.For<IMarketManager>();
            _stateSub = Substitute.For<IGameplayState>();
            _mapSub = Substitute.For<IMapManager>();
            _actionSub = Substitute.For<IActionSystem>();

            _mockUI = new MockUISystem();
            _activePlayer = new Player(PlayerColor.Red);
            var turnManager = new TurnManager(new List<Player> { _activePlayer });

            _stateSub.IsMarketOpen.Returns(true);

            _inputMode = new MarketInputMode(
                _stateSub,
                _inputManager,
                _mockUI,
                _marketSub,
                turnManager
            );
        }

        [TestMethod]
        public void HandleInput_UpdatesMarketManager()
        {
            // 1. Arrange
            _mockInput.SetMouseState(new MouseState(50, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            // 2. Act
            _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            // 3. Assert
            // FIX: The interface takes Vector2, not InputManager
            _marketSub.Received(1).Update(Arg.Any<Vector2>());
        }

        [TestMethod]
        public void HandleInput_ClickingCard_ReturnsBuyCardCommand()
        {
            // 1. Arrange
            var card = new Card("market_card", "Buy Me", 3, CardAspect.Order, 1, 0);
            card.Position = new Vector2(100, 100);

            card.IsHovered = true;

            _marketSub.MarketRow.Returns(new List<Card> { card });

            _mockInput.SetMouseState(new MouseState(110, 110, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(110, 110, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

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
            _marketSub.MarketRow.Returns(new List<Card>());

            _mockInput.SetMouseState(new MouseState(10, 10, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(10, 10, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            // 2. Act
            var result = _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            // 3. Assert
            Assert.IsNull(result);
            _stateSub.Received(1).CloseMarket();
        }

        [TestMethod]
        public void HandleInput_ClickingMarketButton_DoesNotCloseMarket()
        {
            _mockUI.IsMarketHovered = true;

            _mockInput.SetMouseState(new MouseState(10, 10, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(10, 10, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            var result = _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            Assert.IsNull(result);
            _stateSub.DidNotReceive().CloseMarket();
        }
    }
}