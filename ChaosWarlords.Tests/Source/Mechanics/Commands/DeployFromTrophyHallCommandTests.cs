using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    // Unit-level Validate()/Execute() coverage for DeployFromTrophyHallCommand (EffectType.
    // DeployFromTrophyHall, Mummy Lord) - mirrors ReturnAnySpyCommandTests.cs's shape.
    [TestClass]
    [TestCategory("Unit")]
    public class DeployFromTrophyHallCommandTests
    {
        private TestGameplayState _state = null!;
        private Player _activePlayer = null!;
        private Player _sourcePlayer = null!;
        private MapNode _targetNode = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
            _activePlayer = new PlayerBuilder().WithColor(PlayerColor.Red).Build();
            _sourcePlayer = new PlayerBuilder().WithColor(PlayerColor.Blue).Build();
            _sourcePlayer.SetTrophyHall(1, PlayerColor.Neutral);

            _state.TurnManager.ActivePlayer.Returns(_activePlayer);
            _state.TurnManager.GetPlayerByColor(PlayerColor.Blue).Returns(_sourcePlayer);
            _state.TurnManager.GetPlayerByColor(PlayerColor.Red).Returns(_activePlayer);

            _targetNode = new MapNodeBuilder().WithId(1).Build();
            _state.MapManager.Nodes.Returns(new List<MapNode> { _targetNode });
            _state.MapManager.CanMoveDestination(_targetNode).Returns(true);

            var effect = new CardEffect(EffectType.DeployFromTrophyHall, 0) { TargetNeutralTroopOnly = true };
            _state.ActionSystem.CurrentSourceEffect.Returns(effect);
            _state.ActionSystem.PendingTrophyHallSourceColor.Returns(PlayerColor.Blue);
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenTargetNodeNotFound()
        {
            var command = new DeployFromTrophyHallCommand(targetNodeId: 999, PlayerColor.Blue, PlayerColor.Neutral);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenDestinationIsNotEmpty()
        {
            _state.MapManager.CanMoveDestination(_targetNode).Returns(false);
            var command = new DeployFromTrophyHallCommand(_targetNode.Id, PlayerColor.Blue, PlayerColor.Neutral);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsTrue_WhenEverythingMatches()
        {
            var command = new DeployFromTrophyHallCommand(_targetNode.Id, PlayerColor.Blue, PlayerColor.Neutral);

            Assert.IsTrue(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenCardRequiresNeutralButNonNeutralTroopColorRequested()
        {
            // Defense-in-depth: re-derives the neutral-only restriction from the currently
            // pending CardEffect rather than trusting the caller, matching AssassinateCommand/
            // SupplantCommand's own RequiresNeutralTarget pattern.
            _sourcePlayer.SetTrophyHall(1, PlayerColor.Blue);
            var command = new DeployFromTrophyHallCommand(_targetNode.Id, PlayerColor.Blue, PlayerColor.Blue);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenSourcePlayerColorDoesNotMatchThePendingResolvedSource()
        {
            // A forged command naming a DIFFERENT player than the one ActionSystem/
            // TrophyHallRuleEngine actually resolved when targeting opened.
            _state.TurnManager.GetPlayerByColor(PlayerColor.Black).Returns(new PlayerBuilder().WithColor(PlayerColor.Black).Build());
            var command = new DeployFromTrophyHallCommand(_targetNode.Id, PlayerColor.Black, PlayerColor.Neutral);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenSourcePlayerHasNoMatchingTroopInTheirTrophyHall()
        {
            // Stale/forged command: the source player's trophy hall no longer has that color
            // (e.g. already taken by an earlier dispatch of the same command - replay defense).
            _sourcePlayer.SetTrophyHall(0);
            var command = new DeployFromTrophyHallCommand(_targetNode.Id, PlayerColor.Blue, PlayerColor.Neutral);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenSourcePlayerColorDoesNotResolveToARealPlayer()
        {
            var command = new DeployFromTrophyHallCommand(_targetNode.Id, PlayerColor.Orange, PlayerColor.Neutral);
            _state.ActionSystem.PendingTrophyHallSourceColor.Returns(PlayerColor.Orange);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Execute_WhenTargetNodeNotFound_DoesNothing()
        {
            var command = new DeployFromTrophyHallCommand(targetNodeId: 999, PlayerColor.Blue, PlayerColor.Neutral);

            command.Execute(_state.MatchContext);

            _state.ActionSystem.DidNotReceiveWithAnyArgs().PerformDeployFromTrophyHall(default!, default, default, default);
        }

        [TestMethod]
        public void Execute_DelegatesToActionSystem_PerformDeployFromTrophyHall()
        {
            var command = new DeployFromTrophyHallCommand(_targetNode.Id, PlayerColor.Blue, PlayerColor.Neutral, cardId: "mummy_lord_abc123");

            command.Execute(_state.MatchContext);

            _state.ActionSystem.Received(1).PerformDeployFromTrophyHall(_targetNode, PlayerColor.Blue, PlayerColor.Neutral, "mummy_lord_abc123");
        }

        [TestMethod]
        public void ToDto_RoundTripsThroughDtoMapper()
        {
            var command = new DeployFromTrophyHallCommand(_targetNode.Id, PlayerColor.Blue, PlayerColor.Neutral, "mummy_lord_abc123");

            var dto = command.ToDto();
            var hydrated = ChaosWarlords.Source.Core.Utilities.CommandHydrator.HydrateCommand(dto, _state.MatchContext) as DeployFromTrophyHallCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);
            Assert.AreEqual(command.SourcePlayerColor, hydrated.SourcePlayerColor);
            Assert.AreEqual(command.TroopColor, hydrated.TroopColor);
            Assert.AreEqual(command.CardId, hydrated.CardId);
        }
    }
}
