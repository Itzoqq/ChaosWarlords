using System.Collections.Generic;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;

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
        /// Universal pile for all devoured cards (removed from game).
        /// </summary>
        public List<Card> VoidPile { get; private set; } = new List<Card>();

        // 2. Convenience Properties (Shortcuts)
        public Player ActivePlayer => TurnManager.ActivePlayer;

        // 3. Match-Specific Settings (that don't belong in a generic manager)
        public int TargetVictoryPoints { get; set; } = 40;
        public bool IsGamePaused { get; set; } = false;

        // New Phase Tracking
        public MatchPhase CurrentPhase { get; set; } = MatchPhase.Setup;

        public MatchContext(
            ITurnManager turn,
            IMapManager map,
            IMarketManager market,
            IActionSystem action,
            ICardDatabase cardDb)
        {
            TurnManager = turn;
            MapManager = map;
            MarketManager = market;
            ActionSystem = action;
            CardDatabase = cardDb;
        }
    }
}