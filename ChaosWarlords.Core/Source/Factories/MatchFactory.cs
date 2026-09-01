using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;

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

        // Rulebook p.4: 2-4 players. Defaults to today's exact Red/Blue-only behavior so every
        // existing caller (Game1.cs, tests) is unaffected - pass playerColors to Build() for a
        // 3-4 player match. NOTE: the shipped test map (MapFactory.CreateScenarioMap) is a
        // small scenario map, not the full multi-section Underdark board the rulebook's 3-4
        // player setup expects (see planning.txt) - this fixes MatchFactory/SiteControlSystem's
        // ability to correctly BUILD and SCORE a 3-4 player match, not the separate, larger
        // "expand the map" effort.
        private static readonly IReadOnlyList<PlayerColor> DefaultPlayerColors = new[] { PlayerColor.Red, PlayerColor.Blue };

        /// <summary>
        /// Builds a new match with all necessary components.
        /// </summary>
        /// <param name="seed">Optional seed for deterministic gameplay. If null, uses Environment.TickCount.</param>
        /// <param name="playerColors">Which seats to create, in seat order. Defaults to [Red, Blue] (2 players). Must have 2-4 entries, matching the rulebook's supported player count.</param>
        /// <returns>WorldData containing all initialized managers and systems.</returns>
        public WorldData Build(IReplayManager replayManager, int? seed = null, IReadOnlyList<PlayerColor>? playerColors = null)
        {
            var colors = playerColors ?? DefaultPlayerColors;
            if (colors.Count < 2 || colors.Count > 4)
            {
                throw new ArgumentException($"Tyrants of the Underdark supports 2-4 players, got {colors.Count}.", nameof(playerColors));
            }

            // 0. Initialize seeded RNG
            int matchSeed = seed ?? Environment.TickCount;
            var random = new SeededGameRandom(matchSeed, _logger);
            _logger.Log($"Match created with seed: {matchSeed}", LogChannel.Info);

            var playerStateManager = new PlayerStateManager(_logger);

            _logger.Log($"[RNG] Pre-MarketManager: {random.CallCount}", LogChannel.Debug);
            var marketManager = new MarketManager(_cardDatabase, random);
            _logger.Log($"[RNG] Post-MarketManager Checksum: {random.CallCount}", LogChannel.Info);

            _logger.Log($"[RNG] Pre-CreatePlayers: {random.CallCount}", LogChannel.Debug);
            var players = CreatePlayers(colors, _cardDatabase, random, _logger);
            _logger.Log($"[RNG] Post-Players Checksum: {random.CallCount}", LogChannel.Info);

            var turnManager = new TurnManager(players, random, _logger);

            // Create VictoryManager
            var victoryManager = new VictoryManager(_logger);

            var mapManager = SetupMap(turnManager, playerStateManager, _logger);
            var actionSystem = SetupActionSystem(turnManager, mapManager, marketManager, playerStateManager, _logger);

            //ApplyScenarioRules(mapManager); This is for testing purposes only

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

        private static List<Player> CreatePlayers(IReadOnlyList<PlayerColor> colors, ICardDatabase cardDatabase, IGameRandom random, IGameLogger logger)
        {
            var players = new List<Player>();

            for (int seatIndex = 0; seatIndex < colors.Count; seatIndex++)
            {
                var color = colors[seatIndex];
                var player = CreateDefaultPlayer(color, $"Player {color}", seatIndex, cardDatabase, random, logger);
                players.Add(player);
            }

            return players;
        }

        private static Player CreateDefaultPlayer(PlayerColor color, string name, int seatIndex, ICardDatabase cardDatabase, IGameRandom random, IGameLogger logger)
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
            //player.TroopsInBarracks = 5; // Reverted temporary change
            logger.Log($"Created {name} with SeatIndex: {seatIndex}", LogChannel.Info);
            for (int i = 0; i < 3; i++) player.DeckManager.AddToTop(CardFactory.CreateSoldier(random));
            for (int i = 0; i < 7; i++) player.DeckManager.AddToTop(CardFactory.CreateNoble(random));

            // TESTING: Add all devour cards to starting deck
            // NOTE: random must be passed through here, same as CreateSoldier/CreateNoble
            // above - GetCardById(id) without it falls back to CardFactory.GenerateUniqueId's
            // Guid.NewGuid() branch (see CardFactory.cs), making these 4 cards' Card.Id
            // non-deterministic even with an identical seed. That broke exactly what the
            // "Deterministic ID generation for Replay compatibility" comment a few lines up
            // is about - confirmed via ReplayFidelityTests.cs, which failed to hydrate a
            // PlayCardCommand for one of these cards on replay (its recorded CardId no
            // longer matched the freshly-rebuilt hand's CardId, since the ID is regenerated
            // from scratch - via a NEW random Guid suffix each time - on every MatchFactory.
            // Build() call, live or replay, regardless of seed). See planning.txt.
            player.DeckManager.AddToTop(cardDatabase.GetCardById("wight", random)!);
            player.DeckManager.AddToTop(cardDatabase.GetCardById("market_corruptor", random)!);
            player.DeckManager.AddToTop(cardDatabase.GetCardById("skeletal_horde", random)!);
            player.DeckManager.AddToTop(cardDatabase.GetCardById("cultist_of_myrkul", random)!);

            logger.Log($"[RNG] Shuffling Deck for {color}: {(random as SeededGameRandom)?.CallCount ?? -1}", LogChannel.Debug);
            player.DeckManager.Shuffle(random);
            logger.Log($"[RNG] Post-Shuffle for {color}: {(random as SeededGameRandom)?.CallCount ?? -1}", LogChannel.Debug);
            return player;
        }

        private static MapManager SetupMap(ITurnManager turnManager, IPlayerStateManager playerStateManager, IGameLogger logger)
        {
            (List<MapNode> nodes, List<Site> sites, _) = MapFactory.CreateScenarioMap(logger);
            return new MapManager(nodes, sites, turnManager, logger, playerStateManager);
        }

        private static ActionSystem SetupActionSystem(ITurnManager turnManager, IMapManager mapManager, IMarketManager marketManager, IPlayerStateManager playerStateManager, IGameLogger logger)
        {
            return new ActionSystem(turnManager, mapManager, logger, playerStateManager, marketManager);
        }

        internal static void ApplyScenarioRules(MapManager mapManager)
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



