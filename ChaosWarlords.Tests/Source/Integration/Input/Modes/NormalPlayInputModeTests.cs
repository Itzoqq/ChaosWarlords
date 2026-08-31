using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Contexts;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Rendering;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Commands;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Core.Events; // Fixed namespace
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Integration.Input.Modes
{
    [TestClass]
    [TestCategory("Integration")]
    public class NormalPlayInputModeTests
    {
        private NormalPlayInputMode _inputMode = null!;
        private MockInputProvider _mockInput = null!;
        private IInputManager _inputManager = null!;

        // Concrete Fake
        private TestGameplayState _stateFake = null!;

        // Substitutes (Dependencies of State)
        private IMapManager _mapSub = null!;
        private IActionSystem _actionSub = null!;
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
            _marketSub = Substitute.For<IMarketManager>();
            _mockUI = Substitute.For<IUIManager>();
            _activePlayer = TestData.Players.RedPlayer();
            var mockRandom = Substitute.For<IGameRandom>();

            // Define p1 and p2 for the TurnManager instantiation
            var p1 = _activePlayer;
            var p2 = TestData.Players.BluePlayer();
            _turnManager = new TurnManager(new List<Player> { p1, p2 }, mockRandom, Utilities.TestLogger.Instance);

            // Initialize Fake State
            _stateFake = new TestGameplayState
            {
                MapManager = _mapSub,
                TurnManager = _turnManager,
                ActionSystem = _actionSub,
                MarketManager = _marketSub, // Assuming IMarketManager property exists in state
                MatchContext = new MatchContext(
                     _turnManager,
                     _mapSub,
                     _marketSub,
                     _actionSub,
                     Substitute.For<ICardDatabase>(),
                     new PlayerStateManager(Utilities.TestLogger.Instance),
                     null, Utilities.TestLogger.Instance
                )
            };

            _inputMode = new NormalPlayInputMode(
                _stateFake,
                _inputManager,
                _mockUI,
                _mapSub,
                _turnManager,
                _actionSub
            );
        }

        [TestMethod]
        public void HandleInteraction_ClickOnCard_ReturnsPlayCardCommand()
        {
            // 1. Arrange
            var card = TestData.Cards.CheapCard();

            // Mock State to return this card as hovered
            _stateFake.HoveredHandCard = card;

            // Create Event
            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(110, 110));

            // 2. Act
            var result = _inputMode.HandleInteraction(
                evt,
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
        public void HandleInteraction_ClickOnMandatoryInnerCircleDevourCard_ReturnsPlayCardCommand_NotPreCommit()
        {
            // Regression test: ShouldHandleDevourPreCommit used to pre-commit ANY mandatory,
            // non-Market devour effect - including InnerCircle - by calling TryStartDevourHand
            // unconditionally. That's wrong for InnerCircle: it isn't selected by clicking a
            // Hand card, and pre-committing it before the card is Play()'d would leave the
            // card unplayed while MatchManager.ShouldResumeDevourChain's "not on stack, resume
            // manually" fallback ran the OnSuccess chain anyway. This must fall through to
            // normal play instead - InnerCircle devour already works correctly post-play (see
            // MandatoryInnerCircleDevourIntegrationTests.cs), it just can't be pre-selected by
            // a hand click. See planning.txt.
            var card = TestData.Cards.DevourCard(); // mandatory (IsOptional defaults false), TargetLocation defaults to Hand - set explicitly below
            card.Effects.Single(e => e.Type == EffectType.Devour).TargetLocation = CardLocation.InnerCircle;

            _stateFake.HoveredHandCard = card;
            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(110, 110));

            var result = _inputMode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            Assert.IsInstanceOfType(result, typeof(PlayCardCommand));
            _actionSub.DidNotReceive().TryStartDevourInnerCircle(Arg.Any<Card>(), Arg.Any<System.Action>(), Arg.Any<bool>());
            _actionSub.DidNotReceive().TryStartDevourHand(Arg.Any<Card>(), Arg.Any<System.Action>(), Arg.Any<bool>());
        }

        [TestMethod]
        public void HandleInteraction_ClickOnMapNode_DeploysTroop()
        {
            // 1. Arrange
            // Ensure no card is hovered
            _stateFake.HoveredHandCard = null;

            // Setup Map Mock to return a node at click location
            var targetNode = TestData.MapNodes.Node1();
            var clickPos = new Vector2(200, 200);
            _mapSub.GetNodeAt(clickPos.ToLogicVector2()).Returns(targetNode);
            _mapSub.CanDeployAt(targetNode, _activePlayer.Color).Returns(true);
            _mapSub.Nodes.Returns(new List<MapNode> { targetNode });

            var evt = new InputEventArgs(InputEventType.LeftClick, clickPos);

            // 2. Act
            var result = _inputMode.HandleInteraction(
                evt,
                _marketSub,
                _mapSub,
                _activePlayer,
                _actionSub
            );

            // 3. Assert
            Assert.IsNotNull(result, "Map interaction should return a DeployTroopCommand.");
            Assert.IsInstanceOfType(result, typeof(DeployTroopCommand));

            // Execute the command to verify it calls map manager
            // result.Execute(_stateFake);
            // Note: Since _stateFake holds the mock _mapSub, executing the command should trigger the Call.
            result.Execute(_stateFake.MatchContext);
            _mapSub.Received(1).TryDeploy(_activePlayer, targetNode);
        }

        [TestMethod]
        public void HandleInteraction_CardOverlapsNode_CardTakesPriority()
        {
            // 1. Arrange
            var card = TestData.Cards.CheapCard();

            // Both Card and Map Node are "active" under the mouse
            _stateFake.HoveredHandCard = card;

            var node = TestData.MapNodes.Node1();
            var clickPos = new Vector2(110, 110);
            _mapSub.GetNodeAt(clickPos.ToLogicVector2()).Returns(node);

            var evt = new InputEventArgs(InputEventType.LeftClick, clickPos);

            // 2. Act
            var result = _inputMode.HandleInteraction(
                evt,
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
        public void HandleInteraction_ClickEmptySpace_ReturnsNull()
        {
            // 1. Arrange
            _stateFake.HoveredHandCard = null;
            var clickPos = new Vector2(500, 500);
            _mapSub.GetNodeAt(clickPos.ToLogicVector2()).Returns((MapNode?)null);

            var evt = new InputEventArgs(InputEventType.LeftClick, clickPos);

            // 2. Act
            var result = _inputMode.HandleInteraction(
                evt,
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
