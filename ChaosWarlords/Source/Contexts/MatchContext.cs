using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Contexts
{
    /// <summary>
    /// Holds all the dependencies required to run a Match.
    /// Passes this single object around instead of 6 individual managers.
    /// </summary>
    public class MatchContext
    {
        // 1. The Core Systems
        public ITurnManager TurnManager { get; private set; }
        public IMapManager MapManager { get; private set; }
        public IMarketManager MarketManager { get; private set; }
        public IActionSystem ActionSystem { get; private set; }
        public ICardDatabase CardDatabase { get; private set; }

        // 2. Convenience Properties (Shortcuts)
        public Player ActivePlayer => TurnManager.ActivePlayer;

        // 3. Match-Specific Settings (that don't belong in a generic manager)
        public int TargetVictoryPoints { get; set; } = 40;
        public bool IsGamePaused { get; set; } = false;

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