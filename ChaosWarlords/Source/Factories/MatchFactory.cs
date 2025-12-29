using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChaosWarlords.Source.Factories
{
    // WorldData structure
    public class WorldData
    {
        public required PlayerStateManager PlayerStateManager { get; set; }
        public required TurnManager TurnManager { get; set; }
        public required MarketManager MarketManager { get; set; }
        public required MapManager MapManager { get; set; }
        public required ActionSystem ActionSystem { get; set; }
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

            var playerStateManager = new PlayerStateManager();
            var marketManager = new MarketManager(_cardDatabase, random);
            var players = CreatePlayers(random);
            var turnManager = new TurnManager(players, random);

            var mapManager = SetupMap(playerStateManager);
            var actionSystem = SetupActionSystem(turnManager, mapManager, playerStateManager);

            ApplyScenarioRules(mapManager);

            return new WorldData
            {
                PlayerStateManager = playerStateManager,
                TurnManager = turnManager,
                MarketManager = marketManager,
                MapManager = mapManager,
                ActionSystem = actionSystem
            };
        }

        private static List<Player> CreatePlayers(IGameRandom random)
        {
            var players = new List<Player>();

            // Player 1 (Red)
            var playerRed = CreateDefaultPlayer(PlayerColor.Red, "Player Red", random);
            players.Add(playerRed);

            // Player 2 (Blue)
            var playerBlue = CreateDefaultPlayer(PlayerColor.Blue, "Player Blue", random);
            players.Add(playerBlue);

            return players;
        }

        private static Player CreateDefaultPlayer(PlayerColor color, string name, IGameRandom random)
        {
            var player = new Player(color, displayName: name);
            for (int i = 0; i < 3; i++) player.DeckManager.AddToTop(CardFactory.CreateSoldier());
            for (int i = 0; i < 7; i++) player.DeckManager.AddToTop(CardFactory.CreateNoble());
            player.DeckManager.Shuffle(random);
            return player;
        }

        private static MapManager SetupMap(IPlayerStateManager playerStateManager)
        {
            (List<MapNode> nodes, List<Site> sites, _) = MapFactory.CreateScenarioMap();
            return new MapManager(nodes, sites, playerStateManager);
        }

        private static ActionSystem SetupActionSystem(ITurnManager turnManager, IMapManager mapManager, IPlayerStateManager playerStateManager)
        {
            var actionSystem = new ActionSystem(turnManager, mapManager);
            actionSystem.SetPlayerStateManager(playerStateManager);
            return actionSystem;
        }

        private static void ApplyScenarioRules(MapManager mapManager)
        {
            if (mapManager.SitesInternal is null) return;

            foreach (var site in mapManager.SitesInternal)
            {
                if (site.Name.Contains("city of gold", StringComparison.OrdinalIgnoreCase))
                {
                    site.Spies.Add(PlayerColor.Blue);
                    site.Spies.Add(PlayerColor.Red);
                    site.Spies.Add(PlayerColor.Neutral);
                }
            }
        }
    }
}



