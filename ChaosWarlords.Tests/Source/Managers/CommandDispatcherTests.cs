using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using System.Linq;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Managers
{
    [TestClass]
    public class CommandDispatcherTests
    {
        private IReplayManager _replayManager = null!;
        private IGameLogger _logger = null!;
        private CommandDispatcher _dispatcher = null!;
        private IGameplayState _state = null!;
        private IGameCommand _command = null!;

        [TestInitialize]
        public void Setup()
        {
            _replayManager = Substitute.For<IReplayManager>();
            _logger = Substitute.For<IGameLogger>();
            // Use Fake State
            // _state = Substitute.For<IGameplayState>();
            // We'll init the fake in each test or setup if possible, but the fake is lightweight.
            // Let's create a field for it but we need to reset it for each test if we shared it.
            // Better to instantiate in logic, but test fields are fine if re-init in setup.

            // Actually, let's keep the field type as IGameplayState interface for the dispatcher signature,
            // but assign the concrete fake.
            _state = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();

            _command = Substitute.For<IGameCommand>();
            _command.Validate(Arg.Any<MatchContext>()).Returns(true);

            _dispatcher = new CommandDispatcher(_replayManager, _logger);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Dispatch_WhenNotReplaying_RecordsAndExecutesCommand()
        {
            // Arrange
            _replayManager.IsReplaying.Returns(false);
            var player = new Player(PlayerColor.Red);
            var matchContext = CreateDispatchableContext(player);

            // Act
            _dispatcher.Dispatch(_command, matchContext);

            // Assert
            // 1. Verifies Recording (Sequence Number starts at 0, increments to 1). Dispatch
            // now reserves its recording slot via RecordingCount/InsertCommand rather than
            // calling RecordCommand directly - see CommandDispatcher.Dispatch's own comment
            // on why (nested-dispatch ordering). The mock's RecordingCount defaults to 0.
            _replayManager.Received(1).InsertCommand(0, _command, player, 1);

            // 2. Verifies Execution
            _command.Received(1).Execute(matchContext);

            // 3. Verify Context Sequence Updated
            Assert.AreEqual(1, matchContext.SequenceNumber);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Dispatch_WhenReplaying_ExecutesButDoesNotRecord()
        {
            // Arrange
            _replayManager.IsReplaying.Returns(true);
            var matchContext = CreateDispatchableContext(new Player(PlayerColor.Red));

            // Act
            _dispatcher.Dispatch(_command, matchContext);

            // Assert
            _replayManager.DidNotReceive().InsertCommand(Arg.Any<int>(), Arg.Any<IGameCommand>(), Arg.Any<Player>(), Arg.Any<int>());
            _command.Received(1).Execute(matchContext);
            Assert.AreEqual(1, matchContext.SequenceNumber); // Still increments sequence logic
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Dispatch_IncrementsSequenceCounter()
        {
            // Arrange
            _replayManager.IsReplaying.Returns(false);
            var player = new Player(PlayerColor.Red);
            var matchContext = CreateDispatchableContext(player);

            // Act
            _dispatcher.Dispatch(_command, matchContext); // seq 1
            _dispatcher.Dispatch(_command, matchContext); // seq 2

            // Assert (mock's RecordingCount always reads back 0, so both dispatches reserve
            // slot 0 - that's fine, this test only cares about the sequence numbers passed)
            _replayManager.Received().InsertCommand(0, _command, player, 1);
            _replayManager.Received().InsertCommand(0, _command, player, 2);
            Assert.AreEqual(2, matchContext.SequenceNumber);
        }
        [TestMethod]
        [TestCategory("Unit")]
        public void Dispatch_WhenExecutionFails_DoesNotRecord()
        {
            // Arrange
            _replayManager.IsReplaying.Returns(false);
            var player = new Player(PlayerColor.Red);
            var matchContext = CreateDispatchableContext(player);

            var failingCommand = Substitute.For<IGameCommand>();
            failingCommand.Validate(matchContext).Returns(true);
            failingCommand.When(c => c.Execute(matchContext)).Do(x => { throw new InvalidOperationException("Boom"); });

            // Act
            try
            {
                _dispatcher.Dispatch(failingCommand, matchContext);
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            // Assert
            // Should NOT have incremented sequence number (rolling back state ideally, but at least not skipping numbers in log)
            // Or if we increment before execution, we might have a gap.
            // The critical thing: ReplayManager should NOT receive the command.
            _replayManager.DidNotReceive().InsertCommand(Arg.Any<int>(), Arg.Any<IGameCommand>(), Arg.Any<Player>(), Arg.Any<int>());
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Dispatch_WhenExecutionFailsPartway_RollsBackMutationsAlreadyApplied()
        {
            // The test above (Dispatch_WhenExecutionFails_DoesNotRecord) only checks that a
            // failed command wasn't recorded to replay - it never checks that anything was
            // actually restored. This is the gap: verify the rollback CommandDispatcher's own
            // doc comment promises (StateRestorer.RestoreState on the pre-command snapshot)
            // actually reverts state a command already mutated before it threw. Uses a real
            // PlayerStateManager (not mocked) so the Power mutation - and its rollback - are
            // both real, not just "was this method called".

            // Arrange
            _replayManager.IsReplaying.Returns(false);
            var player = new Player(PlayerColor.Red);
            var turnManager = new TurnManager(
                new List<Player> { player },
                new SeededGameRandom(123, _logger),
                _logger);
            var mapManager = Substitute.For<IMapManager>();
            mapManager.Nodes.Returns(new List<ChaosWarlords.Source.Entities.Map.MapNode>());
            mapManager.Sites.Returns(new List<ChaosWarlords.Source.Entities.Map.Site>());
            var marketManager = Substitute.For<IMarketManager>();
            marketManager.MarketRow.Returns(new List<Card>());
            marketManager.MarketDeck.Returns(new List<Card>());
            var playerState = new PlayerStateManager(_logger);
            // Unlike the other tests in this file, this one needs a REAL ActionSystem (not a
            // bare Substitute): DtoMapper.ToGameStateDto reads ActionSystem.ExecutionStack,
            // and an unconfigured IActionSystem mock returns null for it (SerializeEffectStack
            // has no null-guard), which makes the pre-command snapshot throw and silently
            // disables rollback (CommandDispatcher.TryCreateSnapshot's documented best-effort
            // fallback) instead of exercising it - a real ActionSystem's ExecutionStack is
            // never null, so this is a test-double gap, not a production one.
            var actionSystem = new ActionSystem(turnManager, mapManager, _logger, playerState, marketManager);

            var matchContext = new MatchContext(
                turnManager,
                mapManager,
                marketManager,
                actionSystem,
                Substitute.For<ICardDatabase>(),
                playerState,
                _logger,
                123);
            actionSystem.SetMatchContext(matchContext);

            int powerBeforeCommand = player.Power;
            bool mutationRan = false;

            var partiallyMutatingCommand = Substitute.For<IGameCommand>();
            partiallyMutatingCommand.Validate(matchContext).Returns(true);
            partiallyMutatingCommand.When(c => c.Execute(matchContext)).Do(x =>
            {
                // Simulate a command that mutates state, THEN fails before finishing -
                // exactly the scenario the pre-execution snapshot/rollback exists for.
                playerState.AddPower(player, 5);
                _ = matchContext.Random.NextInt(1000);
                mutationRan = true;
                throw new InvalidOperationException("Boom");
            });

            // Act
            try
            {
                _dispatcher.Dispatch(partiallyMutatingCommand, matchContext);
                Assert.Fail("Expected the command's exception to propagate.");
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            // Assert
            Assert.IsTrue(mutationRan, "Setup check: the mutation must actually have run before the throw, or the assertion below would be trivially true.");
            var logCalls = string.Join(" | ", _logger.ReceivedCalls().Select(c => string.Join(",", c.GetArguments())));
            Assert.AreEqual(powerBeforeCommand, player.Power, $"Power gained before the command threw must be rolled back, not left applied. Log calls: {logCalls}");
            var controlRandom = new SeededGameRandom(123, _logger);
            Assert.AreEqual(controlRandom.NextInt(1000), matchContext.Random.NextInt(1000), "Rollback must restore the next deterministic RNG output after a failing command.");
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Dispatch_WhenExecutionFails_RestoresTurnOwnedStateAndPhysicalTransientMarkers()
        {
            var red = new Player(PlayerColor.Red) { SeatIndex = 0 };
            var blue = new Player(PlayerColor.Blue) { SeatIndex = 1 };
            var turnManager = new TurnManager(new List<Player> { red, blue }, new SeededGameRandom(123, _logger), _logger);
            var mapManager = Substitute.For<IMapManager>();
            mapManager.Nodes.Returns(new List<ChaosWarlords.Source.Entities.Map.MapNode>());
            mapManager.Sites.Returns(new List<ChaosWarlords.Source.Entities.Map.Site>());
            var marketManager = Substitute.For<IMarketManager>();
            marketManager.MarketRow.Returns(new List<Card>());
            marketManager.MarketDeck.Returns(new List<Card>());
            var cardDb = Substitute.For<ICardDatabase>();
            var source = new Card("rollback_source", "Rollback Source", 1, CardAspect.Sorcery, 0, 0, 0) { Location = CardLocation.Played };
            var target = new Card("rollback_target", "Rollback Target", 1, CardAspect.Sorcery, 0, 0, 0) { Location = CardLocation.Played };
            source.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.VictoryPoints));
            cardDb.GetCardById(Arg.Any<string>(), Arg.Any<IGameRandom?>()).Returns(call => call.Arg<string>() switch
            {
                "rollback_source" => source,
                "rollback_target" => target,
                _ => null
            });
            red.AddToPlayed(source);
            red.AddToPlayed(target);
            var playerState = new PlayerStateManager(_logger);
            var actionSystem = new ActionSystem(turnManager, mapManager, _logger, playerState, marketManager);
            var context = new MatchContext(turnManager, mapManager, marketManager, actionSystem, cardDb, playerState, _logger, 123);
            actionSystem.SetMatchContext(context);

            var turn = turnManager.CurrentTurnContext;
            var turnPlayerBeforeFailure = turn.ActivePlayer;
            turn.RecordPlayedCard(CardAspect.Sorcery);
            turn.AddPromotionCredit(source, 1, isOptional: true);
            context.RecordAction("BeforeFailure", "Checkpoint action");
            context.CardsMarkedForTurnEndDevour.Add(source);
            context.CardsMarkedForTurnEndPromote.Add(source);
            context.PendingOpponentDiscardTriggers.Add(source);
            turnManager.BeginForcedActingPlayer(blue);

            var failingCommand = Substitute.For<IGameCommand>();
            failingCommand.Validate(context).Returns(true);
            failingCommand.When(command => command.Execute(context)).Do(_callInfo =>
            {
                playerState.AddPower(red, 5);
                _ = context.Random.NextInt(1000);
                context.TurnManager.EndForcedActingPlayer();
                context.TurnManager.EndTurn();
                context.TurnManager.CurrentTurnContext.RecordPlayedCard(CardAspect.Shadow);
                context.TurnManager.CurrentTurnContext.ForfeitRemainingPromotions();
                context.RecordAction("AfterFailure", "Must disappear");
                context.CardsMarkedForTurnEndDevour.Clear();
                context.CardsMarkedForTurnEndPromote.Clear();
                context.PendingOpponentDiscardTriggers.Clear();
                throw new InvalidOperationException("Boom");
            });

            Assert.ThrowsExactly<InvalidOperationException>(() => _dispatcher.Dispatch(failingCommand, context));

            var restoredSource = red.PlayedCards.Single(card => card.RuntimeId == source.RuntimeId);
            Assert.AreEqual(0, red.Power);
            Assert.AreEqual(1, context.TurnManager.CurrentTurnContext.GetAspectCount(CardAspect.Sorcery));
            Assert.AreEqual(0, context.TurnManager.CurrentTurnContext.GetAspectCount(CardAspect.Shadow));
            Assert.AreEqual(1, context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
            Assert.HasCount(1, context.TurnManager.CurrentTurnContext.ActionHistory);
            Assert.AreSame(turnPlayerBeforeFailure, context.TurnManager.CurrentTurnContext.ActivePlayer);
            Assert.AreSame(blue, context.TurnManager.ForcedActingPlayer);
            Assert.AreSame(restoredSource, context.CardsMarkedForTurnEndDevour.Single());
            Assert.AreSame(restoredSource, context.CardsMarkedForTurnEndPromote.Single());
            Assert.AreSame(restoredSource, context.PendingOpponentDiscardTriggers.Single());
            Assert.AreEqual(new SeededGameRandom(123, _logger).NextInt(1000), context.Random.NextInt(1000));
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Dispatch_WhenRollbackSnapshotCannotBeCaptured_RejectsBeforeValidationOrExecution()
        {
            var partialContext = new MatchContextBuilder()
                .WithLogger(_logger)
                .WithSeed(123)
                .Build();

            Assert.ThrowsExactly<InvalidOperationException>(() => _dispatcher.Dispatch(_command, partialContext));

            _command.DidNotReceive().Validate(Arg.Any<MatchContext>());
            _command.DidNotReceive().Execute(Arg.Any<MatchContext>());
        }

        private MatchContext CreateDispatchableContext(Player player)
        {
            var turnManager = new TurnManager(
                new List<Player> { player },
                new SeededGameRandom(123, _logger),
                _logger);
            var mapManager = Substitute.For<IMapManager>();
            mapManager.Nodes.Returns(new List<ChaosWarlords.Source.Entities.Map.MapNode>());
            mapManager.Sites.Returns(new List<ChaosWarlords.Source.Entities.Map.Site>());
            var marketManager = Substitute.For<IMarketManager>();
            marketManager.MarketRow.Returns(new List<Card>());
            marketManager.MarketDeck.Returns(new List<Card>());
            var playerState = new PlayerStateManager(_logger);
            var actionSystem = new ActionSystem(turnManager, mapManager, _logger, playerState, marketManager);
            var context = new MatchContext(
                turnManager,
                mapManager,
                marketManager,
                actionSystem,
                Substitute.For<ICardDatabase>(),
                playerState,
                _logger,
                123);
            actionSystem.SetMatchContext(context);
            return context;
        }
    }
}
