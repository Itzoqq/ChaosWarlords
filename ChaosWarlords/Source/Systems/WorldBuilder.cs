using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;
using System.IO;
using System;
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
        private readonly string _cardDataPath;
        private readonly string _mapDataPath;

        public WorldBuilder(string cardDataPath, string mapDataPath)
        {
            _cardDataPath = cardDataPath;
            _mapDataPath = mapDataPath;
        }

        // Texture is NOT used here anymore! 
        // We can remove the parameter or just ignore it. I'll ignore it to keep signature similar if you want, 
        // but cleaner to remove. I will remove it.
        public WorldData Build()
        {
            // 1. Initialize Databases (No Textures!)
            if (File.Exists(_cardDataPath))
            {
                CardDatabase.Load(_cardDataPath);
            }

            // 2. Setup Market
            var marketManager = new MarketManager();
            marketManager.InitializeDeck(CardDatabase.GetAllMarketCards());

            // 3. Setup Player (No Textures!)
            var player = new Player(PlayerColor.Red);
            // Starter Deck
            for (int i = 0; i < 3; i++) player.Deck.Add(CardFactory.CreateSoldier());
            for (int i = 0; i < 7; i++) player.Deck.Add(CardFactory.CreateNoble());
            player.DrawCards(5);

            // 4. Setup Map
            MapManager mapManager;
            (List<MapNode>, List<Site>) mapData;

            if (File.Exists(_mapDataPath)) mapData = MapFactory.LoadFromFile(_mapDataPath);
            else mapData = MapFactory.CreateTestMap();

            mapManager = new MapManager(mapData.Item1, mapData.Item2);

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