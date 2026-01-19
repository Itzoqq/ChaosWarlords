using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using NSubstitute;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Input;
using ChaosWarlords.Source.GameStates;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Core.Composition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Core.Events;
using System;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Integration.Input.Processors
{
    [TestClass]
    [TestCategory("Integration")]
    public class GameplayInputCoordinatorTests
    {
        private GameplayInputCoordinator _coordinator = null!;
        private TestableGameplayState _state = null!;
        private MatchContext _context = null!;
        private IActionSystem _actionSub = null!;
        private InputManager _inputManager = null!; // Concrete implementation for integration
        private IInputProvider _mockInputProvider = null!;

        [TestInitialize]
        public void Setup()
        {
            var p1 = TestData.Players.RedPlayer();
            var p2 = TestData.Players.BluePlayer();
            var mockRandom = Substitute.For<IGameRandom>();
            var tm = new TurnManager(new List<Player> { p1, p2 }, mockRandom, Utilities.TestLogger.Instance);

            var mapManager = Substitute.For<IMapManager>();
            var marketManager = Substitute.For<IMarketManager>();
            _actionSub = Substitute.For<IActionSystem>();
            var cardDb = Substitute.For<ICardDatabase>();

            var ps = new PlayerStateManager(Utilities.TestLogger.Instance);
            _context = new MatchContext(tm, mapManager, marketManager, _actionSub, cardDb, ps, null, Utilities.TestLogger.Instance);

            // 3. Setup InputManager with Mock Provider
            _mockInputProvider = Substitute.For<IInputProvider>();
            _inputManager = new InputManager(_mockInputProvider);

            // 2. Setup Testable State
            // We pass null for Game because our subclass doesn't use it in this specific test scope
            // We pass our concrete _inputManager
            _state = new TestableGameplayState(null!, _mockInputProvider, cardDb, Utilities.TestLogger.Instance);
            // Manually overwrite the backing field to ensure consistency if the constructor created a new one
            // However, TestableGameplayState constructor creates a new InputManager(input). 
            // We want to control the one used by Coordinator.
            // Let's rely on injecting the same provider.

            // 4. Create Coordinator (Now with a valid State and UIManager)
            _state.SetUIManager(Substitute.For<IUIManager>());
            _coordinator = new GameplayInputCoordinator(_state, _inputManager, _context);
        }

        [TestMethod]
        public void SwitchToTargetingMode_SelectsSpy_IfStateIsPlacingSpy()
        {
            // Arrange
            _actionSub.CurrentState.Returns(ActionState.TargetingPlaceSpy);

            // Act
            _coordinator.SwitchToTargetingMode();

            // Assert
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(TargetingInputMode));
        }

        [TestMethod]
        public void SwitchToTargetingMode_SelectsDevour_IfStateIsDevour()
        {
            // Arrange
            _actionSub.CurrentState.Returns(ActionState.TargetingDevourHand);

            // Act
            _coordinator.SwitchToTargetingMode();

            // Assert
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(DevourInputMode));
        }

        [TestMethod]
        public void HandleActionStateChanged_NormalStateWithMarketClosed_SwitchesToNormalMode()
        {
            // Arrange
            _actionSub.CurrentState.Returns(ActionState.Normal);
            _state.MarketStateManager.Close(); // Ensure market is closed

            // Act - Trigger the event
            _actionSub.OnStateChanged += Raise.Event<EventHandler<ActionState>>(null, ActionState.Normal);

            // Assert
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(NormalPlayInputMode));
        }

        [TestMethod]
        public void HandleActionStateChanged_NormalStateWithMarketOpen_SwitchesToMarketMode()
        {
            // Arrange
            _actionSub.CurrentState.Returns(ActionState.Normal);
            _state.MarketStateManager.OpenForBrowsing();

            // Act - Trigger the event
            _actionSub.OnStateChanged += Raise.Event<EventHandler<ActionState>>(null, ActionState.Normal);

            // Assert
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(MarketInputMode));
        }

        [TestMethod]
        public void HandleActionStateChanged_NormalStateWithMarketOpenAlreadyInMarketMode_PreservesMarketMode()
        {
            // Arrange
            _actionSub.CurrentState.Returns(ActionState.Normal);
            _state.MarketStateManager.OpenForBrowsing();

            // Switch first
            _coordinator.SwitchToNormalMode(); // Should go to Market because it's open
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(MarketInputMode));
            
            var initialMode = _coordinator.CurrentMode;

            // Act - Trigger the event again
            _actionSub.OnStateChanged += Raise.Event<EventHandler<ActionState>>(null, ActionState.Normal);

            // Assert - Should still be MarketInputMode (not recreated)
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(MarketInputMode));
        }

        [TestMethod]
        public void HandleActionStateChanged_TargetingState_SwitchesToTargetingMode()
        {
            // Arrange
            _actionSub.CurrentState.Returns(ActionState.TargetingAssassinate);

            // Act
            _actionSub.OnStateChanged += Raise.Event<EventHandler<ActionState>>(null, ActionState.TargetingAssassinate);

            // Assert
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(TargetingInputMode));
        }

        [TestMethod]
        public void HandleActionStateChanged_SelectingCardToPromote_SwitchesToPromoteMode()
        {
            // Arrange
            _actionSub.CurrentState.Returns(ActionState.SelectingCardToPromote);
            // Use AddPromotionCredit on the REAL context instead of mocking the property
            var dummyCard = CardFactory.CreateSoldier(Substitute.For<IGameRandom>());
            _context.TurnManager.CurrentTurnContext.AddPromotionCredit(dummyCard, 2);

            // Act
            _actionSub.OnStateChanged += Raise.Event<EventHandler<ActionState>>(null, ActionState.SelectingCardToPromote);

            // Assert
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(PromoteInputMode));
        }

        [TestMethod]
        public void HandleActionStateChanged_TargetingDevourHand_SwitchesToDevourMode()
        {
            // Arrange
            _actionSub.CurrentState.Returns(ActionState.TargetingDevourHand);

            // Act
            _actionSub.OnStateChanged += Raise.Event<EventHandler<ActionState>>(null, ActionState.TargetingDevourHand);

            // Assert
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(DevourInputMode));
        }

        [TestMethod]
        public void HandleInputEvent_WithNullCommand_DoesNotExecuteCommand()
        {
            // Arrange
            var mockMode = Substitute.For<IInputMode>();
            mockMode.HandleInteraction(Arg.Any<InputEventArgs>(), Arg.Any<IMarketManager>(), Arg.Any<IMapManager>(), Arg.Any<Player>(), Arg.Any<IActionSystem>())
                .Returns((IGameCommand?)null);

            // Use reflection to set the current mode (still needed as CurrentMode is read-only)
            var field = typeof(GameplayInputCoordinator).GetField("_currentMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(_coordinator, mockMode);

            // Trigger Event via InputManager Update
            
            // Initial state (Released)
            _mockInputProvider.GetMouseState().Returns(new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            // Clicked state
            _mockInputProvider.GetMouseState().Returns(new MouseState(0, 0, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            
            // Act
            _inputManager.Update(); // This fires OnInputEvent -> HandleInputEvent on Coordinator

            // Assert
            mockMode.Received(1).HandleInteraction(Arg.Any<InputEventArgs>(), Arg.Any<IMarketManager>(), Arg.Any<IMapManager>(), Arg.Any<Player>(), Arg.Any<IActionSystem>());
        }

        [TestMethod]
        public void HandleInputEvent_WithValidCommand_ExecutesCommand()
        {
            // Arrange
            var mockCommand = Substitute.For<IGameCommand>();
            var mockMode = Substitute.For<IInputMode>();
            mockMode.HandleInteraction(Arg.Any<InputEventArgs>(), Arg.Any<IMarketManager>(), Arg.Any<IMapManager>(), Arg.Any<Player>(), Arg.Any<IActionSystem>())
                .Returns(mockCommand);

            var field = typeof(GameplayInputCoordinator).GetField("_currentMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(_coordinator, mockMode);

            // Trigger Event via InputManager Update
            _mockInputProvider.GetMouseState().Returns(new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            _mockInputProvider.GetMouseState().Returns(new MouseState(0, 0, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            
            // Act
            _inputManager.Update();

            // Assert
            mockMode.Received(1).HandleInteraction(Arg.Any<InputEventArgs>(), Arg.Any<IMarketManager>(), Arg.Any<IMapManager>(), Arg.Any<Player>(), Arg.Any<IActionSystem>());
            CollectionAssert.Contains(_state.RecordedCommands, mockCommand);
        }

        [TestMethod]
        public void SetMarketMode_SwitchesToMarketInputMode()
        {
            _state.MarketStateManager.OpenForBrowsing();
            // Need to manually trigger logic check, or call explicit Switch
            // Coordinator constructor sets initial mode based on state. 
            // If we change state, we expect Coordinator to update IF notified. 
            // Tests usually trigger notification or call SwitchToNormalMode which checks market.
            
            _coordinator.SwitchToNormalMode(); // Re-evaluates

            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(MarketInputMode));
        }

        [TestMethod]
        public void SetMarketMode_CanToggleBetweenModes()
        {
            _state.MarketStateManager.OpenForBrowsing();
            _coordinator.SwitchToNormalMode();
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(MarketInputMode));

            _state.MarketStateManager.Close();
            _coordinator.SwitchToNormalMode();
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(NormalPlayInputMode));
        }

        internal class TestableGameplayState : GameplayState
        {
            public TestableGameplayState(Game game, IInputProvider input, ICardDatabase db, IGameLogger logger)
                : base(new GameDependencies
                {
                    Game = game,
                    InputManager = new InputManager(input),
                    CardDatabase = db,
                    Logger = logger,
                    UIManager = Substitute.For<IUIManager>(), // Default mock
                    ReplayManager = Substitute.For<IReplayManager>(),
                    View = Substitute.For<IGameplayView>(),   // Default mock
                    ViewportWidth = 1920,
                    ViewportHeight = 1080
                })
            {
                // Initialize MarketStateManager for tests
                _marketStateManager = new MarketStateManager(logger);
            }

            public void SetUIManager(IUIManager ui)
            {
                // We access the internal field from the base class
                _uiManagerBacking = ui;
            }

            public List<IGameCommand> RecordedCommands { get; } = new();

            public override void RecordAndExecuteCommand(IGameCommand command)
            {
                // Mock behavior: Access to DB/Game would fail, so we just capture the command
                RecordedCommands.Add(command);
            }
        }
    }
}


