using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
{
    public class Site
    {
        public string Name { get; private set; }

        public ResourceType ControlResource { get; private set; }
        public int ControlAmount { get; private set; }

        public ResourceType TotalControlResource { get; private set; }
        public int TotalControlAmount { get; private set; }

        public bool IsCity { get; set; }

        public List<MapNode> NodesInternal { get; private set; } = new List<MapNode>();
        public PlayerColor Owner { get; internal set; } = PlayerColor.None;
        internal List<PlayerColor> Spies { get; private set; } = new List<PlayerColor>();
        public bool HasTotalControl { get; internal set; } = false;

        // Visual Bounds (Kept in Model for Hit-Testing logic)
        public Rectangle Bounds { get; private set; }

        public Site(string name,
                    ResourceType controlType, int controlAmt,
                    ResourceType totalType, int totalAmt)
        {
            Name = name;
            ControlResource = controlType;
            ControlAmount = controlAmt;
            TotalControlResource = totalType;
            TotalControlAmount = totalAmt;
        }

        public void AddNode(MapNode node)
        {
            NodesInternal.Add(node); // Change: Nodes to NodesInternal
            RecalculateBounds();
        }

        public void RecalculateBounds()
        {
            if (NodesInternal.Count == 0) return; // Change: Nodes to NodesInternal

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var node in NodesInternal) // Change: Nodes to NodesInternal
            {
                if (node.Position.X < minX) minX = node.Position.X;
                if (node.Position.Y < minY) minY = node.Position.Y;
                if (node.Position.X > maxX) maxX = node.Position.X;
                if (node.Position.Y > maxY) maxY = node.Position.Y;
            }

            // Logic Padding for "Hit Box"
            int sidePadding = 35;
            int topPadding = 70;
            int bottomPadding = 35;

            int width = (int)(maxX - minX) + (sidePadding * 2);
            int height = (int)(maxY - minY) + topPadding + bottomPadding;

            Bounds = new Rectangle((int)minX - sidePadding, (int)minY - topPadding, width, height);
        }

        public int GetTroopCount(PlayerColor color)
        {
            return NodesInternal.Count(n => n.Occupant == color); // Change: Nodes to NodesInternal
        }

        public PlayerColor GetControllingPlayer()
        {
            var troopCounts = NodesInternal // Change: Nodes to NodesInternal
                .Where(n => n.Occupant != PlayerColor.None && n.Occupant != PlayerColor.Neutral)
                .GroupBy(n => n.Occupant)
                .Select(g => new { Player = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            if (troopCounts.Count == 0) return PlayerColor.None;

            if (troopCounts.Count > 1 && troopCounts[0].Count == troopCounts[1].Count)
                return PlayerColor.None;

            return troopCounts[0].Player;
        }

        public bool HasSpy(PlayerColor color) => Spies.Contains(color);
        public void AddSpy(PlayerColor color) => Spies.Add(color);
        public bool RemoveSpy(PlayerColor color) => Spies.Remove(color);
    }
}