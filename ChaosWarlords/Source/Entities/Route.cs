using System.Collections.Generic;

namespace ChaosWarlords.Source.Entities
{
    public class Route
    {
        public Site From { get; private set; }
        public Site To { get; private set; }
        public List<MapNode> Nodes { get; private set; } = new List<MapNode>();

        public Route(Site from, Site to)
        {
            From = from;
            To = to;
        }

        public void AddNode(MapNode node)
        {
            Nodes.Add(node);
        }
    }
}
