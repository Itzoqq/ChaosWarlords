using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.States.Input
{
    [TestClass]
    public class MarketInputModeTests
    {
        // ------------------------------------------------------------------------
        // 2. TEST SETUP
        // ------------------------------------------------------------------------

        private MarketInputMode _inputMode = null!;
        private MockInputProvider _mockInput = null!;
        private InputManager _inputManager = null!;
        private MockMarketManager _mockMarket = null!;
        private MockUISystem _mockUI = null!;
        private MockGameplayState _mockState = null!;
        private Player _activePlayer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInput = new MockInputProvider();
            _inputManager = new InputManager(_mockInput);
            _mockMarket = new MockMarketManager();
            _mockUI = new MockUISystem();
            _activePlayer = new Player(PlayerColor.Red);

            // Create dependencies not used by this mode but required for State Construction
            var mockMap = new MockMapManager();
            var mockAction = new MockActionSystem();
            var turnManager = new TurnManager(new List<Player> { _activePlayer });

            _mockState = new MockGameplayState(mockAction);
            _mockState.InputManager = _inputManager;
            _mockState.UIManager = _mockUI;
            _mockState.MapManager = mockMap;
            _mockState.MarketManager = _mockMarket;
            _mockState.TurnManager = turnManager;
            _mockState.IsMarketOpen = true; // Default for Market Mode tests

            // Create the mode under test
            _inputMode = new MarketInputMode(
                _mockState,
                _inputManager,
                _mockUI,
                _mockMarket,
                turnManager
            );
        }

        // ------------------------------------------------------------------------
        // 3. UNIT TESTS
        // ------------------------------------------------------------------------

        [TestMethod]
        public void HandleInput_UpdatesMarketManager()
        {
            // 1. Arrange
            _mockInput.SetMouseState(new MouseState(50, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            // 2. Act
            _inputMode.HandleInput(_inputManager, _mockMarket, new MockMapManager(), _activePlayer, new MockActionSystem());

            // 3. Assert
            Assert.IsTrue(_mockMarket.UpdateCalled, "MarketManager.Update() must be called every frame to handle hover effects.");
        }

        [TestMethod]
        public void HandleInput_ClickingCard_ReturnsBuyCardCommand()
        {
            // 1. Arrange
            // Add a card to the market at (100, 100)
            var card = new Card("market_card", "Buy Me", 3, CardAspect.Order, 1, 0);
            card.Position = new Vector2(100, 100);
            _mockMarket.MarketRow.Add(card);

            // Simulate Click at (110, 110) - Inside Card
            _mockInput.SetMouseState(new MouseState(110, 110, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(110, 110, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            // 2. Act
            var result = _inputMode.HandleInput(_inputManager, _mockMarket, new MockMapManager(), _activePlayer, new MockActionSystem());

            // 3. Assert
            Assert.IsNotNull(result, "Clicking a market card should return a command.");
            Assert.IsInstanceOfType(result, typeof(BuyCardCommand), "Should return a BuyCardCommand.");

            // Verify the card is hovered (Mock Logic Check)
            Assert.IsTrue(card.IsHovered, "Card should be hovered when clicked.");
        }

        [TestMethod]
        public void HandleInput_ClickingEmptySpace_ClosesMarket()
        {
            // 1. Arrange
            _mockMarket.MarketRow.Clear(); // Empty market

            // Click at (500, 500) - Empty Space
            _mockInput.SetMouseState(new MouseState(500, 500, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(500, 500, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            // 2. Act
            var result = _inputMode.HandleInput(_inputManager, _mockMarket, new MockMapManager(), _activePlayer, new MockActionSystem());

            // 3. Assert
            Assert.IsNull(result, "Clicking empty space should return null (Action is handled via state method).");
            Assert.IsTrue(_mockState.CloseMarketCalled, "Clicking empty space should close the market menu.");
        }

        [TestMethod]
        public void HandleInput_ClickingMarketButton_DoesNotCloseMarket()
        {
            // 1. Arrange
            // The UI Manager says we are hovering the button (e.g. the 'Close' or 'Toggle' button)
            _mockUI.IsMarketHovered = true;

            // Click at (10, 10) - Assume this is where the button is
            _mockInput.SetMouseState(new MouseState(10, 10, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(10, 10, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            // 2. Act
            var result = _inputMode.HandleInput(_inputManager, _mockMarket, new MockMapManager(), _activePlayer, new MockActionSystem());

            // 3. Assert
            Assert.IsNull(result);
            Assert.IsFalse(_mockState.CloseMarketCalled, "Clicking the UI Button should NOT trigger CloseMarket() inside InputMode (UI handles button clicks).");
        }
    }
}