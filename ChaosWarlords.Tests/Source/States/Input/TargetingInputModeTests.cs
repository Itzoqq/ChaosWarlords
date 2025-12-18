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

            var mockState = new MockGameplayState(_mockAction);
            mockState.InputManager = _inputManager;
            mockState.UIManager = _mockUI;
            mockState.MapManager = _mockMap;
            mockState.MarketManager = _mockMarket;
            mockState.TurnManager = _turnManager;

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
            _mockInput.SetMouseState(new MouseState(100, 100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update(); // Frame 1
            _mockInput.SetMouseState(new MouseState(100, 100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released));
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
            _mockInput.SetMouseState(new MouseState(100, 100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(100, 100, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
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
            _mockInput.SetMouseState(new MouseState(200, 200, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(200, 200, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
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
            _mockInput.SetMouseState(new MouseState(clickX, clickY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(clickX, clickY, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            // 2. Act
            _inputMode.HandleInput(_inputManager, _mockMarket, _mockMap, _activePlayer, _mockAction);

            // 3. Assert
            Assert.AreEqual(PlayerColor.Blue, _mockAction.LastFinalizedSpyColor, "Clicking the spy button should finalize the action with that color.");
        }

        [TestMethod]
        public void HandleInput_ClickingEmptySpace_DoesNothing()
        {
            // 1. Arrange
            _mockAction.CurrentState = ActionState.TargetingAssassinate;
            _mockMap.NodeToReturn = null;
            _mockMap.SiteToReturn = null; // No site, no node

            // Click at (500, 500)
            _mockInput.SetMouseState(new MouseState(500, 500, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
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
            _mockInput.SetMouseState(new MouseState(800, 600, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            // 2. Act
            _inputMode.HandleInput(_inputManager, _mockMarket, _mockMap, _activePlayer, _mockAction);

            // 3. Assert
            Assert.IsTrue(_mockAction.CancelTargetingCalled, "Clicking outside the spy selection buttons should cancel the targeting action.");
            Assert.IsNull(_mockAction.LastFinalizedSpyColor, "Should not have finalized any spy selection.");
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
            _mockInput.SetMouseState(new MouseState(300, 300, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            // 2. Act
            _inputMode.HandleInput(_inputManager, _mockMarket, _mockMap, _activePlayer, _mockAction);

            // 3. Assert
            Assert.IsTrue(_mockAction.HandleTargetClickCalled);
            Assert.AreEqual(targetSite, _mockAction.ClickedSite, "ActionSystem should receive the clicked Site.");
        }
    }
}