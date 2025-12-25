using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.States.Input
{
    [TestClass]
    public class TargetingInputModeTests
    {
        private TargetingInputMode _inputMode = null!;
        private MockInputProvider _mockInput = null!;
        private InputManager _inputManager = null!;

        private IMapManager _mapSub = null!;
        private IActionSystem _actionSub = null!;
        private IGameplayState _stateSub = null!;
        private IMarketManager _marketSub = null!;

        private MockUISystem _mockUI = null!;
        private TurnManager _turnManager = null!;
        private Player _activePlayer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInput = new MockInputProvider();
            _inputManager = new InputManager(_mockInput);

            _mapSub = Substitute.For<IMapManager>();
            _actionSub = Substitute.For<IActionSystem>();
            _stateSub = Substitute.For<IGameplayState>();
            _marketSub = Substitute.For<IMarketManager>();

            _mockUI = new MockUISystem();
            _activePlayer = new Player(PlayerColor.Red);
            _turnManager = new TurnManager(new List<Player> { _activePlayer });

            _inputMode = new TargetingInputMode(
                _stateSub,
                _inputManager,
                _mockUI,
                _mapSub,
                _turnManager,
                _actionSub
            );
        }

        [TestMethod]
        public void HandleInput_SafetyCheck_IfActionStateIsNormal_ReturnsSwitchCommand()
        {
            _actionSub.CurrentState.Returns(ActionState.Normal);
            var result = _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(SwitchToNormalModeCommand));
        }

        [TestMethod]
        public void HandleInput_RightClick_CancelsTargeting_AndReturnsSwitchCommand()
        {
            _actionSub.CurrentState.Returns(ActionState.TargetingAssassinate);

            _mockInput.SetMouseState(new MouseState(100, 100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(100, 100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            var result = _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            _actionSub.Received(1).CancelTargeting();
            Assert.IsInstanceOfType(result, typeof(SwitchToNormalModeCommand));
        }

        [TestMethod]
        public void HandleInput_UIBlocking_IfMarketHovered_DoesNothing()
        {
            _actionSub.CurrentState.Returns(ActionState.TargetingPlaceSpy);
            _mockUI.IsMarketHovered = true;

            _mockInput.SetMouseState(new MouseState(100, 100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(100, 100, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            var result = _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            Assert.IsNull(result);
            _actionSub.DidNotReceive().HandleTargetClick(Arg.Any<MapNode>(), Arg.Any<Site>());
        }

        [TestMethod]
        public void HandleInput_ValidTargetClick_CallsSystemHandler()
        {
            _actionSub.CurrentState.Returns(ActionState.TargetingAssassinate);

            var node = new MapNode(1, new Vector2(200, 200));
            _mapSub.GetNodeAt(Arg.Any<Vector2>()).Returns(node);

            _mockInput.SetMouseState(new MouseState(200, 200, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();
            _mockInput.SetMouseState(new MouseState(200, 200, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            _actionSub.Received(1).HandleTargetClick(node, null);
        }

        [TestMethod]
        public void HandleInput_ClickingOutsideSpySelection_CancelsTargeting()
        {
            // 1. Arrange
            _actionSub.CurrentState.Returns(ActionState.SelectingSpyToReturn);

            var site = new NonCitySite("Test Site", ResourceType.Power, 1, ResourceType.Influence, 1);
            // Use Reflection to set bounds if needed, or rely on defaults
            typeof(Site).GetProperty("Bounds")?.SetValue(site, new Rectangle(100, 100, 100, 100));

            _actionSub.PendingSite.Returns(site);

            // Use method call instead of property
            _mapSub.GetEnemySpiesAtSite(site, _activePlayer).Returns(new List<PlayerColor> { PlayerColor.Blue });

            // Click FAR AWAY at (800, 600)
            _mockInput.SetMouseState(new MouseState(800, 600, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            // 2. Act
            _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            // 3. Assert
            _actionSub.Received(1).CancelTargeting();
            _actionSub.DidNotReceive().FinalizeSpyReturn(Arg.Any<PlayerColor>());
        }

        [TestMethod]
        public void HandleInput_ClickingSite_PassesSiteToActionSystem()
        {
            _actionSub.CurrentState.Returns(ActionState.TargetingPlaceSpy);

            var targetSite = new NonCitySite("Target Site", ResourceType.Power, 1, ResourceType.Influence, 1);
            _mapSub.GetSiteAt(Arg.Any<Vector2>()).Returns(targetSite);
            _mapSub.GetNodeAt(Arg.Any<Vector2>()).Returns((MapNode?)null);

            _mockInput.SetMouseState(new MouseState(300, 300, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            _inputManager.Update();

            _inputMode.HandleInput(_inputManager, _marketSub, _mapSub, _activePlayer, _actionSub);

            _actionSub.Received(1).HandleTargetClick(null, targetSite);
        }
    }
}