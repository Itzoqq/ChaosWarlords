using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Factories;
using ChaosWarlords.Source.Managers;
using NSubstitute;
using System.Linq;

namespace ChaosWarlords.Core.Tests.Source.Integration
{
    /// <summary>
    /// This test's real point isn't any individual assertion below - it's that this whole
    /// file compiles and runs at all, inside a project (ChaosWarlords.Core.Tests) whose own
    /// .csproj references ONLY ChaosWarlords.Core.csproj, never the MonoGame-dependent client
    /// project. That makes "Core is headless" a compiler-enforced property rather than just a
    /// documented convention: if MatchFactory, MatchContext, CommandDispatcher, or anything
    /// they touch ever regained a dependency on MonoGame (directly or transitively), this
    /// project would fail to build - not just this test, the whole assembly. See planning.txt.
    ///
    /// Builds a real (if minimal) match via MatchFactory, dispatches a couple of real commands
    /// through a real CommandDispatcher, and checks the resulting state actually changed -
    /// exercising the full composition root (MatchFactory -> MatchContext -> TurnManager ->
    /// MapManager -> ActionSystem -> MatchManager -> CommandDispatcher), not just that the
    /// types involved happen to compile in isolation.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class HeadlessCompositionSmokeTests
    {
        [TestMethod]
        public void MatchFactory_BuildsAMatch_AndCommandDispatcherRunsRealCommands()
        {
            var logger = NullTestLogger.Instance;
            var cardDatabase = Substitute.For<ICardDatabase>();
            cardDatabase.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new System.Collections.Generic.List<Card>());
            cardDatabase.GetMarketCards(Arg.Any<ChaosWarlords.Source.Core.Contexts.MarketDeckSelection>(), Arg.Any<IGameRandom>()).Returns(new System.Collections.Generic.List<Card>());
            var replayManager = new ReplayManager(logger);

            var world = new MatchFactory(cardDatabase, logger).Build(replayManager, seed: 20260901);

            var context = new MatchContext(
                world.TurnManager, world.MapManager, world.MarketManager, world.ActionSystem,
                cardDatabase, world.PlayerStateManager, logger, world.Seed);
            world.ActionSystem.SetMatchContext(context);
            var matchManager = new MatchManager(context, logger, Substitute.For<IVictoryManager>());
            world.ActionSystem.SetMatchManager(matchManager);

            var dispatcher = new CommandDispatcher(replayManager, logger);

            // Sanity: a real world actually got built. MatchFactory.Build deals each player's
            // rulebook-required 5-card opening hand itself (planning.txt TIER 1 item 12) -
            // CreateDefaultPlayer always adds 3 Soldiers + 7 Nobles (10 cards, regardless of the
            // mocked ICardDatabase - those two come from CardFactory directly, not a database
            // lookup) and Build then draws 5 of them into Hand, so Deck + Hand together should
            // still total that same 10, headlessly, with no GameplayState/UI involved at all.
            Assert.HasCount(2, context.TurnManager.Players, "MatchFactory should create 2 players.");
            var firstPlayer = context.TurnManager.ActivePlayer;
            Assert.HasCount(5, firstPlayer.Hand, "MatchFactory.Build should deal a real 5-card opening hand.");
            Assert.AreEqual(10, firstPlayer.DeckManager.DrawPile.Count + firstPlayer.Hand.Count, "Deck + Hand should still total the full starting deck (3 Soldiers + 7 Nobles).");

            // Dispatch a real DeployTroopCommand through the real pipeline.
            var node = context.MapManager.Nodes.First(n => context.MapManager.CanDeployAt(n, firstPlayer.Color));
            dispatcher.Dispatch(new DeployTroopCommand(node.Id), context);

            Assert.AreEqual(firstPlayer.Color, node.Occupant, "DeployTroopCommand should have occupied the target node.");
            Assert.AreEqual(1, context.SequenceNumber, "CommandDispatcher should have advanced SequenceNumber for the dispatched command.");

            // A state hash can be computed at all - proves GetStateHash's full map/player/
            // market traversal runs without throwing on a freshly-built, freshly-mutated world.
            string hash = context.GetStateHash();
            Assert.IsFalse(string.IsNullOrEmpty(hash));
        }
    }
}
