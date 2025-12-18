using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
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
    public class NormalPlayInputModeTests
    {
        // --- 1. Robust Mocks ---

        private class MockInputProvider : IInputProvider
        {
            public MouseState MouseState { get; set; }
            public KeyboardState KeyboardState { get; set; } = new KeyboardState();
            public MouseState GetMouseState() => MouseState;
            public KeyboardState GetKeyboardState() => KeyboardState;
        }

        private class MockMapManager : IMapManager
        {
            // Test Control Properties
            public MapNode? NodeToReturn { get; set; }
            public bool TryDeployCalled { get; private set; }
            public MapNode? DeployTarget { get; private set; }

            // Interface Implementation
            public IReadOnlyList<MapNode> Nodes { get; } = new List<MapNode>();
            public IReadOnlyList<Site> Sites { get; } = new List<Site>();

            // Valid Logic (Used by Test)
            public MapNode GetNodeAt(Vector2 position) => NodeToReturn!; // ! suppresses warning if null, which is fine for "No Node" logic

            public bool TryDeploy(Player currentPlayer, MapNode targetNode)
            {
                TryDeployCalled = true;
                DeployTarget = targetNode;
                return true; // Return true to simulate success
            }

            // Stubs (Not used in this specific test, safe to be empty or throw if really ensuring isolation)
            public Site GetSiteAt(Vector2 position) => null!;
            public Site GetSiteForNode(MapNode node) => null!;
            public void CenterMap(int width, int height) { }
            public void DistributeControlRewards(Player active) { }
            public List<PlayerColor> GetEnemySpiesAtSite(Site site, Player active) => new List<PlayerColor>();
        }

        private class MockActionSystem : IActionSystem
        {
            public ActionState CurrentState { get; set; } = ActionState.Normal;
            public Card? PendingCard { get; }
            public Site? PendingSite { get; }

            public event EventHandler? OnActionCompleted;
            public event EventHandler<string>? OnActionFailed;

            public void RaiseActionCompleted() => OnActionCompleted?.Invoke(this, EventArgs.Empty);
            public void RaiseActionFailed(string reason) => OnActionFailed?.Invoke(this, reason);

            // Stubs
            public void CancelTargeting() { }
            public void FinalizeSpyReturn(PlayerColor spyColor) { }
            public void HandleTargetClick(MapNode node, Site site) { }
            public bool IsTargeting() => false;
            public void SetCurrentPlayer(Player p) { }
            public void StartTargeting(ActionState state, Card card) { }
            public void TryStartAssassinate() { }
            public void TryStartReturnSpy() { }
        }

        private class MockUISystem : IUISystem
        {
            // Inside MockUISystem
            public bool IsMarketHovered { get; set; } = false;
            public bool IsAssassinateHovered { get; set; } = false;
            public bool IsReturnSpyHovered { get; set; } = false;
            public int ScreenWidth { get; }
            public int ScreenHeight { get; }

            // Initialized to Empty to prevent NullRef if accessed
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

        private class MockMarketManager : IMarketManager
        {
            public List<Card> MarketRow { get; } = new List<Card>();
            public void Update(Vector2 mousePos) { }
            public void BuyCard(Player p, Card c) { }
            public void RefillMarket(List<Card> deck) { }
            public bool TryBuyCard(Player player, Card card) => false;
        }

        // --- THE FIX: Correct MockGameplayState ---
        private class MockGameplayState : IGameplayState
        {
            // Properties match Interface (Non-Nullable)
            public InputManager InputManager { get; }
            public IUISystem UIManager { get; }
            public IMapManager MapManager { get; }
            public IMarketManager MarketManager { get; }
            public IActionSystem ActionSystem { get; }
            public TurnManager TurnManager { get; }

            public IInputMode InputMode { get; set; } = null!; // Set late or allowed to be null in test setup
            public bool IsMarketOpen { get; set; }
            public int HandY { get; } = 500;
            public int PlayedY { get; } = 400;

            // Constructor Injection: This is how you fix the "Non-nullable property" warning correctly.
            public MockGameplayState(
                InputManager input,
                IUISystem ui,
                IMapManager map,
                IMarketManager market,
                IActionSystem action,
                TurnManager turn)
            {
                InputManager = input;
                UIManager = ui;
                MapManager = map;
                MarketManager = market;
                ActionSystem = action;
                TurnManager = turn;
            }

            // Stubs for interface methods
            public void PlayCard(Card card) { }
            public void SwitchToNormalMode() { }
            public void SwitchToTargetingMode() { }
            public void ToggleMarket() { }
            public void CloseMarket() { }
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

        // --- Test Setup ---

        private NormalPlayInputMode _inputMode = null!;
        private MockInputProvider _mockInput = null!;
        private InputManager _inputManager = null!;
        private MockMapManager _mockMap = null!;
        private MockActionSystem _mockAction = null!;
        private MockUISystem _mockUI = null!;
        private MockMarketManager _mockMarket = null!;
        private TurnManager _turnManager = null!;
        private Player _activePlayer = null!;

        [TestInitialize]
        public void Setup()
        {
            // 1. Create Core Mocks
            _mockInput = new MockInputProvider();
            _inputManager = new InputManager(_mockInput);
            _mockMap = new MockMapManager();
            _mockAction = new MockActionSystem();
            _mockUI = new MockUISystem();
            _mockMarket = new MockMarketManager();

            _activePlayer = new Player(PlayerColor.Red);
            _turnManager = new TurnManager(new List<Player> { _activePlayer });

            // 2. Create the Mock State injecting the SAME mocks
            // This ensures that if the code accesses state.MapManager, it gets _mockMap
            var mockState = new MockGameplayState(
                _inputManager,
                _mockUI,
                _mockMap,
                _mockMarket,
                _mockAction,
                _turnManager
            );

            // 3. Initialize the Mode under test
            _inputMode = new NormalPlayInputMode(
                mockState,
                _inputManager,
                _mockUI,
                _mockMap,
                _turnManager,
                _mockAction
            );
        }

        // --- Tests ---

        [TestMethod]
        public void HandleInput_ClickOnCard_ReturnsPlayCardCommand()
        {
            // 1. Arrange
            var card = new Card("test", "Test Soldier", 0, CardAspect.Neutral, 0, 0);
            card.Position = new Vector2(100, 100);
            _activePlayer.Hand.Add(card);

            // Frame 1: Released
            _mockInput.MouseState = new MouseState(110, 110, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();

            // Frame 2: Pressed (Click)
            _mockInput.MouseState = new MouseState(110, 110, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();

            // 2. Act
            var result = _inputMode.HandleInput(
                _inputManager,
                _mockMarket,
                _mockMap,
                _activePlayer,
                _mockAction
            );

            // 3. Assert
            Assert.IsNotNull(result, "Clicking a card should return a command.");
            Assert.IsInstanceOfType(result, typeof(PlayCardCommand), "Should return a PlayCardCommand.");
        }

        [TestMethod]
        public void HandleInput_ClickOnMapNode_DeploysTroop()
        {
            // 1. Arrange
            _activePlayer.Hand.Clear();

            // Setup Map Mock to return a node
            var targetNode = new MapNode(1, new Vector2(200, 200));
            _mockMap.NodeToReturn = targetNode;

            // Frame 1: Released
            _mockInput.MouseState = new MouseState(200, 200, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();

            // Frame 2: Pressed
            _mockInput.MouseState = new MouseState(200, 200, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();

            // 2. Act
            var result = _inputMode.HandleInput(
                _inputManager,
                _mockMarket,
                _mockMap,
                _activePlayer,
                _mockAction
            );

            // 3. Assert
            Assert.IsNull(result, "Map interaction should not return a generic command.");
            Assert.IsTrue(_mockMap.TryDeployCalled, "MapManager.TryDeploy should have been called.");
            Assert.AreEqual(targetNode, _mockMap.DeployTarget);
        }

        [TestMethod]
        public void HandleInput_CardOverlapsNode_CardTakesPriority()
        {
            // 1. Arrange
            // Position Card at (100, 100)
            var card = new Card("test", "Test Soldier", 0, CardAspect.Neutral, 0, 0);
            card.Position = new Vector2(100, 100);
            _activePlayer.Hand.Add(card);

            // Position Node at (100, 100) as well (Visual Overlap)
            var node = new MapNode(1, new Vector2(100, 100));
            _mockMap.NodeToReturn = node;

            // Simulate Click exactly at (110, 110) where both exist
            _mockInput.MouseState = new MouseState(110, 110, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update(); // Frame 1
            _mockInput.MouseState = new MouseState(110, 110, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update(); // Frame 2

            // 2. Act
            var result = _inputMode.HandleInput(
                _inputManager,
                _mockMarket,
                _mockMap,
                _activePlayer,
                _mockAction
            );

            // 3. Assert
            Assert.IsNotNull(result, "Input should handle the Card, returning a command.");
            Assert.IsInstanceOfType(result, typeof(PlayCardCommand), "Card interaction must take priority over Map interaction.");
            Assert.IsFalse(_mockMap.TryDeployCalled, "Map deployment should NOT trigger when clicking a card.");
        }

        [TestMethod]
        public void HandleInput_ClickEmptySpace_ReturnsNull()
        {
            // 1. Arrange
            _activePlayer.Hand.Clear();
            _mockMap.NodeToReturn = null; // No node at click location

            // Click at arbitrary location (500, 500)
            _mockInput.MouseState = new MouseState(500, 500, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();
            _mockInput.MouseState = new MouseState(500, 500, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();

            // 2. Act
            var result = _inputMode.HandleInput(
                _inputManager,
                _mockMarket,
                _mockMap,
                _activePlayer,
                _mockAction
            );

            // 3. Assert
            Assert.IsNull(result, "Clicking empty space should return null.");
            Assert.IsFalse(_mockMap.TryDeployCalled, "Should not attempt deploy if no node is clicked.");
        }
    }
}