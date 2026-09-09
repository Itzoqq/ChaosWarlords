namespace ChaosWarlords.Source.Utilities
{
    public class MapGenerationConfig
    {
        public List<SiteConfig> Sites { get; set; } = [];
        public List<RouteConfig> Routes { get; set; } = [];
    }

    public class SiteConfig
    {
        public required string Name { get; set; }
        public bool IsCity { get; set; }
        public bool IsStartingSite { get; set; }
        public Core.Data.LogicVector2 Position { get; set; } // Center position
        public int NodeCount { get; set; } = 1;
        public ResourceType ControlResource { get; set; }
        public int ControlAmount { get; set; }
        public ResourceType TotalControlResource { get; set; }
        public int TotalControlAmount { get; set; }
        public int EndGameVP { get; set; }

        // Rulebook p.4 setup step 6: "Put white (unaligned) troop pieces in all troop spaces
        // marked with a [symbol]" - a fixed subset of this site's troop spaces, applied once at
        // generation time (MapLayoutEngine.GenerateSites), not simulated with any RNG. Capped at
        // NodeCount if set higher. MUST be 0 for a StartingSite: rule 11's initial player deploy
        // needs an entirely empty starting site, and MapRuleEngine.CanDeployDuringSetup's
        // "site not already occupied by another player" check is site-wide, not per-node, so any
        // pre-seeded Neutral troop there would permanently block that site's initial deploy for
        // every player. MapLayoutEngine skips + logs a warning rather than seeding it anyway.
        public int NeutralTroopSpaceCount { get; set; }
    }

    public class RouteConfig
    {
        public required string FromSiteName { get; set; }
        public required string ToSiteName { get; set; }
        public int NodeCount { get; set; } // Nodes *between* the sites
    }
}



