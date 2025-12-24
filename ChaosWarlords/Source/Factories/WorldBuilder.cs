using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities;
using System.Collections.Generic;
using System.IO;

namespace ChaosWarlords.Source.Systems
{
    // WorldData structure
    public class WorldData
    {
        public TurnManager TurnManager { get; set; }
        public MarketManager MarketManager { get; set; }
        public MapManager MapManager { get; set; }
        public ActionSystem ActionSystem { get; set; }
    }

    public class WorldBuilder
    {
        private readonly ICardDatabase _cardDatabase;
        private readonly string _mapDataPath;

        public WorldBuilder(ICardDatabase cardDatabase, string mapDataPath)
        {
            _cardDatabase = cardDatabase;
            _mapDataPath = mapDataPath;
        }

        // Build Method
        public WorldData Build()
        {
            // 1. Setup Market
            var marketManager = new MarketManager(_cardDatabase);

            // 2. Setup Players (Now two players)
            var players = new List<Player>();

            // Player 1 (Red)
            var playerRed = new Player(PlayerColor.Red);
            for (int i = 0; i < 3; i++) playerRed.DeckManager.AddToTop(CardFactory.CreateSoldier());
            for (int i = 0; i < 7; i++) playerRed.DeckManager.AddToTop(CardFactory.CreateNoble());
            players.Add(playerRed);

            // Player 2 (Blue)
            var playerBlue = new Player(PlayerColor.Blue);
            for (int i = 0; i < 3; i++) playerBlue.DeckManager.AddToTop(CardFactory.CreateSoldier());
            for (int i = 0; i < 7; i++) playerBlue.DeckManager.AddToTop(CardFactory.CreateNoble());
            players.Add(playerBlue);

            // 3. Setup Turn Manager
            var turnManager = new TurnManager(players);

            // Note: turnManager.ActivePlayer is now valid immediately after construction

            // 4. Setup Map (Load from JSON)
            (List<MapNode>, List<Site>) mapData;
            try
            {
                using (var stream = Microsoft.Xna.Framework.TitleContainer.OpenStream(Path.Combine("Content", _mapDataPath)))
                {
                    mapData = MapFactory.LoadFromStream(stream);
                }
            }
            catch
            {
                mapData = MapFactory.CreateTestMap();
            }

            var mapManager = new MapManager(mapData.Item1, mapData.Item2);

            // 5. Setup Action System
            // REFACTOR: ActionSystem is now initialized with the TurnManager, not the Player
            var actionSystem = new ActionSystem(turnManager, mapManager);

            // 6. Scenario Rules (Updated to reflect multiple players)
            if (mapManager.SitesInternal != null)
            {
                foreach (var site in mapManager.SitesInternal)
                {
                    if (site.Name.ToLower().Contains("city of gold"))
                    {
                        // Assigning spies to player Blue and Neutral still valid
                        site.Spies.Add(PlayerColor.Blue);
                        site.Spies.Add(PlayerColor.Red);
                        site.Spies.Add(PlayerColor.Neutral);
                    }
                }
            }

            // 7. Return WorldData
            return new WorldData
            {
                TurnManager = turnManager,
                MarketManager = marketManager,
                MapManager = mapManager,
                ActionSystem = actionSystem
            };
        }
    }
}