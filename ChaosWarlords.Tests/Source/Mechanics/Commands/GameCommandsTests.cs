using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Commands
{
    [TestClass]
    public class GameCommandsTests
    {
        private IGameplayState _stateSub = null!;
        private IMarketManager _marketSub = null!;
        private IMapManager _mapSub = null!;
        private ITurnManager _turnSub = null!;
        private IActionSystem _actionSub = null!;
        private IMatchController _controllerSub = null!;

        [TestInitialize]
        public void Setup()
        {
            // Initialize Logger to prevent static errors
            GameLogger.Initialize();

            // Create mocks
            _stateSub = Substitute.For<IGameplayState>();
            _marketSub = Substitute.For<IMarketManager>();
            _mapSub = Substitute.For<IMapManager>();
            _turnSub = Substitute.For<ITurnManager>();
            _actionSub = Substitute.For<IActionSystem>();

            _controllerSub = Substitute.For<IMatchController>();

            // Wire up the state to return these mocks
            _stateSub.MarketManager.Returns(_marketSub);
            _stateSub.MapManager.Returns(_mapSub);
            _stateSub.TurnManager.Returns(_turnSub);
            _stateSub.ActionSystem.Returns(_actionSub);
            _stateSub.MatchController.Returns(_controllerSub);
        }

        [TestMethod]
        public void BuyCardCommand_ExecutesTryBuyCard()
        {
            // Arrange
            var player = new Player(PlayerColor.Red);
            _turnSub.ActivePlayer.Returns(player);

            var card = new Card("c1", "Test Card", 3, CardAspect.Neutral, 0, 0, 0);
            var command = new BuyCardCommand(card);

            // Act
            command.Execute(_stateSub);

            // Assert
            _marketSub.Received(1).TryBuyCard(player, card);
        }

        [TestMethod]
        public void DeployTroopCommand_ExecutesTryDeploy()
        {
            // Arrange
            var player = new Player(PlayerColor.Blue);
            _turnSub.ActivePlayer.Returns(player);

            var node = new MapNode(1, new Microsoft.Xna.Framework.Vector2(0, 0));
            var command = new DeployTroopCommand(node);

            // Act
            command.Execute(_stateSub);

            // Assert
            _mapSub.Received(1).TryDeploy(player, node);
        }

        [TestMethod]
        public void ToggleMarketCommand_ClosesMarket_WhenOpen()
        {
            // Arrange
            _stateSub.IsMarketOpen.Returns(true);
            var command = new ToggleMarketCommand();

            // Act
            command.Execute(_stateSub);

            // Assert
            _stateSub.Received(1).CloseMarket();
            _stateSub.DidNotReceive().ToggleMarket();
        }

        [TestMethod]
        public void ToggleMarketCommand_TogglesMarket_WhenClosed()
        {
            // Arrange
            _stateSub.IsMarketOpen.Returns(false);
            var command = new ToggleMarketCommand();

            // Act
            command.Execute(_stateSub);

            // Assert
            // Based on your implementation: else { state.ToggleMarket(); }
            _stateSub.Received(1).ToggleMarket();
            _stateSub.DidNotReceive().CloseMarket();
        }

        [TestMethod]
        public void ResolveSpyCommand_FinalizesSpyReturn()
        {
            // Arrange
            var targetColor = PlayerColor.Red;
            var command = new ResolveSpyCommand(targetColor);

            // Act
            command.Execute(_stateSub);

            // Assert
            _actionSub.Received(1).FinalizeSpyReturn(targetColor);
        }

        [TestMethod]
        public void CancelActionCommand_CancelsTargeting_AndSwitchesToNormal()
        {
            // Arrange
            var command = new CancelActionCommand();

            // Act
            command.Execute(_stateSub);

            // Assert
            _actionSub.Received(1).CancelTargeting();
            _stateSub.Received(1).SwitchToNormalMode();
        }

        [TestMethod]
        public void SwitchToNormalModeCommand_ExecutesSwitch()
        {
            // Arrange
            var command = new SwitchToNormalModeCommand();

            // Act
            command.Execute(_stateSub);

            // Assert
            _stateSub.Received(1).SwitchToNormalMode();
        }

        [TestMethod]
        public void StartReturnSpyCommand_InitiatesReturnProcess_IfTargetingReturnSpy()
        {
            // Arrange
            var command = new StartReturnSpyCommand();

            // Mock ActionSystem to say we are currently in TargetingReturnSpy state
            _actionSub.CurrentState.Returns(ActionState.TargetingReturnSpy);

            // Act
            command.Execute(_stateSub);

            // Assert
            _actionSub.Received(1).TryStartReturnSpy();
            // Should switch to targeting mode if state matches
            _stateSub.Received(1).SwitchToTargetingMode();
        }

        [TestMethod]
        public void StartReturnSpyCommand_DoesNotSwitchMode_IfStateIsNotTargeting()
        {
            // Arrange
            var command = new StartReturnSpyCommand();

            // Mock ActionSystem to say we are in Normal state (failed to start return)
            _actionSub.CurrentState.Returns(ActionState.Normal);

            // Act
            command.Execute(_stateSub);

            // Assert
            _actionSub.Received(1).TryStartReturnSpy();
            _stateSub.DidNotReceive().SwitchToTargetingMode();
        }

        [TestMethod]
        public void DevourCardCommand_ExecutesDevour_AndCompletesAction()
        {
            // Arrange
            var card = new Card("victim", "Victim", 0, CardAspect.Neutral, 0, 0, 0);
            var command = new DevourCardCommand(card);

            // Act
            command.Execute(_stateSub);

            // Assert
            // 1. Verify we called the controller (using the new mock field)
            _controllerSub.Received(1).DevourCard(card);

            // 2. Verify we told the ActionSystem "We are done selecting"
            _actionSub.Received(1).CompleteAction();
        }

        [TestMethod]
        public void PlayCardCommand_DelegatesToState()
        {
            // Arrange
            var card = new Card("c1", "Test", 0, CardAspect.Neutral, 0, 0, 0);
            var command = new PlayCardCommand(card);

            // Act
            command.Execute(_stateSub);

            // Assert
            _stateSub.Received(1).PlayCard(card);
        }

        [TestMethod]
        public void EndTurnCommand_EndsTurn_WhenAllowed()
        {
            // Arrange
            var command = new EndTurnCommand();
             string reason;
            _stateSub.CanEndTurn(out reason).Returns(x => {
                x[0] = "";
                return true;
            });

            // Act
            command.Execute(_stateSub);

            // Assert
            _stateSub.Received(1).EndTurn();
        }

        [TestMethod]
        public void EndTurnCommand_LogsWarning_WhenNotAllowed()
        {
            // Arrange
            var command = new EndTurnCommand();
            string expectedReason = "You must do X first.";
            string reason;
            _stateSub.CanEndTurn(out reason).Returns(x => {
                x[0] = expectedReason;
                return false;
            });

            // Act
            command.Execute(_stateSub);

            // Assert
            _stateSub.DidNotReceive().EndTurn();
            // We can't easily assert the static GameLogger was called without an interface wrapper 
            // or digging into the static logger, but we verify it didn't EndTurn.
        }

        [TestMethod]
        public void StartAssassinateCommand_InitiatesAssassinate()
        {
            // Arrange
            var command = new StartAssassinateCommand();
            
            // Mock that we successfully entered targeting mode
            _actionSub.CurrentState.Returns(ActionState.TargetingAssassinate);

            // Act
            command.Execute(_stateSub);

            // Assert
            _actionSub.Received(1).TryStartAssassinate();
            _stateSub.Received(1).SwitchToTargetingMode();
        }

        [TestMethod]
        public void StartAssassinateCommand_DoesNotSwitch_IfFailed()
        {
            // Arrange
            var command = new StartAssassinateCommand();

            // Mock that we FAILED to enter targeting mode (e.g. no targets)
            _actionSub.CurrentState.Returns(ActionState.Normal);

            // Act
            command.Execute(_stateSub);

            // Assert
            _actionSub.Received(1).TryStartAssassinate();
            _stateSub.DidNotReceive().SwitchToTargetingMode();
        }
    }
}