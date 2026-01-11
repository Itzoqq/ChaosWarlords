using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Data;
using System.Linq;

namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// DTO for MapNode state. 
    /// Only captures dynamic data (Occupant). 
    /// Static data (Position, Neighbors) is assumed to be part of the static map definition.
    /// </summary>
    public class MapNodeDto : IDto<MapNode>
    {
        public int Id { get; set; }
        public PlayerColor Occupant { get; set; }

        public MapNodeDto() { }

        public MapNodeDto(MapNode node)
        {
            if (node is null) return;
            Id = node.Id;
            Occupant = node.Occupant;
        }

        public MapNode ToEntity()
        {
            throw new System.NotImplementedException("MapNode hydration requires IMapManager context.");
        }
    }

    /// <summary>
    /// DTO for Site state.
    /// Captures dynamic data like Spies.
    /// </summary>
    public class SiteDto : IDto<Site>
    {
        public int Id { get; set; }
        public System.Collections.Generic.List<string> Spies { get; set; } = new System.Collections.Generic.List<string>();

        public SiteDto() { }

        public SiteDto(Site site)
        {
            if (site is null) return;
            Id = site.Id;
            Spies = site.Spies.Select(s => s.ToString()).ToList();
        }

        public Site ToEntity()
        {
            throw new System.NotImplementedException("Site hydration requires IMapManager context.");
        }
    }

    /// <summary>
    /// Container DTO for the entire map state.
    /// </summary>
    public class MapDto
    {
        public System.Collections.Generic.List<MapNodeDto> Nodes { get; set; } = new System.Collections.Generic.List<MapNodeDto>();
        public System.Collections.Generic.List<SiteDto> Sites { get; set; } = new System.Collections.Generic.List<SiteDto>();
    }
}
