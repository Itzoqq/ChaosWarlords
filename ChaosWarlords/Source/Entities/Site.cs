using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
{
    public abstract class Site
    {
        public string Name { get; protected set; }

        public ResourceType ControlResource { get; protected set; }
        public int ControlAmount { get; protected set; }

        public ResourceType TotalControlResource { get; protected set; }
        public int TotalControlAmount { get; protected set; }

        public int EndGameVictoryPoints { get; set; }

        public bool IsCity { get; set; }

        public List<MapNode> NodesInternal { get; protected set; } = new List<MapNode>();
        public PlayerColor Owner { get; internal set; } = PlayerColor.None;
        internal List<PlayerColor> Spies { get; private set; } = new List<PlayerColor>();
        public bool HasTotalControl { get; internal set; } = false;

        // Visual Bounds (Kept in Model for Hit-Testing logic)
        public Rectangle Bounds { get; protected set; }

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

        public virtual void AddNode(MapNode node)
        {
            NodesInternal.Add(node);
            RecalculateBounds();
        }

        public virtual void RecalculateBounds()
        {
            if (NodesInternal.Count == 0) return;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var node in NodesInternal)
            {
                if (node.Position.X < minX) minX = node.Position.X;
                if (node.Position.Y < minY) minY = node.Position.Y;
                if (node.Position.X > maxX) maxX = node.Position.X;
                if (node.Position.Y > maxY) maxY = node.Position.Y;
            }

            // Logic Padding for "Hit Box"
            int sidePadding = GameConstants.SiteVisuals.SIDE_PADDING;
            int topPadding = GameConstants.SiteVisuals.TOP_PADDING;
            int bottomPadding = GameConstants.SiteVisuals.BOTTOM_PADDING;

            int width = (int)(maxX - minX) + (sidePadding * 2);
            int height = (int)(maxY - minY) + topPadding + bottomPadding;

            Bounds = new Rectangle((int)minX - sidePadding, (int)minY - topPadding, width, height);
        }

        public int GetTroopCount(PlayerColor color)
        {
            return NodesInternal.Count(n => n.Occupant == color);
        }

        public bool HasTroop(PlayerColor color)
        {
            return NodesInternal.Any(n => n.Occupant == color);
        }

        public PlayerColor GetControllingPlayer()
        {
            var troopCounts = NodesInternal
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