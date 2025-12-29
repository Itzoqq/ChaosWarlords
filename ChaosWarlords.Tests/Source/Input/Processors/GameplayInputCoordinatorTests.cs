using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using NSubstitute;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Input;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Core.Composition;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Tests.Source.Systems
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
            var tm = new TurnManager(new List<Player> { p1, p2 }, mockRandom, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            var mapManager = Substitute.For<IMapManager>();
            var marketManager = Substitute.For<IMarketManager>();
            _actionSub = Substitute.For<IActionSystem>();
            var cardDb = Substitute.For<ICardDatabase>();

            var ps = new PlayerStateManager(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            _context = new MatchContext(tm, mapManager, marketManager, _actionSub, cardDb, ps, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            // 2. Setup Testable State
            // We pass null for Game/InputProvider because our subclass doesn't use them in this specific test scope
            _state = new TestableGameplayState(null!, Substitute.For<IInputProvider>(), cardDb, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

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
        public void SetMarketMode_True_SwitchesToMarket()
        {
            _coordinator.SetMarketMode(true);
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(MarketInputMode));
        }

        [TestMethod]
        public void SetMarketMode_False_SwitchesToNormal()
        {
            _coordinator.SetMarketMode(true); // First open market
            _coordinator.SetMarketMode(false); // Then close it
            Assert.IsInstanceOfType(_coordinator.CurrentMode, typeof(NormalPlayInputMode));
        }

        // --- Helper Subclass to Expose Internals ---
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
                    View = Substitute.For<IGameplayView>(),   // Default mock
                    ViewportWidth = 1920,
                    ViewportHeight = 1080
                })
            {
            }

            public void SetUIManager(IUIManager ui)
            {
                // We access the internal field from the base class
                _uiManagerBacking = ui;
            }
        }
    }
}


