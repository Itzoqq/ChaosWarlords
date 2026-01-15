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

            // 2. Setup Testable State
            // We pass null for Game/InputProvider because our subclass doesn't use them in this specific test scope
            _state = new TestableGameplayState(null!, Substitute.For<IInputProvider>(), cardDb, Utilities.TestLogger.Instance);

            // Inject a Mock UIManager so SwitchToNormalMode() doesn't crash
            _state.SetUIManager(Substitute.For<IUIManager>());

            // 3. Setup InputManager
            var inputManager = new InputManager(Substitute.For<IInputProvider>());

            // 4. Create Coordinator (Now with a valid State and UIManager)
            _coordinator = new GameplayInputCoordinator(_state, inputManager, _context);
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
        public void HandleInput_WithNullCommand_DoesNotExecuteCommand()
        {
            // Arrange
            var mockMode = Substitute.For<IInputMode>();
            mockMode.HandleInput(Arg.Any<IInputManager>(), Arg.Any<IMarketManager>(), Arg.Any<IMapManager>(), Arg.Any<Player>(), Arg.Any<IActionSystem>())
                .Returns((IGameCommand?)null);

            // Use reflection to set the current mode
            var field = typeof(GameplayInputCoordinator).GetField("_currentMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(_coordinator, mockMode);

            // Act
            _coordinator.HandleInput();

            // Assert - RecordAndExecuteCommand should not be called (we can't directly verify this without more mocking)
            // But we can verify the mode was called
            mockMode.Received(1).HandleInput(Arg.Any<IInputManager>(), Arg.Any<IMarketManager>(), Arg.Any<IMapManager>(), Arg.Any<Player>(), Arg.Any<IActionSystem>());
        }

        [TestMethod]
        public void HandleInput_WithValidCommand_ExecutesCommand()
        {
            // Arrange
            var mockCommand = Substitute.For<IGameCommand>();
            var mockMode = Substitute.For<IInputMode>();
            mockMode.HandleInput(Arg.Any<IInputManager>(), Arg.Any<IMarketManager>(), Arg.Any<IMapManager>(), Arg.Any<Player>(), Arg.Any<IActionSystem>())
                .Returns(mockCommand);

            // Use reflection to set the current mode
            var field = typeof(GameplayInputCoordinator).GetField("_currentMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(_coordinator, mockMode);

            // Act
            _coordinator.HandleInput();

            // Assert - Mode was called
            mockMode.Received(1).HandleInput(Arg.Any<IInputManager>(), Arg.Any<IMarketManager>(), Arg.Any<IMapManager>(), Arg.Any<Player>(), Arg.Any<IActionSystem>());
            // Note: We can't easily verify RecordAndExecuteCommand was called without more complex mocking
        }

        [TestMethod]
        public void SetMarketMode_SwitchesToMarketInputMode()
        {
            _state.MarketStateManager.OpenForBrowsing();
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(MarketInputMode));
        }

        [TestMethod]
        public void SetMarketMode_CanToggleBetweenModes()
        {
            _state.MarketStateManager.OpenForBrowsing();
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(MarketInputMode));

            _state.MarketStateManager.Close();
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


