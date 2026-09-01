using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;
using System.Linq; // Required for OrderBy

namespace ChaosWarlords.Source.Contexts
{
    public enum MatchPhase
    {
        Setup,
        Playing
    }

    /// <summary>
    /// Holds all the dependencies required to run a Match.
    /// Passes this single object around instead of 6 individual managers.
    /// THIS CLASS IS PURE DATA HOLDER - NO LOGIC HERE!
    /// THIS CLASS HAS IMMUTABLE SYSTEMS - set at construction time only.
    /// THIS CLASS HAS SCOPED LIFETIME - exists only for the duration of a Match.
    /// </summary>
    public class MatchContext
    {
        // 1. The Core Systems
        public ITurnManager TurnManager { get; private set; }
        public IMapManager MapManager { get; private set; }
        public IMarketManager MarketManager { get; private set; }
        public IMatchManager MatchManager { get; set; } = null!; // Late init allow
        public IActionSystem ActionSystem { get; private set; }
        public ICardDatabase CardDatabase { get; private set; }
        public CardRuleEngine CardRuleEngine { get; private set; }

        /// <summary>
        /// Deterministic random number generator for this match.
        /// All random events must use this to ensure reproducible gameplay.
        /// </summary>
        public IGameRandom Random { get; private set; }

        /// <summary>
        /// Centralized state manager for all player mutations.
        /// </summary>
        public IPlayerStateManager PlayerStateManager { get; private set; }

        public IGameLogger Logger { get; private set; }

        /// <summary>
        /// The seed used to initialize the random number generator.
        /// Can be used to reproduce the exact same match.
        /// </summary>
        public int Seed { get; private set; }

        /// <summary>
        /// Universal pile for all devoured cards (removed from game).
        /// </summary>
        public List<Card> VoidPile { get; private set; } = new List<Card>();

        /// <summary>
        /// Cards marked for destruction at the end of the turn (Self-Devour).
        /// </summary>
        public List<Card> CardsMarkedForTurnEndDevour { get; private set; } = new List<Card>();

        /// <summary>
        /// One entry per card played this turn that forces "each opponent discards a card"
        /// at end of turn (e.g. Neogi) - stacks, so 2 such cards played the same turn means
        /// every opponent owes 2 discards. Consumed and cleared by MatchManager.EndTurn's
        /// opponent-discard phase, mirroring CardsMarkedForTurnEndDevour's shape.
        /// </summary>
        public List<Card> PendingOpponentDiscardTriggers { get; private set; } = new List<Card>();

        // 2. Convenience Properties (Shortcuts)
        public Player ActivePlayer => TurnManager.ActivePlayer;

        // 3. Match-Specific Settings (that don't belong in a generic manager)
        public int TargetVictoryPoints { get; set; } = GameConstants.TargetVictoryPoints;
        public bool IsGamePaused { get; set; }

        /// <summary>
        /// Tracks the current turn number for logging and replay purposes.
        /// </summary>
        public int CurrentTurnNumber { get; set; }

        // Phase Tracking
        public MatchPhase CurrentPhase { get; set; } = MatchPhase.Setup;

        /// <summary>
        /// Monotonically increasing sequence number for every executed command.
        /// Critical for multiplayer synchronization and state verification.
        /// </summary>
        public long SequenceNumber { get; set; }

        public MatchContext(
            ITurnManager turn,
            IMapManager map,
            IMarketManager market,
            IActionSystem action,
            ICardDatabase cardDb,
            IPlayerStateManager playerState,
            IGameLogger logger,
            int? seed = null)
        {
            // ArgumentNullException.ThrowIfNull keeps this constructor's own body a flat,
            // branch-free sequence - the null check + throw both live inside the BCL method
            // instead of being reimplemented 7 times as "?? throw" here.
            ArgumentNullException.ThrowIfNull(turn);
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(market);
            ArgumentNullException.ThrowIfNull(action);
            ArgumentNullException.ThrowIfNull(cardDb);
            ArgumentNullException.ThrowIfNull(playerState);
            ArgumentNullException.ThrowIfNull(logger);

            TurnManager = turn;
            MapManager = map;
            MarketManager = market;
            ActionSystem = action;
            CardDatabase = cardDb;
            PlayerStateManager = playerState;
            Logger = logger;

            // Initialize seeded RNG
            Seed = seed ?? Environment.TickCount;
            Random = new SeededGameRandom(Seed, logger);

            // Initialize Rules Engine
            CardRuleEngine = new CardRuleEngine(this, logger);
        }

