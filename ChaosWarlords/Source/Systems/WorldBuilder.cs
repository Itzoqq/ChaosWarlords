using Microsoft.Xna.Framework.Content;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChaosWarlords.Source.Systems
{
    // UPDATED WorldData structure
    public class WorldData
    {
        // Removed: public Player Player { get; set; }
        public TurnManager TurnManager { get; set; } // NEW: Replaces the single Player object
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

        // MODIFIED Build Method
        public WorldData Build()
        {
            // 1. Initialize Databases (Handled)

            // 2. Setup Market
            var marketManager = new MarketManager();
            marketManager.InitializeDeck(_cardDatabase.GetAllMarketCards());

            // 3. Setup Players (Now two players)
            var players = new List<Player>();

            // Player 1 (Red)
            var playerRed = new Player(PlayerColor.Red);
            for (int i = 0; i < 3; i++) playerRed.Deck.Add(CardFactory.CreateSoldier());
            for (int i = 0; i < 7; i++) playerRed.Deck.Add(CardFactory.CreateNoble());
            players.Add(playerRed);

            // Player 2 (Blue)
            var playerBlue = new Player(PlayerColor.Blue);
            for (int i = 0; i < 3; i++) playerBlue.Deck.Add(CardFactory.CreateSoldier());
            for (int i = 0; i < 7; i++) playerBlue.Deck.Add(CardFactory.CreateNoble());
            players.Add(playerBlue);

            // 4. Setup Turn Manager
            var turnManager = new TurnManager(players);
            Player activePlayer = turnManager.ActivePlayer; // This will be Player Red

            // 5. Setup Map
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

            // 6. Setup Action System
            // ActionSystem is now initialized with the CURRENT active player (Red)
            var actionSystem = new ActionSystem(activePlayer, mapManager);

            // 7. Scenario Rules (Updated to reflect multiple players)
            if (mapManager.Sites != null)
            {
                foreach (var site in mapManager.Sites)
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

            // 8. Return WorldData
            return new WorldData
            {
                TurnManager = turnManager, // Return the new TurnManager
                MarketManager = marketManager,
                MapManager = mapManager,
                ActionSystem = actionSystem
            };
        }
    }
}