using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;
using System;
using System.Collections.Generic;

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

        private List<Player> CreatePlayers(IGameRandom random)
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

        private Player CreateDefaultPlayer(PlayerColor color, string name, IGameRandom random)
        {
            var player = new Player(color, displayName: name);
            for (int i = 0; i < 3; i++) player.DeckManager.AddToTop(CardFactory.CreateSoldier());
            for (int i = 0; i < 7; i++) player.DeckManager.AddToTop(CardFactory.CreateNoble());
            player.DeckManager.Shuffle(random);
            return player;
        }

        private MapManager SetupMap(IPlayerStateManager playerStateManager)
        {
            (List<MapNode> nodes, List<Site> sites, _) = MapFactory.CreateScenarioMap();
            return new MapManager(nodes, sites, playerStateManager);
        }

        private ActionSystem SetupActionSystem(ITurnManager turnManager, IMapManager mapManager, IPlayerStateManager playerStateManager)
        {
            var actionSystem = new ActionSystem(turnManager, mapManager);
            actionSystem.SetPlayerStateManager(playerStateManager);
            return actionSystem;
        }

        private void ApplyScenarioRules(MapManager mapManager)
        {
            if (mapManager.SitesInternal == null) return;

            foreach (var site in mapManager.SitesInternal)
            {
                if (site.Name.ToLower().Contains("city of gold"))
                {
                    site.Spies.Add(PlayerColor.Blue);
                    site.Spies.Add(PlayerColor.Red);
                    site.Spies.Add(PlayerColor.Neutral);
                }
            }
        }
    }
}



