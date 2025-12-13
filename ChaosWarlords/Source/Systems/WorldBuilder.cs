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
            // We allow texture to be null for unit testing logic
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

            // 4. Setup Map
            MapManager mapManager;
            if (File.Exists(_mapDataPath))
            {
                var mapData = MapFactory.LoadFromFile(_mapDataPath, texture);
                mapManager = new MapManager(mapData.Item1, mapData.Item2);
            }
            else
            {
                // Fallback / Test Map
                var nodes = MapFactory.CreateTestMap(texture);
                var sites = new List<Site>();
                mapManager = new MapManager(nodes, sites);
            }
            mapManager.PixelTexture = texture;

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