using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using System;
using System.Collections.Generic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;

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
        public IActionSystem ActionSystem { get; private set; }
        public ICardDatabase CardDatabase { get; private set; }

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
        /// The seed used to initialize the random number generator.
        /// Can be used to reproduce the exact same match.
        /// </summary>
        public int Seed { get; private set; }

        /// <summary>
        /// Universal pile for all devoured cards (removed from game).
        /// </summary>
        public List<Card> VoidPile { get; private set; } = new List<Card>();

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
            TurnManager = turn ?? throw new ArgumentNullException(nameof(turn));
            MapManager = map ?? throw new ArgumentNullException(nameof(map));
            MarketManager = market ?? throw new ArgumentNullException(nameof(market));
            ActionSystem = action ?? throw new ArgumentNullException(nameof(action));
            CardDatabase = cardDb ?? throw new ArgumentNullException(nameof(cardDb));
            PlayerStateManager = playerState ?? throw new ArgumentNullException(nameof(playerState));

            // Initialize seeded RNG
            Seed = seed ?? Environment.TickCount;
            Random = new SeededGameRandom(Seed, logger);
        }

        public void RecordAction(string actionType, string summary)
        {
            // Null check for TurnManager and CurrentTurnContext to prevent crashes in partially mocked tests
            TurnManager?.CurrentTurnContext?.RecordAction(actionType, summary);
        }
    }
}


