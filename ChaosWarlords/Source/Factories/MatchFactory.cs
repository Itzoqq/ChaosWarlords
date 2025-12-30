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
        public int Seed { get; set; }
        public required IGameRandom GameRandom { get; set; }
    }

    public class MatchFactory
    {
        private readonly ICardDatabase _cardDatabase;
        private readonly IGameLogger _logger;

        public MatchFactory(ICardDatabase cardDatabase, IGameLogger logger)
        {
            _cardDatabase = cardDatabase;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Builds a new match with all necessary components.
        /// </summary>
        /// <param name="seed">Optional seed for deterministic gameplay. If null, uses Environment.TickCount.</param>
        /// <returns>WorldData containing all initialized managers and systems.</returns>
        public WorldData Build(IReplayManager replayManager, int? seed = null)
        {
            // 0. Initialize seeded RNG
            int matchSeed = seed ?? Environment.TickCount;
            var random = new SeededGameRandom(matchSeed, _logger);
            _logger.Log($"Match created with seed: {matchSeed}", LogChannel.Info);

            var playerStateManager = new PlayerStateManager(_logger);
            
            _logger.Log($"[RNG] Pre-MarketManager: {random.CallCount}", LogChannel.Debug);
            var marketManager = new MarketManager(_cardDatabase, random);
            _logger.Log($"[RNG] Post-MarketManager Checksum: {random.CallCount}", LogChannel.Info);

            _logger.Log($"[RNG] Pre-CreatePlayers: {random.CallCount}", LogChannel.Debug);
            var players = CreatePlayers(random, _logger);
            _logger.Log($"[RNG] Post-Players Checksum: {random.CallCount}", LogChannel.Info);
            
            var turnManager = new TurnManager(players, random, _logger);

            var mapManager = SetupMap(playerStateManager, _logger);
            var actionSystem = SetupActionSystem(turnManager, mapManager, playerStateManager, _logger);

            ApplyScenarioRules(mapManager);

            return new WorldData
            {
                PlayerStateManager = playerStateManager,
                TurnManager = turnManager,
                MarketManager = marketManager,
                MapManager = mapManager,
                ActionSystem = actionSystem,
                Seed = matchSeed,
                GameRandom = random
            };
        }

        private static List<Player> CreatePlayers(IGameRandom random, IGameLogger logger)
        {
            var players = new List<Player>();

            // Player 1 (Red)
            var playerRed = CreateDefaultPlayer(PlayerColor.Red, "Player Red", 0, random, logger);
            players.Add(playerRed);

            // Player 2 (Blue)
            var playerBlue = CreateDefaultPlayer(PlayerColor.Blue, "Player Blue", 1, random, logger);
            players.Add(playerBlue);

            return players;
        }

        private static Player CreateDefaultPlayer(PlayerColor color, string name, int seatIndex, IGameRandom random, IGameLogger logger)
        {
            logger.Log($"[RNG] Creating Player {color}: {(random as SeededGameRandom)?.CallCount ?? -1}", LogChannel.Debug);
            // Deterministic ID generation for Replay compatibility
            // We use a simple hash of the name/color to Create a GUID
            byte[] fullHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name));
            byte[] hash = new byte[16];
            Array.Copy(fullHash, hash, 16);
            var deterministicId = new Guid(hash);

            var player = new Player(color, deterministicId, displayName: name);
            player.SeatIndex = seatIndex;
            logger.Log($"Created {name} with SeatIndex: {seatIndex}", LogChannel.Info);
            for (int i = 0; i < 3; i++) player.DeckManager.AddToTop(CardFactory.CreateSoldier(random));
            for (int i = 0; i < 7; i++) player.DeckManager.AddToTop(CardFactory.CreateNoble(random));
            logger.Log($"[RNG] Shuffling Deck for {color}: {(random as SeededGameRandom)?.CallCount ?? -1}", LogChannel.Debug);
            player.DeckManager.Shuffle(random);
            logger.Log($"[RNG] Post-Shuffle for {color}: {(random as SeededGameRandom)?.CallCount ?? -1}", LogChannel.Debug);
            return player;
        }

        private static MapManager SetupMap(IPlayerStateManager playerStateManager, IGameLogger logger)
        {
            (List<MapNode> nodes, List<Site> sites, _) = MapFactory.CreateScenarioMap(logger);
            return new MapManager(nodes, sites, logger, playerStateManager);
        }

        private static ActionSystem SetupActionSystem(ITurnManager turnManager, IMapManager mapManager, IPlayerStateManager playerStateManager, IGameLogger logger)
        {
            var actionSystem = new ActionSystem(turnManager, mapManager, logger);
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



