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
    public class TargetingInputModeTests
    {
        // ------------------------------------------------------------------------
        // 1. ROBUST MOCKS (Updated with Setters and Event Raisers)
        // ------------------------------------------------------------------------

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
            public Site? SiteToReturn { get; set; }

            public List<PlayerColor> SpiesToReturn { get; set; } = new List<PlayerColor>();

            // Verification Flags
            public bool TryDeployCalled { get; private set; }

            // Interface Implementation
            public IReadOnlyList<MapNode> Nodes { get; } = new List<MapNode>();
            public IReadOnlyList<Site> Sites { get; } = new List<Site>();

            public MapNode GetNodeAt(Vector2 position) => NodeToReturn!;
            public Site GetSiteAt(Vector2 position) => SiteToReturn!;

            // Logic needed for Spy Selection Test
            public List<PlayerColor> GetEnemySpiesAtSite(Site site, Player active)
            {
                return SpiesToReturn;
            }

            public bool TryDeploy(Player currentPlayer, MapNode targetNode)
            {
                TryDeployCalled = true;
                return true;
            }

            // Stubs
            public Site GetSiteForNode(MapNode node) => null!;
            public void CenterMap(int width, int height) { }
            public void DistributeControlRewards(Player active) { }
        }

        private class MockActionSystem : IActionSystem
        {
            // Test Controls
            public ActionState CurrentState { get; set; } = ActionState.Normal;
            public Card? PendingCard { get; set; } // Settable for tests
            public Site? PendingSite { get; set; } // Settable for spy selection

            // Verification Flags
            public bool CancelTargetingCalled { get; private set; }
            public bool HandleTargetClickCalled { get; private set; }
            public MapNode? ClickedNode { get; private set; }
            public Site? ClickedSite { get; private set; }
            public PlayerColor? FinalizedSpyColor { get; private set; }

            public event EventHandler? OnActionCompleted;
            public event EventHandler<string>? OnActionFailed;

            // Interface Implementation
            public void CancelTargeting()
            {
                CancelTargetingCalled = true;
                CurrentState = ActionState.Normal; // Simulate reset
            }

            public void HandleTargetClick(MapNode node, Site site)
            {
                HandleTargetClickCalled = true;
                ClickedNode = node;
                ClickedSite = site;
            }

            public void FinalizeSpyReturn(PlayerColor spyColor)
            {
                FinalizedSpyColor = spyColor;
            }

            public void RaiseActionCompleted() => OnActionCompleted?.Invoke(this, EventArgs.Empty);
            public void RaiseActionFailed(string reason) => OnActionFailed?.Invoke(this, reason);

            // Stubs
            public bool IsTargeting() => CurrentState != ActionState.Normal;
            public void SetCurrentPlayer(Player p) { }
            public void StartTargeting(ActionState state, Card card) { CurrentState = state; PendingCard = card; }
            public void TryStartAssassinate() { }
            public void TryStartReturnSpy() { }
        }

        private class MockUISystem : IUISystem
        {
            // Settable for Tests
            public bool IsMarketHovered { get; set; } = false;
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

        private class MockMarketManager : IMarketManager
        {
            public List<Card> MarketRow { get; } = new List<Card>();
            public void Update(Vector2 mousePos) { }
            public void BuyCard(Player p, Card c) { }
            public void RefillMarket(List<Card> deck) { }
            public bool TryBuyCard(Player player, Card card) => false;
        }

        private class MockGameplayState : IGameplayState
        {
            public InputManager InputManager { get; }
            public IUISystem UIManager { get; }
            public IMapManager MapManager { get; }
            public IMarketManager MarketManager { get; }
            public IActionSystem ActionSystem { get; }
            public TurnManager TurnManager { get; }

            public IInputMode InputMode { get; set; } = null!;
            public bool IsMarketOpen { get; set; }
            public int HandY { get; } = 500;
            public int PlayedY { get; } = 400;

            public MockGameplayState(InputManager input, IUISystem ui, IMapManager map, IMarketManager market, IActionSystem action, TurnManager turn)
            {
                InputManager = input; UIManager = ui; MapManager = map; MarketManager = market; ActionSystem = action; TurnManager = turn;
            }

            public void SwitchToNormalMode() { } // Stub

            // Stubs
            public void PlayCard(Card card) { }
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

        // ------------------------------------------------------------------------
        // 2. TEST SETUP
        // ------------------------------------------------------------------------

        private TargetingInputMode _inputMode = null!;
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
            _mockInput = new MockInputProvider();
            _inputManager = new InputManager(_mockInput);
            _mockMap = new MockMapManager();
            _mockAction = new MockActionSystem();
            _mockUI = new MockUISystem();
            _mockMarket = new MockMarketManager();

            _activePlayer = new Player(PlayerColor.Red);
            _turnManager = new TurnManager(new List<Player> { _activePlayer });

            var mockState = new MockGameplayState(_inputManager, _mockUI, _mockMap, _mockMarket, _mockAction, _turnManager);

            // Create the mode under test
            _inputMode = new TargetingInputMode(
                mockState,
                _inputManager,
                _mockUI,
                _mockMap,
                _turnManager,
                _mockAction
            );
        }

        // ------------------------------------------------------------------------
        // 3. UNIT TESTS
        // ------------------------------------------------------------------------

        [TestMethod]
        public void HandleInput_SafetyCheck_IfActionStateIsNormal_ReturnsSwitchCommand()
        {
            // 1. Arrange
            // The Action System somehow reset to Normal, but we are still in TargetingInputMode
            _mockAction.CurrentState = ActionState.Normal;

            // 2. Act
            var result = _inputMode.HandleInput(_inputManager, _mockMarket, _mockMap, _activePlayer, _mockAction);

            // 3. Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(SwitchToNormalModeCommand),
                "If logic state desyncs (is Normal), input mode should force a switch back to Normal.");
        }

        [TestMethod]
        public void HandleInput_RightClick_CancelsTargeting_AndReturnsSwitchCommand()
        {
            // 1. Arrange
            _mockAction.CurrentState = ActionState.TargetingAssassinate;

            // Simulate Right Click
            _mockInput.MouseState = new MouseState(100, 100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update(); // Frame 1
            _mockInput.MouseState = new MouseState(100, 100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
            _inputManager.Update(); // Frame 2: Right Clicked

            // 2. Act
            var result = _inputMode.HandleInput(_inputManager, _mockMarket, _mockMap, _activePlayer, _mockAction);

            // 3. Assert
            Assert.IsTrue(_mockAction.CancelTargetingCalled, "Right clicking should invoke CancelTargeting() on the backend.");
            Assert.IsInstanceOfType(result, typeof(SwitchToNormalModeCommand), "Right click should return a command to switch state.");
        }

        [TestMethod]
        public void HandleInput_UIBlocking_IfMarketHovered_DoesNothing()
        {
            // 1. Arrange
            _mockAction.CurrentState = ActionState.TargetingPlaceSpy;
            _mockUI.IsMarketHovered = true; // User is hovering over the Market UI

            // Simulate a Left Click (which would normally fire a target action)
            _mockInput.MouseState = new MouseState(100, 100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();
            _mockInput.MouseState = new MouseState(100, 100, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();

            // 2. Act
            var result = _inputMode.HandleInput(_inputManager, _mockMarket, _mockMap, _activePlayer, _mockAction);

            // 3. Assert
            Assert.IsNull(result, "Clicking while UI is hovered should do nothing.");
            Assert.IsFalse(_mockAction.HandleTargetClickCalled, "Should NOT process target clicks through UI.");
        }

        [TestMethod]
        public void HandleInput_ValidTargetClick_CallsSystemHandler()
        {
            // 1. Arrange
            _mockAction.CurrentState = ActionState.TargetingAssassinate;

            // Setup a Map Node at (200, 200)
            var node = new MapNode(1, new Vector2(200, 200));
            _mockMap.NodeToReturn = node;

            // Click at (200, 200)
            _mockInput.MouseState = new MouseState(200, 200, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();
            _mockInput.MouseState = new MouseState(200, 200, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();

            // 2. Act
            _inputMode.HandleInput(_inputManager, _mockMarket, _mockMap, _activePlayer, _mockAction);

            // 3. Assert
            Assert.IsTrue(_mockAction.HandleTargetClickCalled, "Clicking a valid node should pass data to ActionSystem.");
            Assert.AreEqual(node, _mockAction.ClickedNode);
        }

        [TestMethod]
        public void HandleInput_SelectingSpyToReturn_ClickingSpyButton_FinalizesAction()
        {
            // 1. Arrange
            _mockAction.CurrentState = ActionState.SelectingSpyToReturn;

            // FIX: Use the correct constructor (Name, ControlType, ControlAmt, TotalType, TotalAmt)
            var site = new Site("Test Site", ResourceType.Power, 1, ResourceType.Influence, 1);

            // FIX: Use Reflection to set the private 'Bounds' property
            // We need the site to be at (100, 100) so we can predict where the "Return Spy" button appears.
            typeof(Site).GetProperty("Bounds")?.SetValue(site, new Rectangle(100, 100, 100, 100));

            _mockAction.PendingSite = site;
            _mockMap.SpiesToReturn = new List<PlayerColor> { PlayerColor.Blue };

            // Logic in TargetingInputMode: Button is at (Site.X, Site.Y - 50) -> (100, 50)
            // Button size is 50x40. Center click = (125, 70).
            int clickX = 110;
            int clickY = 60; // 50 (start Y) + 10 (offset into button)

            // Simulate Click on the spy button
            _mockInput.MouseState = new MouseState(clickX, clickY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();
            _mockInput.MouseState = new MouseState(clickX, clickY, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();

            // 2. Act
            _inputMode.HandleInput(_inputManager, _mockMarket, _mockMap, _activePlayer, _mockAction);

            // 3. Assert
            Assert.AreEqual(PlayerColor.Blue, _mockAction.FinalizedSpyColor, "Clicking the spy button should finalize the action with that color.");
        }

        [TestMethod]
        public void HandleInput_ClickingEmptySpace_DoesNothing()
        {
            // 1. Arrange
            _mockAction.CurrentState = ActionState.TargetingAssassinate;
            _mockMap.NodeToReturn = null;
            _mockMap.SiteToReturn = null; // No site, no node

            // Click at (500, 500)
            _mockInput.MouseState = new MouseState(500, 500, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();

            // 2. Act
            var result = _inputMode.HandleInput(_inputManager, _mockMarket, _mockMap, _activePlayer, _mockAction);

            // 3. Assert
            Assert.IsNull(result, "Clicking empty space should return null.");
            Assert.IsFalse(_mockAction.HandleTargetClickCalled, "Should NOT invoke HandleTargetClick on null targets.");
        }

        [TestMethod]
        public void HandleInput_SpySelection_ClickingOutsideButtons_CancelsTargeting()
        {
            // 1. Arrange
            _mockAction.CurrentState = ActionState.SelectingSpyToReturn;

            // Setup Site at (100, 100)
            var site = new Site("Test Site", ResourceType.Power, 1, ResourceType.Influence, 1);
            typeof(Site).GetProperty("Bounds")?.SetValue(site, new Rectangle(100, 100, 100, 100));
            _mockAction.PendingSite = site;
            _mockMap.SpiesToReturn = new List<PlayerColor> { PlayerColor.Blue };

            // Click FAR AWAY at (800, 600) - nowhere near the buttons at (100, 50)
            _mockInput.MouseState = new MouseState(800, 600, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();

            // 2. Act
            _inputMode.HandleInput(_inputManager, _mockMarket, _mockMap, _activePlayer, _mockAction);

            // 3. Assert
            Assert.IsTrue(_mockAction.CancelTargetingCalled, "Clicking outside the spy selection buttons should cancel the targeting action.");
            Assert.IsNull(_mockAction.FinalizedSpyColor, "Should not have finalized any spy selection.");
        }

        [TestMethod]
        public void HandleInput_ClickingSite_PassesSiteToActionSystem()
        {
            // 1. Arrange
            _mockAction.CurrentState = ActionState.TargetingPlaceSpy; // Logic that targets sites
            _mockMap.NodeToReturn = null;

            var targetSite = new Site("Target Site", ResourceType.Power, 1, ResourceType.Influence, 1);
            _mockMap.SiteToReturn = targetSite;

            // Click
            _mockInput.MouseState = new MouseState(300, 300, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _inputManager.Update();

            // 2. Act
            _inputMode.HandleInput(_inputManager, _mockMarket, _mockMap, _activePlayer, _mockAction);

            // 3. Assert
            Assert.IsTrue(_mockAction.HandleTargetClickCalled);
            Assert.AreEqual(targetSite, _mockAction.ClickedSite, "ActionSystem should receive the clicked Site.");
        }
    }
}