using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;
using System.IO;
using System;

namespace ChaosWarlords.Source.Systems
{
    public class WorldData
    {
        public Player Player { get; set; }
        public MarketManager MarketManager { get; set; }
        public MapManager MapManager { get; set; }
        public ActionSystem ActionSystem { get; set; }
    }

    public class WorldBuilder
    {
        private readonly string _cardDataPath;
        private readonly string _mapDataPath;

        public WorldBuilder(string cardDataPath, string mapDataPath)
        {
            _cardDataPath = cardDataPath;
            _mapDataPath = mapDataPath;
        }

        public WorldData Build(Texture2D texture)
        {
            // 1. Initialize Databases
            // NOTE: Cards still use the texture for now (we will refactor Cards in Step 2)
            if (File.Exists(_cardDataPath))
            {
                CardDatabase.Load(_cardDataPath, texture);
            }

            // 2. Setup Market
            var marketManager = new MarketManager();
            marketManager.InitializeDeck(CardDatabase.GetAllMarketCards());

            // 3. Setup Player
            var player = new Player(PlayerColor.Red);
            // Starter Deck
            for (int i = 0; i < 3; i++) player.Deck.Add(CardFactory.CreateSoldier(texture));
            for (int i = 0; i < 7; i++) player.Deck.Add(CardFactory.CreateNoble(texture));
            player.DrawCards(5);

            // 4. Setup Map (RENDERING EXTRACTED)
            MapManager mapManager;

            // We hold the nodes/sites data here to pass to the manager
            (List<MapNode>, List<Site>) mapData;

            if (File.Exists(_mapDataPath))
            {
                // Updated: No longer takes 'texture'
                mapData = MapFactory.LoadFromFile(_mapDataPath);
            }
            else
            {
                // Fallback / Test Map
                // Updated: No longer takes 'texture' and returns a tuple like LoadFromFile
                mapData = MapFactory.CreateTestMap();
            }

            mapManager = new MapManager(mapData.Item1, mapData.Item2);
            // Removed: mapManager.PixelTexture = texture; (The renderer handles this now)

            // 5. Setup Action System
            var actionSystem = new ActionSystem(player, mapManager);

            // 6. Apply Specific Scenario Rules (e.g. City of Gold)
            if (mapManager.Sites != null)
            {
                foreach (var site in mapManager.Sites)
                {
                    if (site.Name.ToLower().Contains("city of gold"))
                        site.Spies.Add(PlayerColor.Blue);
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