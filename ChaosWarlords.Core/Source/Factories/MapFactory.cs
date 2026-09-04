using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Map;

namespace ChaosWarlords.Source.Utilities
{
    public static class MapFactory
    {
        /// <summary>
        /// Builds the map used for every match today: 5 sites (2 Starting Sites, 1 City,
        /// City of Gold, Obsidian Fortress), connected by 4 routes, generated via
        /// MapLayoutEngine's data-driven MapGenerationConfig pipeline. See planning.txt
        /// section 3 for the real Underdark board this stands in for until that gets built.
        /// </summary>
        public static (List<MapNode>, List<Site>, List<Route>) CreateScenarioMap(IGameLogger logger)
        {
            var config = new MapGenerationConfig();

            // -- Define Sites --
            // 1. Crystal Cave (Starting Site)
            config.Sites.Add(new SiteConfig
            {
                Name = "Crystal Cave",
                IsCity = false,
                IsStartingSite = true,
                Position = new Core.Data.LogicVector2(250 * Core.Data.LogicVector2.ScaleFactor, 100 * Core.Data.LogicVector2.ScaleFactor),
                NodeCount = 2,
                ControlResource = ResourceType.Power,
                ControlAmount = 0,
                TotalControlResource = ResourceType.Power,
                TotalControlAmount = 0,
                EndGameVP = 1 // Starting Sites worth 1 VP per user request
            });

            // 2. Void Portal
            config.Sites.Add(new SiteConfig
            {
                Name = "Void Portal",
                IsCity = false,
                Position = new Core.Data.LogicVector2(250 * Core.Data.LogicVector2.ScaleFactor, 400 * Core.Data.LogicVector2.ScaleFactor),
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
                Position = new Core.Data.LogicVector2(250 * Core.Data.LogicVector2.ScaleFactor, 700 * Core.Data.LogicVector2.ScaleFactor),
                NodeCount = 2,
                ControlResource = ResourceType.Power,
                ControlAmount = 0,
                TotalControlResource = ResourceType.Power,
                TotalControlAmount = 0,
                EndGameVP = 1 // Starting Sites worth 1 VP per user request
            });

            // 4. City of Gold
            config.Sites.Add(new SiteConfig
            {
                Name = "City of Gold",
                IsCity = true,
                Position = new Core.Data.LogicVector2(600 * Core.Data.LogicVector2.ScaleFactor, 400 * Core.Data.LogicVector2.ScaleFactor),
                NodeCount = 4,
                ControlResource = ResourceType.Influence,
                ControlAmount = 1,
                TotalControlResource = ResourceType.VictoryPoints,
                TotalControlAmount = 1,
                EndGameVP = 5 // User Request: 5 VP for control (+2 for Total Control)
            });

            // 5. Obsidian Fortress
            config.Sites.Add(new SiteConfig
            {
                Name = "Obsidian Fortress",
                IsCity = true,
                Position = new Core.Data.LogicVector2(1000 * Core.Data.LogicVector2.ScaleFactor, 400 * Core.Data.LogicVector2.ScaleFactor),
                NodeCount = 6,
                ControlResource = ResourceType.Influence,
                ControlAmount = 1,
                TotalControlResource = ResourceType.VictoryPoints,
                TotalControlAmount = 2,
                EndGameVP = 9 // User Request: 9 VP for control (+2 for Total Control)
            });

            // -- Define Routes --
            config.Routes.Add(new RouteConfig { FromSiteName = "Crystal Cave", ToSiteName = "Void Portal", NodeCount = 2 });
            config.Routes.Add(new RouteConfig { FromSiteName = "Void Portal", ToSiteName = "Shadow Market", NodeCount = 2 });
            config.Routes.Add(new RouteConfig { FromSiteName = "Void Portal", ToSiteName = "City of Gold", NodeCount = 1 });
            config.Routes.Add(new RouteConfig { FromSiteName = "City of Gold", ToSiteName = "Obsidian Fortress", NodeCount = 3 });

            // Generate
            var layoutEngine = new MapLayoutEngine();
            return layoutEngine.GenerateMap(config);
        }
    }
}