        public void RecordAction(string actionType, string summary)
        {
            // Null check for TurnManager and CurrentTurnContext to prevent crashes in partially mocked tests
            TurnManager?.CurrentTurnContext?.RecordAction(actionType, summary);
        }

        /// <summary>
        /// Generates a deterministic hash of the current game state.
        /// Used for detecting desyncs in multiplayer.
        ///
        /// Built entirely from StateHasher.Mix (FNV-1a), not the *31/GetHashCode() pattern
        /// this used to use directly. That mattered for two concrete reasons, not just
        /// hygiene: (1) string.GetHashCode() is randomized per-process by default in modern
        /// .NET (a DoS mitigation) - the old `card.Id.GetHashCode()` in
        /// AppendMarketHashContributions meant two processes with an IDENTICAL market row
        /// (e.g. a server and a client) would still get different hashes, defeating desync
        /// detection the moment the market had any cards in it; (2) Enum.GetHashCode() (used
        /// for CurrentPhase/PlayerColor/node.Occupant) isn't part of the enum's documented
        /// contract, unlike StateHasher's explicit Convert.ToInt32 handling. This is also now
        /// the ONLY state-hash implementation in the codebase - see GameStateDto.StateHash,
        /// which used to compute an independent, much narrower checksum via
        /// StateHasher.ComputeHash directly (Seed/TurnNumber/Phase/SequenceNumber only - no
        /// map/players/market) and has been consolidated to just carry this method's result
        /// instead. See planning.txt.
        /// </summary>
        public string GetStateHash()
        {
            int hash = StateHasher.Init();
            hash = StateHasher.Mix(hash, SequenceNumber);
            hash = StateHasher.Mix(hash, CurrentTurnNumber);
            hash = StateHasher.Mix(hash, CurrentPhase);
            hash = StateHasher.Mix(hash, TurnManager?.ActivePlayer?.Color);
            hash = StateHasher.Mix(hash, Seed);

            hash = AppendMapHashContributions(hash);
            hash = AppendPlayerHashContributions(hash);
            hash = AppendMarketHashContributions(hash);

            return hash.ToString("X", System.Globalization.CultureInfo.InvariantCulture);
        }

        private int AppendMapHashContributions(int hash)
        {
            if (MapManager != null)
            {
                foreach (var node in MapManager.Nodes.OrderBy(n => n.Id))
                {
                    hash = StateHasher.Mix(hash, node.Id);
                    hash = StateHasher.Mix(hash, node.Occupant);
                }
            }
            return hash;
        }

        private int AppendPlayerHashContributions(int hash)
        {
            if (TurnManager != null && TurnManager.Players != null)
            {
                foreach (var player in TurnManager.Players.OrderBy(p => p.Color))
                {
                    hash = StateHasher.Mix(hash, player.Power);
                    hash = StateHasher.Mix(hash, player.Influence);
                    hash = StateHasher.Mix(hash, player.VictoryPoints);
                    hash = StateHasher.Mix(hash, player.TroopsInBarracks);
                    hash = StateHasher.Mix(hash, player.Hand.Count);
                    hash = StateHasher.Mix(hash, player.InnerCircle.Count);
                }
            }
            return hash;
        }

        private int AppendMarketHashContributions(int hash)
        {
            if (MarketManager != null)
            {
                hash = StateHasher.Mix(hash, MarketManager.MarketRow.Count);
                foreach (var card in MarketManager.MarketRow.OrderBy(c => c.Id))
                {
                    hash = StateHasher.Mix(hash, card.Id);
                }
            }
            return hash;
        }
    }
}
