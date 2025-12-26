using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities;
using System.Collections.Generic;
using System.IO;

namespace ChaosWarlords.Source.Systems
{
    // WorldData structure
    public class WorldData
    {
        public TurnManager TurnManager { get; set; }
        public MarketManager MarketManager { get; set; }
        public MapManager MapManager { get; set; }
        public ActionSystem ActionSystem { get; set; }
    }

    public class TestWorldFactory
    {
        private readonly ICardDatabase _cardDatabase;

        public TestWorldFactory(ICardDatabase cardDatabase)
        {
            _cardDatabase = cardDatabase;
        }

        // Build Method
        public WorldData Build()
        {
            // 1. Setup Market
            var marketManager = new MarketManager(_cardDatabase);

            // 2. Setup Players (Now two players)
            var players = new List<Player>();

            // Player 1 (Red)
            var playerRed = new Player(PlayerColor.Red);
            for (int i = 0; i < 3; i++) playerRed.DeckManager.AddToTop(CardFactory.CreateSoldier());
            for (int i = 0; i < 7; i++) playerRed.DeckManager.AddToTop(CardFactory.CreateNoble());
            playerRed.DeckManager.Shuffle();
            players.Add(playerRed);

            // Player 2 (Blue)
            var playerBlue = new Player(PlayerColor.Blue);
            for (int i = 0; i < 3; i++) playerBlue.DeckManager.AddToTop(CardFactory.CreateSoldier());
            for (int i = 0; i < 7; i++) playerBlue.DeckManager.AddToTop(CardFactory.CreateNoble());
            playerBlue.DeckManager.Shuffle();
            players.Add(playerBlue);

            // 3. Setup Turn Manager
            var turnManager = new TurnManager(players);

            // Note: turnManager.ActivePlayer is now valid immediately after construction

            // 4. Setup Map (Procedural Generation)
            var config = new MapGenerationConfig();

            // -- Define Sites --
            // 1. Crystal Cave (Starting Site)
            config.Sites.Add(new SiteConfig 
            { 
                Name = "Crystal Cave", 
                IsCity = false,
                IsStartingSite = true,
                Position = new Microsoft.Xna.Framework.Vector2(250, 100), 
                NodeCount = 2,
                ControlResource = ResourceType.Power, 
                ControlAmount = 0,
                TotalControlResource = ResourceType.Power, 
                TotalControlAmount = 0,
                EndGameVP = 2
            });

            // 2. Void Portal
            config.Sites.Add(new SiteConfig 
            { 
                Name = "Void Portal", 
                IsCity = false, 
                Position = new Microsoft.Xna.Framework.Vector2(250, 400), 
                NodeCount = 3,
                ControlResource = ResourceType.Power, 
                ControlAmount = 0,
                TotalControlResource = ResourceType.Power, 
                TotalControlAmount = 0,
                EndGameVP = 1
            });

            // 3. Shadow Market (Starting Site)
            config.Sites.Add(new SiteConfig 
            { 
                Name = "Shadow Market", 
                IsCity = false,
                IsStartingSite = true,
                Position = new Microsoft.Xna.Framework.Vector2(250, 700), 
                NodeCount = 2,
                ControlResource = ResourceType.Power, 
                ControlAmount = 0,
                TotalControlResource = ResourceType.Power, 
                TotalControlAmount = 0,
                EndGameVP = 2
            });

            // 4. City of Gold
            config.Sites.Add(new SiteConfig 
            { 
                Name = "City of Gold", 
                IsCity = true, 
                Position = new Microsoft.Xna.Framework.Vector2(600, 400), 
                NodeCount = 4,
                ControlResource = ResourceType.Influence, 
                ControlAmount = 1,
                TotalControlResource = ResourceType.VictoryPoints, 
                TotalControlAmount = 1,
                EndGameVP = 0
            });

            // 5. Obsidian Fortress
            config.Sites.Add(new SiteConfig 
            { 
                Name = "Obsidian Fortress", 
                IsCity = true, 
                Position = new Microsoft.Xna.Framework.Vector2(1000, 400), 
                NodeCount = 6,
                ControlResource = ResourceType.Influence, 
                ControlAmount = 1,
                TotalControlResource = ResourceType.VictoryPoints, 
                TotalControlAmount = 2,
                EndGameVP = 0
            });

            // -- Define Routes --
            config.Routes.Add(new RouteConfig { FromSiteName = "Crystal Cave", ToSiteName = "Void Portal", NodeCount = 2 });
            config.Routes.Add(new RouteConfig { FromSiteName = "Void Portal", ToSiteName = "Shadow Market", NodeCount = 2 });
            config.Routes.Add(new RouteConfig { FromSiteName = "Void Portal", ToSiteName = "City of Gold", NodeCount = 1 });
            config.Routes.Add(new RouteConfig { FromSiteName = "City of Gold", ToSiteName = "Obsidian Fortress", NodeCount = 3 });

            // Generate
            var layoutEngine = new MapLayoutEngine();
            (List<MapNode> nodes, List<Site> sites, List<Route> routes) = layoutEngine.GenerateMap(config);

            var mapManager = new MapManager(nodes, sites);

            // 5. Setup Action System
            // REFACTOR: ActionSystem is now initialized with the TurnManager, not the Player
            var actionSystem = new ActionSystem(turnManager, mapManager);

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
                TurnManager = turnManager,
                MarketManager = marketManager,
                MapManager = mapManager,
                ActionSystem = actionSystem
            };
        }
    }
}