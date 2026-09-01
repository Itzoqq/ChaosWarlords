using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Factories;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Reusable functional/scenario test harness for card mechanics (see planning.txt TIER 1,
    /// "2026-09-01: real functional/scenario test harness"). Builds a REAL match via
    /// MatchFactory, using the REAL cards.json loaded into a REAL CardDatabase (not a
    /// Substitute, not a hand-typed CardEffect tree), and drives it through a REAL
    /// CommandDispatcher - Validate()/Execute()/recording/rollback-on-exception are all
    /// genuinely exercised, the same composition HeadlessCompositionSmokeTests.cs already
    /// proves works end to end.
    ///
    /// This exists to close two specific, nameable gaps that let real shipped bugs (Carrion
    /// Crawler's market never opening; Wight/Cultist of Myrkul's broken mutual-exclusivity)
    /// slip past a fully green test suite:
    ///  1. Tests calling ActionSystem methods or command.Execute(context) directly instead of
    ///     going through PlayCardCommand -> CommandDispatcher, the actual path a player's
    ///     click produces.
    ///  2. Tests hand-typing a card's CardEffect tree in a C# helper instead of loading it
    ///     from the shipped cards.json, which can silently drift from what's actually shipped.
    ///
    /// Usage sketch:
    ///   var scenario = MatchScenario.Build();
    ///   var red = scenario.AsActivePlayer(PlayerColor.Red);
    ///   var wight = scenario.GiveCard(PlayerColor.Red, "wight");
    ///   scenario.PlayCard(wight);
    ///   scenario.RespondToLatestInteraction(accept: true);
    ///   ... assert on scenario.Context / scenario.Player(PlayerColor.Red) ...
    /// </summary>
    public sealed class MatchScenario
    {
        public MatchContext Context { get; }
        public CommandDispatcher Dispatcher { get; }
        public ICardDatabase CardDatabase { get; }
        public IReplayManager ReplayManager { get; }

        /// <summary>
        /// Every InteractionRequest raised so far (e.g. a card's optional/Alternative "accept
        /// or decline?" popup) - captured here instead of requiring every test to wire its own
        /// subscriber.
        /// </summary>
        public List<InteractionRequest> Interactions { get; } = new();

        /// <summary>
        /// Every reason string raised via ActionSystem.OnActionFailed/NotifyFailure so far -
        /// useful for adversarial scenarios asserting a rejected action failed for the
        /// expected reason rather than just "nothing happened".
        /// </summary>
        public List<string> ActionFailures { get; } = new();

        private MatchScenario(MatchContext context, CommandDispatcher dispatcher, ICardDatabase cardDatabase, IReplayManager replayManager)
        {
            Context = context;
            Dispatcher = dispatcher;
            CardDatabase = cardDatabase;
            ReplayManager = replayManager;
            Context.ActionSystem.OnInteractionRequested += req => Interactions.Add(req);
            Context.ActionSystem.OnActionFailed += (_, reason) => ActionFailures.Add(reason);
        }

        /// <summary>
        /// Builds a fresh 2-player (Red/Blue) match: real cards.json, real MatchFactory-wired
        /// TurnManager/MapManager/MarketManager/ActionSystem/PlayerStateManager, real
        /// MatchManager, real CommandDispatcher. Seat order is randomized per-seed by
        /// TurnManager - don't assume Red goes first, use AsActivePlayer instead.
        /// </summary>
        /// <param name="seed">Deterministic RNG seed for the match.</param>
        /// <param name="playerColors">Optional 2-4 player roster (defaults to MatchFactory's
        /// own default of [Red, Blue]) - passed straight through to MatchFactory.Build. Added
        /// for scenarios that genuinely need a 3rd/4th seat (e.g. a "target a player" primitive
        /// where the boundary case under test requires an INELIGIBLE opponent to exist
        /// alongside an ELIGIBLE one, which 2 players alone can't express - see
        /// CraniumRatsScenarioTests).</param>
        public static MatchScenario Build(int? seed = 20260901, IReadOnlyList<PlayerColor>? playerColors = null)
        {
            var logger = ChaosWarlords.Tests.Utilities.TestLogger.Instance;

            var localizationService = new LocalizationManager();
            using (var locStream = File.OpenRead(ResolveLocalizationJsonPath()))
            {
                localizationService.Load(locStream);
            }

            var cardDatabase = new CardDatabase(localizationService);
            using (var stream = File.OpenRead(ResolveCardsJsonPath()))
            {
                cardDatabase.Load(stream);
            }

            var replayManager = new ReplayManager(logger);
            var world = new MatchFactory(cardDatabase, logger).Build(replayManager, seed, playerColors);

            var context = new MatchContext(
                world.TurnManager, world.MapManager, world.MarketManager, world.ActionSystem,
                cardDatabase, world.PlayerStateManager, logger, world.Seed);
            world.ActionSystem.SetMatchContext(context);

            var matchManager = new MatchManager(context, logger, new VictoryManager(logger));
            world.ActionSystem.SetMatchManager(matchManager);

            var dispatcher = new CommandDispatcher(replayManager, logger);
            return new MatchScenario(context, dispatcher, cardDatabase, replayManager);
        }

        private static string ResolveCardsJsonPath()
        {
            // Same relative-path convention CardDatabaseIntegrationTests.cs already uses -
            // assumes tests run from bin/Debug/net10.0 under ChaosWarlords.Tests.
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/cards.json");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "cards.json not found - the scenario harness requires the REAL card data (see planning.txt TIER 1), not a Substitute.",
                    path);
            }
            return path;
        }

        private static string ResolveLocalizationJsonPath()
        {
            // Same relative-path convention as ResolveCardsJsonPath above - Name/Description
            // live here, not in cards.json (see planning.txt's localization design).
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/localization/en_US.json");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "en_US.json not found - the scenario harness requires the REAL localization bundle, not a Substitute.",
                    path);
            }
            return path;
        }

        public Player Player(PlayerColor color) =>
            Context.TurnManager.Players.First(p => p.Color == color);

        /// <summary>
        /// Pulls the REAL card instance for <paramref name="cardId"/> out of the REAL database
        /// (not a hand-typed stand-in) and adds it to <paramref name="color"/>'s hand.
        /// </summary>
        public Card GiveCard(PlayerColor color, string cardId)
        {
            var card = CardDatabase.GetCardById(cardId, Context.Random)
                ?? throw new InvalidOperationException($"No card with id '{cardId}' in cards.json - check the id.");
            Player(color).AddToHand(card);
            return card;
        }

        /// <summary>
        /// Advances real turns (TurnManager.EndTurn) until <paramref name="color"/> is
        /// ActivePlayer, then returns that Player for convenience. Bare TurnManager.EndTurn,
        /// not MatchManager.EndTurn - this is scenario SETUP (whose turn the test starts on),
        /// not the thing under test, so it deliberately skips CleanUpTurn/opponent-discard/etc.
        /// </summary>
        public Player AsActivePlayer(PlayerColor color)
        {
            int guard = 0;
            while (Context.ActivePlayer.Color != color)
            {
                Context.TurnManager.EndTurn();
                if (++guard > Context.TurnManager.Players.Count)
                {
                    throw new InvalidOperationException($"No player with color {color} in this match.");
                }
            }
            return Context.ActivePlayer;
        }

        /// <summary>
        /// Dispatches a command through the REAL CommandDispatcher (Validate/Execute/record/
        /// rollback-on-exception all genuinely exercised) - the same path a real player's
        /// click produces. Use directly (rather than PlayCard/ClickTarget) for adversarial
        /// scenarios that build an illegal command by hand.
        /// </summary>
        public void Dispatch(IGameCommand command) => Dispatcher.Dispatch(command, Context);

        public void PlayCard(Card card) => Dispatch(new PlayCardCommand(card));

        /// <summary>
        /// Routes a map-node/site click through the REAL ActionSystem.HandleTargetClick (the
        /// same call TargetingInputMode makes from a real click) and dispatches the resulting
        /// command through the real CommandDispatcher, if one was produced. Returns the
        /// command (null means the click was rejected outright by the targeting state machine
        /// - assert on that directly for negative/invalid-target scenarios).
        /// </summary>
        public IGameCommand? ClickTarget(MapNode? node, Site? site)
        {
            var command = Context.ActionSystem.HandleTargetClick(node, site);
            if (command != null)
            {
                Dispatch(command);
            }
            return command;
        }

        /// <summary>
        /// Routes a hand-card click through ActionSystem.HandleDevourSelection (the real
        /// "pick this card to devour" path) and dispatches the resulting command.
        /// </summary>
        public IGameCommand? SelectDevourCard(Card? card)
        {
            var command = Context.ActionSystem.HandleDevourSelection(card);
            if (command != null)
            {
                Dispatch(command);
            }
            return command;
        }

        /// <summary>
        /// Responds to the MOST RECENTLY raised OnInteractionRequested popup.
        /// </summary>
        public void RespondToLatestInteraction(bool accept)
        {
            if (Interactions.Count == 0)
            {
                throw new InvalidOperationException("No interaction has been requested yet.");
            }
            Interactions[^1].OnResponse(accept);
        }

        /// <summary>
        /// Dispatches <paramref name="command"/> and asserts it was REJECTED - SequenceNumber
        /// and GetStateHash() must both be unchanged from immediately before the dispatch.
        /// The standard adversarial-scenario assertion (see planning.txt TIER 1/section 2's
        /// testing policy): wrong player, a stale/nonexistent target, insufficient resources,
        /// or a replayed command should all fail this way - Validate() rejects, nothing
        /// mutates, SequenceNumber doesn't advance. Use this instead of hand-rolling the same
        /// "capture before, dispatch, assert unchanged" boilerplate per test.
        /// </summary>
        public void AssertRejected(IGameCommand command, string? because = null)
        {
            long sequenceBefore = Context.SequenceNumber;
            string hashBefore = Context.GetStateHash();

            Dispatch(command);

            Assert.AreEqual(
                sequenceBefore, Context.SequenceNumber,
                because ?? "A rejected command must not advance SequenceNumber.");
            Assert.AreEqual(
                hashBefore, Context.GetStateHash(),
                because ?? "A rejected command must not change any game state.");
        }

        /// <summary>
        /// Dispatches <paramref name="command"/> once (the caller is expected to assert its
        /// own effects happened as usual after this call returns), then dispatches the SAME
        /// command instance again immediately - a stale/replayed command re-sent after it
        /// already resolved. Asserts the SECOND dispatch changed nothing further
        /// (SequenceNumber/state hash both frozen at their post-first-dispatch values) - the
        /// double-spend/replay scenario planning.txt's testing policy requires every card to
        /// guard against (see section 6.C.2 - this was ZERO-coverage anywhere in the suite
        /// before TIER 1's audit).
        /// </summary>
        public void DispatchTwice(IGameCommand command)
        {
            Dispatch(command);

            long sequenceAfterFirst = Context.SequenceNumber;
            string hashAfterFirst = Context.GetStateHash();

            Dispatch(command);

            Assert.AreEqual(
                sequenceAfterFirst, Context.SequenceNumber,
                "Re-dispatching an already-resolved command must not advance SequenceNumber a second time.");
            Assert.AreEqual(
                hashAfterFirst, Context.GetStateHash(),
                "Re-dispatching an already-resolved command must not mutate state a second time.");
        }
    }
}
