using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Systems
{
    [ExcludeFromCodeCoverage]
    public class WorldData
    {
        public Player Player { get; set; }
        public MarketManager MarketManager { get; set; }
        public MapManager MapManager { get; set; }
        public ActionSystem ActionSystem { get; set; }
    }

    public class WorldBuilder
    {
        private readonly ICardDatabase _cardDatabase; // Dependency
        private readonly string _mapDataPath;

        // Constructor now takes the DATABASE, not the file path for cards
        public WorldBuilder(ICardDatabase cardDatabase, string mapDataPath)
        {
            _cardDatabase = cardDatabase;
            _mapDataPath = mapDataPath;
        }

        public WorldData Build()
        {
            // 1. Initialize Databases
            // (CardDatabase is already loaded externally and passed in via constructor)

            // 2. Setup Market
            var marketManager = new MarketManager();
            // Use the injected DB instance
            marketManager.InitializeDeck(_cardDatabase.GetAllMarketCards());

            // 3. Setup Player
            var player = new Player(PlayerColor.Red);
            // Starter Deck
            for (int i = 0; i < 3; i++) player.Deck.Add(CardFactory.CreateSoldier());
            for (int i = 0; i < 7; i++) player.Deck.Add(CardFactory.CreateNoble());
            player.DrawCards(5);

            // 4. Setup Map
            (List<MapNode>, List<Site>) mapData;
            try
            {
                using (var stream = TitleContainer.OpenStream(Path.Combine("Content", _mapDataPath)))
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
            var actionSystem = new ActionSystem(player, mapManager);

            // 6. Scenario Rules
            if (mapManager.Sites != null)
            {
                foreach (var site in mapManager.Sites)
                {
                    if (site.Name.ToLower().Contains("city of gold"))
                    {
                        site.Spies.Add(PlayerColor.Blue);
                        site.Spies.Add(PlayerColor.Neutral);
                    }
                }
            }

            return new WorldData
            {
                Player = player,
                MarketManager = marketManager,
                MapManager = mapManager,
                ActionSystem = actionSystem
            };
        }
    }
}