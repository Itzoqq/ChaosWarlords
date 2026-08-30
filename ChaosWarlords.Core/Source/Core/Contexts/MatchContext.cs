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

        /// <summary>
        /// UI event mediator for requesting player choices (e.g., optional effects).
        /// Nullable for test scenarios where UI interactions aren't needed.
        /// </summary>
        public IUIEventMediator? UIEventMediator { get; private set; }
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
            IUIEventMediator? uiMediator,
            IGameLogger logger,
            int? seed = null)
        {
            TurnManager = turn ?? throw new ArgumentNullException(nameof(turn));
            MapManager = map ?? throw new ArgumentNullException(nameof(map));
            MarketManager = market ?? throw new ArgumentNullException(nameof(market));
            ActionSystem = action ?? throw new ArgumentNullException(nameof(action));
            CardDatabase = cardDb ?? throw new ArgumentNullException(nameof(cardDb));
            PlayerStateManager = playerState ?? throw new ArgumentNullException(nameof(playerState));
            UIEventMediator = uiMediator; // Nullable for tests
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
        /// </summary>
        public string GetStateHash()
        {
            long hash = 17;
            hash = hash * 31 + SequenceNumber;
            hash = hash * 31 + CurrentTurnNumber;
            hash = hash * 31 + (int)CurrentPhase;
            hash = hash * 31 + (TurnManager?.ActivePlayer?.Color.GetHashCode() ?? 0);
            hash = hash * 31 + Seed;

            hash = AppendMapHashContributions(hash);
            hash = AppendPlayerHashContributions(hash);
            hash = AppendMarketHashContributions(hash);

            return hash.ToString("X", System.Globalization.CultureInfo.InvariantCulture);
        }

        private long AppendMapHashContributions(long hash)
        {
            if (MapManager != null)
            {
                foreach (var node in MapManager.Nodes.OrderBy(n => n.Id))
                {
                    hash = hash * 31 + node.Id;
                    hash = hash * 31 + node.Occupant.GetHashCode();
                }
            }
            return hash;
        }

        private long AppendPlayerHashContributions(long hash)
        {
            if (TurnManager != null && TurnManager.Players != null)
            {
                foreach (var player in TurnManager.Players.OrderBy(p => p.Color))
                {
                    hash = hash * 31 + player.Power;
                    hash = hash * 31 + player.Influence;
                    hash = hash * 31 + player.VictoryPoints;
                    hash = hash * 31 + player.TroopsInBarracks;
                    hash = hash * 31 + player.Hand.Count;
                    hash = hash * 31 + player.InnerCircle.Count;
                }
            }
            return hash; 
        }

        private long AppendMarketHashContributions(long hash)
        {
            if (MarketManager != null)
            {
                hash = hash * 31 + MarketManager.MarketRow.Count;
                foreach (var card in MarketManager.MarketRow.OrderBy(c => c.Id))
                {
                    hash = hash * 31 + card.Id.GetHashCode();
                }
            }
            return hash;
        }
    }
}
