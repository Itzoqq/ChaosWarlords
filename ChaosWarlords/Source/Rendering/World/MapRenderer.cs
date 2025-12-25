using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Text;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Views
{
    [ExcludeFromCodeCoverage]
    public class MapRenderer
    {
        private Texture2D _pixelTexture;
        private Texture2D _nodeTexture;
        private SpriteFont _font;

        // --- Site Text Cache ---
        // Stores the StringBuilder for each site to avoid per-frame allocations.
        private class SiteVisualData
        {
            public StringBuilder Text { get; } = new StringBuilder();
            public PlayerColor LastOwner { get; set; } = PlayerColor.None;
            public bool LastTotalControl { get; set; } = false;
            // We force an update on the first draw
            public bool IsDirty { get; set; } = true;
        }

        private Dictionary<Site, SiteVisualData> _siteCache = new Dictionary<Site, SiteVisualData>();

        public MapRenderer(Texture2D pixelTexture, Texture2D nodeTexture, SpriteFont font)
        {
            _pixelTexture = pixelTexture;
            _nodeTexture = nodeTexture;
            _font = font;
        }

        public void Draw(SpriteBatch spriteBatch, IMapManager map, MapNode hoveredNode, Site hoveredSite)
        {
            DrawRoutes(spriteBatch, map);
            DrawSites(spriteBatch, map.Sites);
            DrawNodes(spriteBatch, map.Nodes, hoveredNode);
        }

        private void DrawSites(SpriteBatch spriteBatch, IReadOnlyList<Site> sites)
        {
            if (sites == null) return;
            foreach (var site in sites)
            {
                // Background
                spriteBatch.Draw(_pixelTexture, site.Bounds, Color.Black * 0.5f);

                // Border
                Color borderColor = (site.Owner == PlayerColor.None) ? Color.Gray : GetColor(site.Owner);
                DrawBorder(spriteBatch, site.Bounds, borderColor, 2);

                // Text
                DrawSiteText(spriteBatch, site);

                // Spies
                DrawSpies(spriteBatch, site);
            }
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Bottom, rect.Width, thickness), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Right, rect.Y, thickness, rect.Height), color);
        }

        private void DrawSiteText(SpriteBatch spriteBatch, Site site)
        {
            // 1. Get or Create Cache Entry
            if (!_siteCache.TryGetValue(site, out SiteVisualData cache))
            {
                cache = new SiteVisualData();
                _siteCache[site] = cache;
            }

            // 2. Check for changes (Dirty Flag)
            if (cache.IsDirty || cache.LastOwner != site.Owner || cache.LastTotalControl != site.HasTotalControl)
            {
                UpdateSiteText(cache, site);
                cache.LastOwner = site.Owner;
                cache.LastTotalControl = site.HasTotalControl;
                cache.IsDirty = false;
            }

            // 3. Draw using StringBuilder
            Vector2 textPos = new Vector2(site.Bounds.X + 10, site.Bounds.Y + 10);

            // Draw Shadow
            spriteBatch.DrawString(_font, cache.Text, textPos + new Vector2(1, 1), Color.Black);
            // Draw Text
            spriteBatch.DrawString(_font, cache.Text, textPos, site.IsCity ? Color.Gold : Color.LightGray);
        }

        private void UpdateSiteText(SiteVisualData cache, Site site)
        {
            var sb = cache.Text;
            sb.Clear();
            sb.Append(site.Name.ToUpper());

            if (site.Owner != PlayerColor.None)
            {
                sb.Append("\n[Control: +");
                sb.Append(site.ControlAmount);
                sb.Append(" ");
                sb.Append(site.ControlResource);
                sb.Append("]");

                if (site.HasTotalControl)
                {
                    sb.Append("\n[TOTAL: +");
                    sb.Append(site.TotalControlAmount);
                    sb.Append(" ");
                    sb.Append(site.TotalControlResource);
                    sb.Append("]");
                }
            }
            else
            {
                sb.Append("\n(");
                sb.Append(site.ControlAmount);
                sb.Append(" ");
                sb.Append(site.ControlResource);
                sb.Append(")");
            }
        }

        private void DrawSpies(SpriteBatch spriteBatch, Site site)
        {
            int spySize = 12;
            int startX = site.Bounds.X - (spySize / 2);
            int startY = site.Bounds.Y - (spySize / 2);
            int i = 0;

            foreach (var spyColor in site.Spies)
            {
                Color drawColor = GetColor(spyColor);
                Vector2 spyPos = new Vector2(startX + (i * (spySize + 2)), startY);
                Rectangle spyRect = new Rectangle((int)spyPos.X, (int)spyPos.Y, spySize, spySize);

                spriteBatch.Draw(_pixelTexture, new Rectangle(spyRect.X - 1, spyRect.Y - 1, spyRect.Width + 2, spyRect.Height + 2), Color.Black);
                spriteBatch.Draw(_pixelTexture, spyRect, drawColor);
                i++;
            }
        }

        private void DrawNodes(SpriteBatch spriteBatch, IReadOnlyList<MapNode> nodes, MapNode hoveredNode)
        {
            foreach (var node in nodes)
            {
                Color drawColor = Color.Gray;

                // 1. Determine Base Player Color (Original Logic)
                if (node.Occupant == PlayerColor.Red) drawColor = Color.Red;
                else if (node.Occupant == PlayerColor.Blue) drawColor = Color.Blue;
                else if (node.Occupant == PlayerColor.Neutral) drawColor = Color.White;
                // Unoccupied nodes remain Color.Gray

                // 2. Apply Highlight Logic (NEW Logic)
                if (node == hoveredNode)
                {
                    // Goal: Make the color a lighter/brighter version of itself.
                    // Lerping (blending) the base color toward white achieves this effect.
                    // 0.4f gives a noticeable, subtle highlight without looking yellow/orange.

                    if (node.Occupant == PlayerColor.Red || node.Occupant == PlayerColor.Blue)
                    {
                        // Player troops get a brightened version of their color.
                        drawColor = Color.Lerp(drawColor, Color.White, 0.4f);
                    }
                    else
                    {
                        // Unoccupied/Neutral still use a gentle highlight (like the old system)
                        drawColor = Color.Lerp(drawColor, Color.Yellow, 0.5f);
                    }
                }

                int radius = MapNode.Radius;
                Rectangle rect = new Rectangle(
                    (int)(node.Position.X - radius),
                    (int)(node.Position.Y - radius),
                    radius * 2,
                    radius * 2);

                spriteBatch.Draw(_nodeTexture, rect, drawColor);
            }
        }

        private void DrawRoutes(SpriteBatch spriteBatch, IMapManager map)
        {
            foreach (var node in map.Nodes)
            {
                foreach (var neighbor in node.Neighbors)
                {
                    if (node.Id < neighbor.Id)
                    {
                        DrawSingleRoute(spriteBatch, map, node, neighbor);
                    }
                }
            }
        }

        private void DrawSingleRoute(SpriteBatch spriteBatch, IMapManager map, MapNode node, MapNode neighbor)
        {
            Site startSite = map.GetSiteForNode(node);
            Site endSite = map.GetSiteForNode(neighbor);

            if (startSite != null && startSite == endSite) return;

            Vector2 p1 = startSite != null ? startSite.Bounds.Center.ToVector2() : node.Position;
            Vector2 p2 = endSite != null ? endSite.Bounds.Center.ToVector2() : neighbor.Position;

            if (startSite != null) p1 = GetIntersection(startSite.Bounds, p2, p1);
            if (endSite != null) p2 = GetIntersection(endSite.Bounds, p1, p2);

            DrawLine(spriteBatch, p1, p2, Color.DarkGray, 2);
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, int thickness)
        {
            Vector2 edge = end - start;
            float angle = (float)System.Math.Atan2(edge.Y, edge.X);

            spriteBatch.Draw(_pixelTexture,
                new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), thickness),
                null, color, angle, new Vector2(0, 0.5f), SpriteEffects.None, 0);
        }

        private Vector2 GetIntersection(Rectangle rect, Vector2 start, Vector2 end)
        {
            if (MapGeometry.TryGetLineIntersection(start, end, new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Top), out Vector2 r)) return r;
            if (MapGeometry.TryGetLineIntersection(start, end, new Vector2(rect.Right, rect.Top), new Vector2(rect.Right, rect.Bottom), out r)) return r;
            if (MapGeometry.TryGetLineIntersection(start, end, new Vector2(rect.Right, rect.Bottom), new Vector2(rect.Left, rect.Bottom), out r)) return r;
            if (MapGeometry.TryGetLineIntersection(start, end, new Vector2(rect.Left, rect.Bottom), new Vector2(rect.Left, rect.Top), out r)) return r;
            return end;
        }

        private Color GetColor(PlayerColor p)
        {
            if (p == PlayerColor.Red) return Color.Red;
            if (p == PlayerColor.Blue) return Color.Blue;
            return Color.White;
        }
    }
}