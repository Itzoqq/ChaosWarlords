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
    public class NormalPlayInputModeTests
    {
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
            var mockState = new MockGameplayState(_mockAction);
            mockState.InputManager = _inputManager;
            mockState.UIManager = _mockUI;
            mockState.MapManager = _mockMap;
            mockState.MarketManager = _mockMarket;
            mockState.TurnManager = _turnManager;

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
            _mockInput.SetMouseState(new MouseState(110, 110, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            // Frame 2: Pressed (Click)
            _mockInput.SetMouseState(new MouseState(110, 110, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
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
            _mockInput.SetMouseState(new MouseState(200, 200, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            // Frame 2: Pressed
            _mockInput.SetMouseState(new MouseState(200, 200, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
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
            Assert.AreEqual(targetNode, _mockMap.LastDeployTarget);
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
            _mockInput.SetMouseState(new MouseState(110, 110, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update(); // Frame 1
            _mockInput.SetMouseState(new MouseState(110, 110, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
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
            _mockInput.SetMouseState(new MouseState(500, 500, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(500, 500, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
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