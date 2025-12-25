using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.Utilities
{
    public class MapGenerationConfig
    {
        public List<SiteConfig> Sites { get; set; } = new List<SiteConfig>();
        public List<RouteConfig> Routes { get; set; } = new List<RouteConfig>();
    }

    public class SiteConfig
    {
        public string Name { get; set; }
        public bool IsCity { get; set; }
        public Vector2 Position { get; set; } // Center position
        public int NodeCount { get; set; } = 1;
        public ResourceType ControlResource { get; set; }
        public int ControlAmount { get; set; }
        public ResourceType TotalControlResource { get; set; }
        public int TotalControlAmount { get; set; }
        public int EndGameVP { get; set; }
    }

    public class RouteConfig
    {
        public string FromSiteName { get; set; }
        public string ToSiteName { get; set; }
        public int NodeCount { get; set; } // Nodes *between* the sites
    }
}
