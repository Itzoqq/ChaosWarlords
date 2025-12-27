using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Entities.Map
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


