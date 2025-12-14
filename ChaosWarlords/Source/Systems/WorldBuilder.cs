using Microsoft.Xna.Framework; // <--- FIXED: Added this for TitleContainer
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

        public WorldData Build()
        {
            // 1. Initialize Databases (Using TitleContainer)
            try
            {
                // FIXED: Changed _cardFile to _cardDataPath
                using (var stream = TitleContainer.OpenStream(Path.Combine("Content", _cardDataPath)))
                {
                    CardDatabase.Load(stream);
                }
            }
            catch (FileNotFoundException)
            {
                GameLogger.Log("Card data file not found. Ensure 'cards.json' is in the Content folder.", LogChannel.Error);
            }

            // 2. Setup Market
            var marketManager = new MarketManager();
            marketManager.InitializeDeck(CardDatabase.GetAllMarketCards());

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
                // FIXED: Changed _mapFile to _mapDataPath
                using (var stream = TitleContainer.OpenStream(Path.Combine("Content", _mapDataPath)))
                {
                    mapData = MapFactory.LoadFromStream(stream);
                }
            }
            catch
            {
                mapData = MapFactory.CreateTestMap();
            }

            // --- FIXED: This line was missing! ---
            // We loaded the raw data (mapData), now we must create the Manager.
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