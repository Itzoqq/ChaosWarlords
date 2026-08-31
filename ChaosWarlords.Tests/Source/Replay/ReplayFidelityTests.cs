using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Factories;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Replay
{
    /// <summary>
    /// Phase 1 of the pre-networking roadmap: prove the "headless, deterministic,
    /// replay-based" architecture actually holds end-to-end, rather than assuming it does
    /// because the individual pieces (SeededGameRandom, StateHasher, DtoMapper) each look
    /// right in isolation.
    ///
    /// Unlike ReplayDesyncTests.cs (manually replicates a couple of command effects by hand
    /// and only checks RNG call-counts for one turn) or ReplayScenarioTests.cs/
    /// ReplaySystemTests.cs (recording/DTO-shape only), this plays a real multi-turn game
    /// through the actual CommandDispatcher, records it, replays the recording into a
    /// completely separate MatchContext (same seed, independently constructed), and asserts
    /// MatchContext.GetStateHash() matches after EVERY command - not just at the end, so a
    /// divergence points at exactly which command caused it instead of "somewhere in the
    /// game".
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class ReplayFidelityTests
    {
        private const int RoundsToPlay = 3;

        [TestMethod]
        public void LiveGame_And_ReplayedGame_ProduceIdenticalStateHash_AfterEveryCommand()
        {
            var cardDatabase = LoadRealCardDatabase();
            var logger = ChaosWarlords.Tests.Utilities.TestLogger.Instance;
            const int seed = 20260901;

            // ---------------------------------------------------------------
            // "Server": play a real game through the real command pipeline.
            // ---------------------------------------------------------------
            var liveReplayManager = new ReplayManager(logger);
            var (liveContext, liveDispatcher) = BuildMatch(cardDatabase, logger, liveReplayManager, seed);
            liveReplayManager.InitializeRecording(liveContext.Seed);

            var liveHashes = PlayScriptedGame(liveContext, liveDispatcher, RoundsToPlay);
            string recordedJson = liveReplayManager.GetRecordingJson();

            Assert.IsGreaterThan(10, liveHashes.Count, "Sanity check: the scripted game should have dispatched a non-trivial number of commands.");

            // ---------------------------------------------------------------
            // "Client": replay the SAME recording into a fresh, independently
            // constructed MatchContext (same seed, nothing shared with the live one).
            // ---------------------------------------------------------------
            var replayReplayManager = new ReplayManager(logger);
            var (replayContext, _) = BuildMatch(cardDatabase, logger, replayReplayManager, seed);
            replayReplayManager.StartReplay(recordedJson);

            // Loop on IsReplaying, NOT "GetNextCommand returned non-null" - GetNextCommand
            // returns null both when the queue is genuinely empty (IsReplaying then becomes
            // false, via ReplayManager.StopReplay) AND when a single command fails to
            // hydrate (IsReplaying stays true, that one entry is just skipped and logged).
            // Mirrors how ReplayController.Update actually drives playback in production
            // (its outer loop is gated on _replayManager.IsReplaying, not on the last
            // GetNextCommand call's return value) - looping on the null check instead would
            // silently stop at the first hydration hiccup rather than reporting a real
            // command-count/hash mismatch for it.
            var replayHashes = new System.Collections.Generic.List<(string CommandType, string Hash)>();
            while (replayReplayManager.IsReplaying)
            {
                var cmd = replayReplayManager.GetNextCommand(replayContext);
                if (cmd == null) continue; // hydration failure for this one entry - logged by ReplayManager itself

                // Mirrors ReplayController.UpdatePlayback: replay executes the hydrated
                // command directly against MatchContext, bypassing CommandDispatcher
                // (which would re-validate and re-record - wrong for playback). But it must
                // still increment SequenceNumber itself, matching what Dispatch does for
                // every live command - see ReplayController.UpdatePlayback's comment.
                replayContext.SequenceNumber++;
                cmd.Execute(replayContext);
                replayHashes.Add((cmd.GetType().Name, replayContext.GetStateHash()));
            }

            // ---------------------------------------------------------------
            // Compare checkpoint by checkpoint, not just the final state, so a
            // divergence identifies which command caused it.
            // ---------------------------------------------------------------
            Assert.HasCount(liveHashes.Count, replayHashes, "Replay produced a different number of commands than were recorded live.");

            for (int i = 0; i < liveHashes.Count; i++)
            {
                Assert.AreEqual(
                    liveHashes[i].CommandType, replayHashes[i].CommandType,
                    $"Command #{i} type mismatch between live and replay.");
                Assert.AreEqual(
                    liveHashes[i].Hash, replayHashes[i].Hash,
                    $"State hash diverged at command #{i} ({liveHashes[i].CommandType}) - " +
                    "live and replay agreed on every command before this one and disagree here.\n" +
                    $"--- LIVE ---\n{DumpState(liveContext)}\n--- REPLAY ---\n{DumpState(replayContext)}");
            }
        }

        private static ICardDatabase LoadRealCardDatabase()
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/cards.json");
            if (!System.IO.File.Exists(path))
            {
                Assert.Inconclusive("cards.json not found at " + path);
            }
            var database = new CardDatabase();
            using (var stream = System.IO.File.OpenRead(path))
            {
                database.Load(stream);
            }
            return database;
        }

        private static (MatchContext Context, CommandDispatcher Dispatcher) BuildMatch(ICardDatabase cardDatabase, IGameLogger logger, IReplayManager replayManager, int seed)
        {
            var factory = new MatchFactory(cardDatabase, logger);
            var world = factory.Build(replayManager, seed);

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

            var matchManager = new MatchManager(context, logger, Substitute.For<IVictoryManager>());
            world.ActionSystem.SetMatchManager(matchManager);

            var dispatcher = new CommandDispatcher(replayManager, logger);

            // Auto-decline every optional effect (Wight/Market Corruptor/Skeletal Horde/
            // Cultist of Myrkul all have one) - keeps the scripted playthrough's decision
            // tree small while still exercising the full push/pop stack machinery for each.
            // Safe to leave wired for the replay context too: HandleOptionalEffectDeclined
            // only runs when a live OnInteractionRequested fires, which never happens during
            // replay (replayed commands re-execute the ALREADY-decided outcome, they don't
            // re-raise the original decision point).
            world.ActionSystem.OnInteractionRequested += req => req.OnResponse(false);

            // NOTE: unlike production (GameplayState), OnAutoExecuteCommand and
            // MapManager.OnSetupDeploymentComplete are deliberately NOT wired here - both
            // need to route through PlayScriptedGame's own Run() so a nested/auto-triggered
            // command still gets a hash checkpoint recorded for it (see PlayScriptedGame).
            // Wiring them here would dispatch+record the command correctly but silently skip
            // adding it to liveHashes, causing a live/replay checkpoint-count mismatch even
            // though the recording itself would be correct.

            return (context, dispatcher);
        }

        private static System.Collections.Generic.List<(string CommandType, string Hash)> PlayScriptedGame(MatchContext context, CommandDispatcher dispatcher, int rounds)
        {
            var hashes = new System.Collections.Generic.List<(string, string)>();

            // Returns whether the command actually got recorded, so callers looping on "keep
            // doing this while some condition holds" (e.g. the Power-spending deploy loop
            // below) can stop instead of retrying an invalid command forever. Pre-validates
            // rather than relying on CommandDispatcher's own internal Validate() call,
            // because Dispatch() itself doesn't report success/failure - it only logs and
            // returns early.
            //
            // Captures a checkpoint right after Dispatch() returns - correct for the common
            // case (a command whose Execute() doesn't trigger a nested Dispatch() call), but
            // deliberately NOT used for the Setup-phase deploy below. Dispatch() doesn't
            // RETURN until Execute() - and anything Execute() synchronously triggers,
            // including a nested Dispatch() call - has FULLY finished. There is no
            // observation point between "this command's own effect" and "what it triggered"
            // reachable from outside Execute() at all (CommandDispatcher.OnCommandRecorded
            // included - it fires once Dispatch() itself is about to return, which is
            // already too late for the OUTER command in a nested pair). A checkpoint
            // captured here for such a command would already include the nested command's
            // effects too (e.g. ActivePlayer already switched by a nested EndTurn) - but
            // replay executes the two recorded entries SEPARATELY, one Execute() call each,
            // so its checkpoints would never match. The only correct fix is to capture the
            // outer command's checkpoint manually, at whatever domain event actually
            // separates its own effect from what it triggers - see the
            // OnSetupDeploymentComplete handler below for the concrete case this test hits.
            bool Run(IGameCommand cmd)
            {
                if (!cmd.Validate(context)) return false;
                dispatcher.Dispatch(cmd, context);
                hashes.Add((cmd.GetType().Name, context.GetStateHash()));
                return true;
            }

            // Mirrors GameplayState.HandleSetupDeploymentComplete: MapManager.TryDeploy
            // doesn't advance the turn itself during Setup - it just raises
            // OnSetupDeploymentComplete and leaves ending the turn to a subscriber (its own
            // comment there explains why: during replay this must NOT auto-generate a second
            // EndTurnCommand, since the recorded stream already has the one from the live
            // run - satisfied here simply by never wiring this subscription on the replay
            // context at all, since PlayScriptedGame only ever runs for the live side).
            //
            // This event fires synchronously from INSIDE DeployTroopCommand.Execute() (via
            // MapManager.HandlePostDeployment), at exactly the boundary between "the deploy's
            // own effect is done" and "EndTurn is about to be auto-triggered" - the ONE place
            // this test can correctly capture the deploy's OWN isolated checkpoint, matching
            // what replay will see when it executes just the Deploy DTO (which never reaches
            // this handler in the first place - no subscription exists on the replay
            // context, so OnSetupDeploymentComplete firing there is a pure no-op). The
            // setup-phase loop below dispatches its DeployTroopCommand directly (bypassing
            // Run()'s own automatic checkpoint) specifically so this handler's manual
            // checkpoint is the ONLY one recorded for it.
            context.MapManager.OnSetupDeploymentComplete += () =>
            {
                if (context.CurrentPhase == MatchPhase.Setup)
                {
                    hashes.Add(("DeployTroopCommand", context.GetStateHash()));
                    Run(new EndTurnCommand());
                }
            };

            // Same reasoning for pre-target auto-execution chains (unused by this scripted
            // playthrough today, since every optional devour is auto-declined and no card
            // reaches a mandatory pre-commit path - wired anyway so this test fails loudly
            // instead of silently under-covering if that ever changes). Safe to route through
            // Run()'s automatic capture: unlike Setup's deploy, the effect that triggers this
            // (an optional-effect accept) has no OWN separate checkpoint to isolate here -
            // ProcessOptionalEffect's caller never captures one either.
            context.ActionSystem.OnAutoExecuteCommand += autoCmd => Run(autoCmd);

            // --- Setup phase: each player deploys their first troop. Dispatched directly
            // (not via Run()) - see OnSetupDeploymentComplete's comment above for why: that
            // handler captures this command's own checkpoint at the correct boundary, and a
            // second one captured here (after Dispatch returns, i.e. after EndTurn has ALSO
            // already run) would be a redundant, wrong, combined-state duplicate. ---
            foreach (var player in context.TurnManager.Players)
            {
                var node = FindDeployableNode(context, player);
                Assert.IsNotNull(node, $"Expected an empty deployable node for {player.Color} during initial setup.");
                Assert.IsTrue(new DeployTroopCommand(node).Validate(context), $"Initial deploy for {player.Color} should always validate.");
                dispatcher.Dispatch(new DeployTroopCommand(node), context);
            }

            // --- Playing phase: script `rounds` full rounds (every player gets a turn). ---
            for (int round = 0; round < rounds; round++)
            {
                for (int p = 0; p < context.TurnManager.Players.Count; p++)
                {
                    var player = context.TurnManager.ActivePlayer;

                    // Play every card currently in hand. Break rather than loop forever if a
                    // card ever fails to validate (Hand.Count would otherwise never shrink).
                    while (player.Hand.Count > 0)
                    {
                        var card = player.Hand[0];
                        if (!Run(new PlayCardCommand(card))) break;
                    }

                    // Spend any free troops granted by cards played this turn.
                    while (player.PendingFreeTroops > 0)
                    {
                        var node = FindDeployableNode(context, player);
                        if (node == null) break;
                        if (!Run(new DeployTroopCommand(node))) break;
                    }

                    // Spend a little Power on deployment too, bounded so a stray site-control
                    // interaction can't turn this into an unbounded loop.
                    for (int i = 0; i < 3 && player.Power >= 1; i++)
                    {
                        var node = FindDeployableNode(context, player);
                        if (node == null) break;
                        if (!Run(new DeployTroopCommand(node))) break;
                    }

                    bool endedTurn = Run(new EndTurnCommand());
                    Assert.IsTrue(endedTurn, $"EndTurnCommand should always validate for {player.Color} in this scripted playthrough.");
                }
            }

            return hashes;
        }

        private static MapNode? FindDeployableNode(MatchContext context, Player player)
        {
            return context.MapManager.Nodes.FirstOrDefault(n => context.MapManager.CanDeployAt(n, player.Color));
        }

        private static string DumpState(MatchContext context)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Phase={context.CurrentPhase} ActivePlayer={context.TurnManager.ActivePlayer.Color} Seq={context.SequenceNumber} TurnNum={context.CurrentTurnNumber}");
            foreach (var p in context.TurnManager.Players)
            {
                sb.AppendLine($"  {p.Color}(Seat {p.SeatIndex}): Power={p.Power} Influence={p.Influence} VP={p.VictoryPoints} Troops={p.TroopsInBarracks} HandCount={p.Hand.Count} Hand=[{string.Join(",", p.Hand.Select(c => c.Id))}]");
            }
            foreach (var n in context.MapManager.Nodes.Where(n => n.Occupant != PlayerColor.None))
            {
                sb.AppendLine($"  Node {n.Id}: Occupant={n.Occupant}");
            }
            sb.AppendLine($"  Market=[{string.Join(",", context.MarketManager.MarketRow.Select(c => c.Id))}]");
            return sb.ToString();
        }
    }
}
