using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Source.Systems
{
    [TestClass]
    public class GameplayInputCoordinatorTests
    {
        private GameplayInputCoordinator _coordinator = null!;
        private TestableGameplayState _state = null!;
        private MatchContext _context = null!;
        private IActionSystem _actionSub = null!;

        [TestInitialize]
        public void Setup()
        {
            // 1. Setup Context with a REAL TurnManager
            // (The Coordinator casts 'ITurnManager' to 'TurnManager', so a Mock would become null)
            var p1 = new Player(PlayerColor.Red);
            var p2 = new Player(PlayerColor.Blue);
            var turnManager = new TurnManager(new List<Player> { p1, p2 });

            var mapManager = Substitute.For<IMapManager>();
            var marketManager = Substitute.For<IMarketManager>();
            _actionSub = Substitute.For<IActionSystem>();
            var cardDb = Substitute.For<ICardDatabase>();

            _context = new MatchContext(turnManager, mapManager, marketManager, _actionSub, cardDb);

            // 2. Setup Testable State
            // We pass null for Game/InputProvider because our subclass doesn't use them in this specific test scope
            _state = new TestableGameplayState(null!, Substitute.For<IInputProvider>(), cardDb);

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
            public TestableGameplayState(Game game, IInputProvider input, ICardDatabase db)
                : base(game, input, db)
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