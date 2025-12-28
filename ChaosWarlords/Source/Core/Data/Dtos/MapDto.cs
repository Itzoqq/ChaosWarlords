using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Data;

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
            if (node == null) return;
            Id = node.Id;
            Occupant = node.Occupant;
        }

        public MapNode ToEntity()
        {
            throw new System.NotImplementedException("MapNode hydration requires IMapManager context.");
        }
    }

    /// <summary>
    /// Container DTO for the entire map state.
    /// </summary>
    public class MapDto
    {
        public System.Collections.Generic.List<MapNodeDto> Nodes { get; set; } = new System.Collections.Generic.List<MapNodeDto>();
    }
}
