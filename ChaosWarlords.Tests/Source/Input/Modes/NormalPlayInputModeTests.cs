using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;

using NSubstitute;
using ChaosWarlords.Source.States;

namespace ChaosWarlords.Tests.States.Input
{
    [TestClass]
    public class NormalPlayInputModeTests
    {
        private NormalPlayInputMode _inputMode = null!;
        private MockInputProvider _mockInput = null!;
        private IInputManager _inputManager = null!;

        // Substitutes
        private IMapManager _mapSub = null!;
        private IActionSystem _actionSub = null!;
        private IGameplayState _stateSub = null!;
        private IMarketManager _marketSub = null!;
        private IUIManager _mockUI = null!;
        private TurnManager _turnManager = null!;
        private Player _activePlayer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInput = new MockInputProvider();
            _inputManager = new InputManager(_mockInput);

            // Substitutes
            _mapSub = Substitute.For<IMapManager>();
            _actionSub = Substitute.For<IActionSystem>();
            _stateSub = Substitute.For<IGameplayState>();
            _marketSub = Substitute.For<IMarketManager>();
            _mockUI = Substitute.For<IUIManager>();
            _activePlayer = new Player(PlayerColor.Red);
            var mockRandom = Substitute.For<IGameRandom>();
            _turnManager = new TurnManager(new List<Player> { _activePlayer }, mockRandom);

            _inputMode = new NormalPlayInputMode(
                _stateSub,
                _inputManager,
                _mockUI,
                _mapSub,
                _turnManager,
                _actionSub
            );
        }

        [TestMethod]
        public void HandleInput_ClickOnCard_ReturnsPlayCardCommand()
        {
            // 1. Arrange
            var card = new Card("test", "Test Soldier", 0, CardAspect.Neutral, 0, 0, 0);

            // Mock State to return this card as hovered
            _stateSub.GetHoveredHandCard().Returns(card);

            // Simulate Click
            InputTestHelpers.SimulateLeftClick(_mockInput, _inputManager, 110, 110);

            // 2. Act
            var result = _inputMode.HandleInput(
                _inputManager,
                _marketSub,
                _mapSub,
                _activePlayer,
                _actionSub
            );

            // 3. Assert
            Assert.IsNotNull(result, "Input should handle the Card, returning a command.");
            Assert.IsInstanceOfType(result, typeof(PlayCardCommand));
            _mapSub.DidNotReceive().TryDeploy(Arg.Any<Player>(), Arg.Any<MapNode>());
        }

        [TestMethod]
        public void HandleInput_ClickOnMapNode_DeploysTroop()
        {
            // 1. Arrange
            // Ensure no card is hovered
            _stateSub.GetHoveredHandCard().Returns((Card?)null);

            // Setup Map Mock to return a node at click location
            var targetNode = new MapNode(1, new Vector2(200, 200));
            _mapSub.GetNodeAt(Arg.Any<Vector2>()).Returns(targetNode);

            // Simulate Click at 200,200
            InputTestHelpers.SimulateLeftClick(_mockInput, _inputManager, 200, 200);

            // 2. Act
            var result = _inputMode.HandleInput(
                _inputManager,
                _marketSub,
                _mapSub,
                _activePlayer,
                _actionSub
            );

            // 3. Assert
            Assert.IsNull(result, "Map interaction should not return a generic command.");
            _mapSub.Received(1).TryDeploy(_activePlayer, targetNode);
        }

        [TestMethod]
        public void HandleInput_CardOverlapsNode_CardTakesPriority()
        {
            // 1. Arrange
            var card = new Card("test", "Test Soldier", 0, CardAspect.Neutral, 0, 0, 0);

            // Both Card and Map Node are "active" under the mouse
            _stateSub.GetHoveredHandCard().Returns(card);

            var node = new MapNode(1, new Vector2(100, 100));
            _mapSub.GetNodeAt(Arg.Any<Vector2>()).Returns(node);

            // Simulate Click
            InputTestHelpers.SimulateLeftClick(_mockInput, _inputManager, 110, 110);

            // 2. Act
            var result = _inputMode.HandleInput(
                _inputManager,
                _marketSub,
                _mapSub,
                _activePlayer,
                _actionSub
            );

            // 3. Assert
            Assert.IsNotNull(result, "Input should handle the Card, returning a command.");
            Assert.IsInstanceOfType(result, typeof(PlayCardCommand));
            // Ensure we did NOT try to deploy to the map
            _mapSub.DidNotReceive().TryDeploy(Arg.Any<Player>(), Arg.Any<MapNode>());
        }

        [TestMethod]
        public void HandleInput_ClickEmptySpace_ReturnsNull()
        {
            // 1. Arrange
            _stateSub.GetHoveredHandCard().Returns((Card?)null);
            _mapSub.GetNodeAt(Arg.Any<Vector2>()).Returns((MapNode?)null);

            InputTestHelpers.SimulateLeftClick(_mockInput, _inputManager, 500, 500);

            // 2. Act
            var result = _inputMode.HandleInput(
                _inputManager,
                _marketSub,
                _mapSub,
                _activePlayer,
                _actionSub
            );

            // 3. Assert
            Assert.IsNull(result, "Clicking empty space should return null.");
            _mapSub.DidNotReceive().TryDeploy(Arg.Any<Player>(), Arg.Any<MapNode>());
        }


    }
}


