using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    /// <summary>
    /// CommandDispatcher's own rejection log only ever records a command's type name, which
    /// can't distinguish WHICH of that command's own internal guard clauses produced the
    /// `false` when Validate() has several independent ways to fail. Tests the shared
    /// CommandValidationLogging.RejectValidation helper directly, plus a representative sample
    /// of commands (covering every distinct rejection SHAPE - target not found, unmet resource,
    /// wrong ActionState, wrong-player, delegated MapManager rejection, below an eligibility
    /// threshold - not exhaustively all 23 commands, since every one routes through the same
    /// helper and the helper itself is what's actually being verified here) to confirm
    /// Validate() actually calls it with a specific, useful reason instead of silently
    /// returning false.
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class CommandValidationLoggingTests
    {
        private TestGameplayState _state = null!;
        private IGameLogger _logger = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
            _logger = Substitute.For<IGameLogger>();
            _state.Logger = _logger;
            _state.InitializeMatchContext(); // rebuilds MatchContext so it captures the new Logger
        }

        [TestMethod]
        public void RejectValidation_ReturnsFalse_AndLogsTheReasonAtWarningLevel()
        {
            bool result = _state.MatchContext.RejectValidation("SomeCommand", "a specific reason");

            Assert.IsFalse(result);
            _logger.Received(1).Log("SomeCommand.Validate() rejected: a specific reason", LogChannel.Warning);
        }

        [TestMethod]
        public void AssassinateCommand_Validate_Logs_WhenTargetNodeNotFound()
        {
            var command = new AssassinateCommand(targetNodeId: 999);

            bool result = command.Validate(_state.MatchContext);

            Assert.IsFalse(result);
            _logger.Received(1).Log(
                Arg.Is<string>(s => s.Contains("AssassinateCommand") && s.Contains("999")),
                LogChannel.Warning);
        }

        [TestMethod]
        public void AssassinateCommand_Validate_Logs_WhenPowerInsufficient()
        {
            var node = TestData.MapNodes.Node1();
            _state.MapManager.Nodes.Returns(new List<MapNode> { node });
            _state.TurnManager.ActivePlayer.Returns(TestData.Players.PoorPlayer()); // 0 Power
            _state.MapManager.CanAssassinate(node, Arg.Any<Player>()).Returns(true);

            var command = new AssassinateCommand(node.Id);

            bool result = command.Validate(_state.MatchContext);

            Assert.IsFalse(result);
            _logger.Received(1).Log(
                Arg.Is<string>(s => s.Contains("AssassinateCommand") && s.Contains("Power")),
                LogChannel.Warning);
        }

        [TestMethod]
        public void EndTurnCommand_Validate_Logs_WhenTargetingInProgress()
        {
            _state.ActionSystem.IsTargeting().Returns(true);
            _state.ActionSystem.CurrentState.Returns(ActionState.TargetingAssassinate);
            var command = new EndTurnCommand();

            bool result = command.Validate(_state.MatchContext);

            Assert.IsFalse(result);
            _logger.Received(1).Log(
                Arg.Is<string>(s => s.Contains("EndTurnCommand") && s.Contains("TargetingAssassinate")),
                LogChannel.Warning);
        }

        [TestMethod]
        public void PlaceSpyCommand_Validate_Logs_WhenTargetSiteNotFound()
        {
            _state.MapManager.Sites.Returns(new List<Site>());
            var command = new PlaceSpyCommand(targetSiteId: 999);

            bool result = command.Validate(_state.MatchContext);

            Assert.IsFalse(result);
            _logger.Received(1).Log(
                Arg.Is<string>(s => s.Contains("PlaceSpyCommand") && s.Contains("999")),
                LogChannel.Warning);
        }

        [TestMethod]
        public void DiscardCardCommand_Validate_Logs_WhenTargetIsNotTheExpectedPlayer()
        {
            var red = TestData.Players.RedPlayer();
            var blue = TestData.Players.BluePlayer();
            _state.TurnManager.GetPlayerByColor(PlayerColor.Blue).Returns(blue);
            _state.TurnManager.ActivePlayer.Returns(red);

            var command = new DiscardCardCommand(PlayerColor.Blue, "some_card");

            bool result = command.Validate(_state.MatchContext);

            Assert.IsFalse(result);
            _logger.Received(1).Log(
                Arg.Is<string>(s => s.Contains("DiscardCardCommand") && s.Contains("Blue")),
                LogChannel.Warning);
        }

        [TestMethod]
        public void SelectOpponentCommand_Validate_Logs_WhenBelowEligibilityThreshold()
        {
            _state.ActionSystem.CurrentState.Returns(ActionState.TargetingOpponentSelect);
            var active = TestData.Players.RedPlayer();
            var target = TestData.Players.BluePlayer(); // empty hand by default
            _state.TurnManager.ActivePlayer.Returns(active);
            _state.TurnManager.GetPlayerByColor(PlayerColor.Blue).Returns(target);

            var command = new SelectOpponentCommand(PlayerColor.Blue);

            bool result = command.Validate(_state.MatchContext);

            Assert.IsFalse(result);
            _logger.Received(1).Log(
                Arg.Is<string>(s => s.Contains("SelectOpponentCommand") && s.Contains("eligibility threshold")),
                LogChannel.Warning);
        }

        [TestMethod]
        public void MoveTroopCommand_Validate_Logs_WhenSourceOrDestinationNodeNotFound()
        {
            var command = new MoveTroopCommand(sourceNodeId: 998, destinationNodeId: 999);

            bool result = command.Validate(_state.MatchContext);

            Assert.IsFalse(result);
            _logger.Received(1).Log(
                Arg.Is<string>(s => s.Contains("MoveTroopCommand") && s.Contains("998") && s.Contains("999")),
                LogChannel.Warning);
        }

        [TestMethod]
        public void Validate_DoesNotLogARejection_WhenTheCommandIsActuallyValid()
        {
            // Sanity check the helper isn't fired on the success path too (MatchContext's own
            // construction logs an unrelated "RNG initialized" line, so this asserts on the
            // rejection message specifically rather than "no log call at all").
            var node = TestData.MapNodes.Node1();
            _state.MapManager.Nodes.Returns(new List<MapNode> { node });
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanAssassinate(node, player).Returns(true);

            var command = new AssassinateCommand(node.Id);

            bool result = command.Validate(_state.MatchContext);

            Assert.IsTrue(result);
            _logger.DidNotReceive().Log(Arg.Is<string>(s => s.Contains("Validate() rejected")), Arg.Any<LogChannel>());
        }
    }
}
