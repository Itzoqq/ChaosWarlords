using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Factories;
using ChaosWarlords.Source.Managers;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;

namespace ChaosWarlords.Core.Tests.Source.Integration
{
    [TestClass]
    [TestCategory("Integration")]
    public class HeadlessLifecycleSoakTests
    {
        private const int IterationCount = 100;
        private const int Seed = 20260913;

        [TestMethod]
        public void FreshMatches_SetupReplayAndTeardownRemainIsolatedAcrossOneHundredIterations()
        {
            string? expectedHash = null;

            for (int iteration = 0; iteration < IterationCount; iteration++)
            {
                var logger = NullTestLogger.Instance;
                var liveReplayManager = new ReplayManager(logger);
                var (liveContext, liveDispatcher) = BuildMatch(CreateCardDatabase(), logger, liveReplayManager);
                liveReplayManager.InitializeRecording(liveContext.Seed);

                int setupCompletionCount = 0;
                Action onSetupDeploymentComplete = () =>
                {
                    setupCompletionCount++;
                    liveDispatcher.Dispatch(new EndTurnCommand(), liveContext);
                };
                liveContext.MapManager.OnSetupDeploymentComplete += onSetupDeploymentComplete;

                foreach (var player in liveContext.TurnManager.Players)
                {
                    var node = liveContext.MapManager.Nodes.First(node => liveContext.MapManager.CanDeployAt(node, player.Color));
                    var deploy = new DeployTroopCommand(node.Id);
                    Assert.IsTrue(deploy.Validate(liveContext), $"Setup deploy must validate on iteration {iteration} for {player.Color}.");
                    liveDispatcher.Dispatch(deploy, liveContext);
                }

                Assert.AreEqual(
                    liveContext.TurnManager.Players.Count,
                    setupCompletionCount,
                    $"Setup completion must run once for each player on iteration {iteration}.");
                string liveHash = liveContext.GetStateHash();
                expectedHash ??= liveHash;
                Assert.AreEqual(expectedHash, liveHash, $"Fresh match {iteration} retained state from an earlier match.");

                var replayReplayManager = new ReplayManager(logger);
                var (replayContext, _) = BuildMatch(CreateCardDatabase(), logger, replayReplayManager);
                replayReplayManager.StartReplay(liveReplayManager.GetRecordingJson());

                int replayedCommandCount = 0;
                while (replayReplayManager.IsReplaying)
                {
                    var command = replayReplayManager.GetNextCommand(replayContext);
                    if (command is null)
                    {
                        Assert.IsFalse(
                            replayReplayManager.IsReplaying,
                            $"Setup replay command {replayedCommandCount} could not be hydrated on iteration {iteration}.");
                        break;
                    }

                    replayContext.SequenceNumber++;
                    command.Execute(replayContext);
                    replayedCommandCount++;
                }

                Assert.AreEqual(
                    liveContext.TurnManager.Players.Count * 2,
                    replayedCommandCount,
                    $"Setup replay must contain one deploy and one automatic end turn per player on iteration {iteration}.");
                Assert.AreEqual(liveHash, replayContext.GetStateHash(), $"Replay diverged from its isolated live match on iteration {iteration}.");

                liveContext.MapManager.OnSetupDeploymentComplete -= onSetupDeploymentComplete;
            }
        }

        private static ICardDatabase CreateCardDatabase()
        {
            var cardDatabase = Substitute.For<ICardDatabase>();
            cardDatabase.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(_ => new List<Card>());
            cardDatabase.GetMarketCards(Arg.Any<ChaosWarlords.Source.Core.Contexts.MarketDeckSelection>(), Arg.Any<IGameRandom>())
                .Returns(_ => new List<Card>());
            return cardDatabase;
        }

        private static (MatchContext Context, CommandDispatcher Dispatcher) BuildMatch(
            ICardDatabase cardDatabase,
            IGameLogger logger,
            IReplayManager replayManager)
        {
            var world = new MatchFactory(cardDatabase, logger).Build(replayManager, Seed);
            var context = new MatchContext(
                world.TurnManager,
                world.MapManager,
                world.MarketManager,
                world.ActionSystem,
                cardDatabase,
                world.PlayerStateManager,
                logger,
                world.Seed);
            world.ActionSystem.SetMatchContext(context);
            world.ActionSystem.SetMatchManager(new MatchManager(context, logger, Substitute.For<IVictoryManager>()));

            return (context, new CommandDispatcher(replayManager, logger));
        }
    }
}
