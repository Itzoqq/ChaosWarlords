using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;

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

        // We need to mock the CardDatabase to satisfy the MatchContext constructor
        private ICardDatabase _cardDbSub = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInput = new MockInputProvider();
            _inputManager = new InputManager(_mockInput);

            // 1. Create the mocks
            _marketSub = Substitute.For<IMarketManager>();
            _stateSub = Substitute.For<IGameplayState>();
            _mapSub = Substitute.For<IMapManager>();
            _actionSub = Substitute.For<IActionSystem>();
            _cardDbSub = Substitute.For<ICardDatabase>(); // Create the new mock

            // Mock the TurnManager (since it's cast to TurnManager in some places, 
            // ideally we use the Interface, but let's stick to your existing pattern)
            // If your test uses ITurnManager, keep using Substitute.For<ITurnManager>()
            var turnSub = Substitute.For<ITurnManager>();

            _mockUI = new MockUISystem();

            // 2. Setup the State to return our Mock UI 
            // (This is crucial if you used Fix #1: accessing UI via state.UIManager)
            _stateSub.UIManager.Returns(_mockUI);

            // 3. Create the MatchContext using our Mocks
            // Note: We use the real concrete MatchContext, but inject mocked systems into it.
            var context = new MatchContext(
                turnSub,
                _mapSub,
                _marketSub,
                _actionSub,
                _cardDbSub
            );

            // 4. Instantiate MarketInputMode with the new signature
            // (State, Input, Context)
            _inputMode = new MarketInputMode(_stateSub, _inputManager, context);

            // Setup active player dummy
            _activePlayer = new Player(PlayerColor.Red);
        }

        [TestMethod]
        public void HandleInput_ClickingCard_ReturnsBuyCardCommand()
        {
            // 1. Arrange
            var card = new Card("market_card", "Buy Me", 3, CardAspect.Order, 1, 0, 0);

            // Mock the State to say "Yes, the mouse is hovering this card"
            _stateSub.GetHoveredMarketCard().Returns(card);

            // Simulate Click
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
            // Mock State to say "Nothing is hovered"
            _stateSub.GetHoveredMarketCard().Returns((Card?)null);

            // Simulate Click
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

            // Simulate Click
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