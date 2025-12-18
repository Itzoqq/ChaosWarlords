using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Tests.States.Input
{
    [TestClass]
    public class MarketInputModeTests
    {
        // ------------------------------------------------------------------------
        // 1. ROBUST MOCKS
        // ------------------------------------------------------------------------

        private class MockInputProvider : IInputProvider
        {
            public MouseState MouseState { get; set; }
            public KeyboardState KeyboardState { get; set; } = new KeyboardState();
            public MouseState GetMouseState() => MouseState;
            public KeyboardState GetKeyboardState() => KeyboardState;
        }

        private class MockMarketManager : IMarketManager
        {
            public List<Card> MarketRow { get; set; } = new List<Card>();

            // Verification Flag
            public bool UpdateCalled { get; private set; }

            public void Update(Vector2 mousePos)
            {
                UpdateCalled = true;
                // Simulate hover logic for tests
                foreach (var card in MarketRow)
                {
                    // Simple hit test for the mock
                    if (card.Bounds.Contains(mousePos))
                        card.IsHovered = true;
                    else
                        card.IsHovered = false;
                }
            }

            // Stubs
            public void BuyCard(Player p, Card c) { }
            public void RefillMarket(List<Card> deck) { }
            public bool TryBuyCard(Player player, Card card) => false;
        }

        private class MockUISystem : IUISystem
        {
            public bool IsMarketHovered { get; set; } = false;

            // Stubs
            public bool IsAssassinateHovered { get; set; } = false;
            public bool IsReturnSpyHovered { get; set; } = false;
            public int ScreenWidth { get; } = 800;
            public int ScreenHeight { get; } = 600;
            public Rectangle MarketButtonRect { get; } = Rectangle.Empty;
            public Rectangle AssassinateButtonRect { get; } = Rectangle.Empty;
            public Rectangle ReturnSpyButtonRect { get; } = Rectangle.Empty;

            public event EventHandler? OnMarketToggleRequest;
            public event EventHandler? OnAssassinateRequest;
            public event EventHandler? OnReturnSpyRequest;

            public void RaiseMarketToggle() => OnMarketToggleRequest?.Invoke(this, EventArgs.Empty);
            public void RaiseAssassinateRequest() => OnAssassinateRequest?.Invoke(this, EventArgs.Empty);
            public void RaiseReturnSpyRequest() => OnReturnSpyRequest?.Invoke(this, EventArgs.Empty);
            public void Update(InputManager input) { }
        }

        private class MockGameplayState : IGameplayState
        {
            public InputManager InputManager { get; }
            public IUISystem UIManager { get; }
            public IMapManager MapManager { get; }
            public IMarketManager MarketManager { get; }
            public IActionSystem ActionSystem { get; }
            public TurnManager TurnManager { get; }

            // State Flags for Verification
            public bool CloseMarketCalled { get; private set; }
            public bool IsMarketOpen { get; set; } = true;

            public MockGameplayState(InputManager input, IUISystem ui, IMapManager map, IMarketManager market, IActionSystem action, TurnManager turn)
            {
                InputManager = input; UIManager = ui; MapManager = map; MarketManager = market; ActionSystem = action; TurnManager = turn;
            }

            public void CloseMarket()
            {
                CloseMarketCalled = true;
                IsMarketOpen = false;
            }

            // Stubs
            public IInputMode InputMode { get; set; } = null!;
            public int HandY { get; } = 0;
            public int PlayedY { get; } = 0;
            public void PlayCard(Card card) { }
            public void SwitchToNormalMode() { }
            public void SwitchToTargetingMode() { }
            public void ToggleMarket() { }
            public void EndTurn() { }
            public void ResolveCardEffects(Card card) { }
            public void MoveCardToPlayed(Card card) { }
            public void ArrangeHandVisuals() { }
            public string GetTargetingText(ActionState state) => "";
            public void LoadContent() { }
            public void UnloadContent() { }
            public void Update(GameTime gameTime) { }
            public void Draw(SpriteBatch spriteBatch) { }
        }

        // Dummy Mocks for Dependency Injection
        private class MockMapManager : IMapManager
        {
            public IReadOnlyList<MapNode> Nodes { get; } = new List<MapNode>();
            public IReadOnlyList<Site> Sites { get; } = new List<Site>();
            public void CenterMap(int w, int h) { }
            public bool TryDeploy(Player p, MapNode n) => false;
            public Site GetSiteForNode(MapNode n) => null!;
            public MapNode GetNodeAt(Vector2 p) => null!;
            public Site GetSiteAt(Vector2 p) => null!;
            public void DistributeControlRewards(Player p) { }
            public List<PlayerColor> GetEnemySpiesAtSite(Site s, Player p) => new List<PlayerColor>();
        }

        private class MockActionSystem : IActionSystem
        {
            public ActionState CurrentState { get; set; } = ActionState.Normal;
            public Card? PendingCard { get; }
            public Site? PendingSite { get; }
            public event EventHandler? OnActionCompleted;
            public event EventHandler<string>? OnActionFailed;
            public void CancelTargeting() { }
            public void FinalizeSpyReturn(PlayerColor c) { }
            public void HandleTargetClick(MapNode n, Site s) { }
            public bool IsTargeting() => false;
            public void SetCurrentPlayer(Player p) { }
            public void StartTargeting(ActionState s, Card c) { }
            public void TryStartAssassinate() { }
            public void TryStartReturnSpy() { }

            // Add Raise methods to satisfy unused event warnings
            public void RaiseActionCompleted() => OnActionCompleted?.Invoke(this, EventArgs.Empty);
            public void RaiseActionFailed(string s) => OnActionFailed?.Invoke(this, s);
        }

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

            _mockState = new MockGameplayState(_inputManager, _mockUI, mockMap, _mockMarket, mockAction, turnManager);

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
            _mockInput.MouseState = new MouseState(50, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
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
            _mockInput.MouseState = new MouseState(110, 110, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();
            _mockInput.MouseState = new MouseState(110, 110, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
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
            _mockInput.MouseState = new MouseState(500, 500, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();
            _mockInput.MouseState = new MouseState(500, 500, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
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
            _mockInput.MouseState = new MouseState(10, 10, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();
            _mockInput.MouseState = new MouseState(10, 10, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();

            // 2. Act
            var result = _inputMode.HandleInput(_inputManager, _mockMarket, new MockMapManager(), _activePlayer, new MockActionSystem());

            // 3. Assert
            Assert.IsNull(result);
            Assert.IsFalse(_mockState.CloseMarketCalled, "Clicking the UI Button should NOT trigger CloseMarket() inside InputMode (UI handles button clicks).");
        }
    }
}