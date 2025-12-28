using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Systems;
using System;
using System.Collections.Generic;
using System.IO;

namespace ChaosWarlords.Source.Factories
{
    // WorldData structure
    public class WorldData
    {
        public PlayerStateManager PlayerStateManager { get; set; }
        public TurnManager TurnManager { get; set; }
        public MarketManager MarketManager { get; set; }
        public MapManager MapManager { get; set; }
        public ActionSystem ActionSystem { get; set; }
    }

    public class MatchFactory
    {
        private readonly ICardDatabase _cardDatabase;

        public MatchFactory(ICardDatabase cardDatabase)
        {
            _cardDatabase = cardDatabase;
        }

        /// <summary>
        /// Builds a new match with all necessary components.
        /// </summary>
        /// <param name="seed">Optional seed for deterministic gameplay. If null, uses Environment.TickCount.</param>
        /// <returns>WorldData containing all initialized managers and systems.</returns>
        public WorldData Build(int? seed = null)
        {
            // 0. Initialize seeded RNG
            int matchSeed = seed ?? Environment.TickCount;
            var random = new SeededGameRandom(matchSeed);
            GameLogger.Log($"Match created with seed: {matchSeed}", LogChannel.Info);

            // 0.5 Setup Player State Manager
            var playerStateManager = new PlayerStateManager();

            // 1. Setup Market
            var marketManager = new MarketManager(_cardDatabase, random);

            // 2. Setup Players (Now two players)
            var players = new List<Player>();

            // Player 1 (Red)
            var playerRed = new Player(PlayerColor.Red, displayName: "Player Red");
            for (int i = 0; i < 3; i++) playerRed.DeckManager.AddToTop(CardFactory.CreateSoldier());
            for (int i = 0; i < 7; i++) playerRed.DeckManager.AddToTop(CardFactory.CreateNoble());
            playerRed.DeckManager.Shuffle(random);
            players.Add(playerRed);

            // Player 2 (Blue)
            var playerBlue = new Player(PlayerColor.Blue, displayName: "Player Blue");
            for (int i = 0; i < 3; i++) playerBlue.DeckManager.AddToTop(CardFactory.CreateSoldier());
            for (int i = 0; i < 7; i++) playerBlue.DeckManager.AddToTop(CardFactory.CreateNoble());
            playerBlue.DeckManager.Shuffle(random);
            players.Add(playerBlue);

            // 3. Setup Turn Manager (with seeded RNG for player order)
            var turnManager = new TurnManager(players, random);

            // Note: turnManager.ActivePlayer is now valid immediately after construction

            // 4. Setup Map (Delegated to MapFactory)
            // This decouples the "How" of map generation from the "How" of match setup.
            (List<MapNode> nodes, List<Site> sites, List<Route> routes) = MapFactory.CreateScenarioMap();

            // 5. Setup Action System (Moved up or just kept here)
            // But we need mapManager first
            
            var mapManager = new MapManager(nodes, sites, playerStateManager);

            // 5. Setup Action System
            // REFACTOR: ActionSystem is now initialized with the TurnManager, not the Player
            var actionSystem = new ActionSystem(turnManager, mapManager);
            actionSystem.SetPlayerStateManager(playerStateManager);

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
                PlayerStateManager = playerStateManager,
                TurnManager = turnManager,
                MarketManager = marketManager,
                MapManager = mapManager,
                ActionSystem = actionSystem
            };
        }
    }
}



