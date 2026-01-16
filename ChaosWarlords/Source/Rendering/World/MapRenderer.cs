using ChaosWarlords.Source.Core.Interfaces.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using ChaosWarlords.Source.Contexts;
using System.Globalization;

namespace ChaosWarlords.Source.Rendering.World
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
            public bool LastTotalControl { get; set; }
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

        public void Draw(SpriteBatch spriteBatch, IMapManager map, MapNode? hoveredNode, Site? hoveredSite)
        {
            DrawRoutes(spriteBatch, map);
            DrawSites(spriteBatch, map);
            DrawNodes(spriteBatch, map.Nodes, hoveredNode);
        }

        private void DrawSites(SpriteBatch spriteBatch, IMapManager map)
        {
            var sites = map.Sites;
            if (sites is null) return;
            foreach (var site in sites)
            {
                // Background
                spriteBatch.Draw(_pixelTexture, site.Bounds, Color.Black * 0.5f);

                // Border
                Color borderColor = (site.Owner == PlayerColor.None) ? Color.Gray : GetColor(site.Owner);
                int thickness = 2;

                // Visual Cue for Phase 0 (Setup)
                if (map.CurrentPhase == MatchPhase.Setup && site is StartingSite)
                {
                    borderColor = Color.LimeGreen;
                    thickness = 4;
                }

                DrawBorder(spriteBatch, site.Bounds, borderColor, thickness);

                // Text
                DrawSiteText(spriteBatch, site);

                // Spies
                DrawSpies(spriteBatch, site);
            }
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            // Use single pooled rectangle, reuse for all 4 sides (0 allocations vs 4)
            using var pooledRect = PooledRectangle.Rent(0, 0, 0, 0);

            pooledRect.Value = new Rectangle(rect.X, rect.Y, rect.Width, thickness);
            spriteBatch.Draw(_pixelTexture, pooledRect.Value, color);

            pooledRect.Value = new Rectangle(rect.X, rect.Bottom, rect.Width, thickness);
            spriteBatch.Draw(_pixelTexture, pooledRect.Value, color);

            pooledRect.Value = new Rectangle(rect.X, rect.Y, thickness, rect.Height);
            spriteBatch.Draw(_pixelTexture, pooledRect.Value, color);

            pooledRect.Value = new Rectangle(rect.Right, rect.Y, thickness, rect.Height);
            spriteBatch.Draw(_pixelTexture, pooledRect.Value, color);
        }

        private void DrawSiteText(SpriteBatch spriteBatch, Site site)
        {
            // 1. Get or Create Cache Entry
            if (!_siteCache.TryGetValue(site, out var cache))
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

            // 3. Draw using StringBuilder (pooled vectors for 0 allocations)
            using var textPos = PooledVector2.Rent(site.Bounds.X + GameConstants.UILayout.MediumPadding, site.Bounds.Y + GameConstants.UILayout.MediumPadding);
            using var shadowOffset = PooledVector2.Rent(1, 1);

            // Draw Shadow
            spriteBatch.DrawString(_font, cache.Text, textPos.Value + shadowOffset.Value, Color.Black);
            // Draw Text
            spriteBatch.DrawString(_font, cache.Text, textPos.Value, site.IsCity ? Color.Gold : Color.LightGray);
        }

        private static void UpdateSiteText(SiteVisualData cache, Site site)
        {
            var sb = cache.Text;
            sb.Clear();
            sb.Append(site.Name.ToUpper(CultureInfo.InvariantCulture));

            if (site.Owner != PlayerColor.None)
            {
                sb.Append("\n[Control: +");
                sb.Append(site.ControlAmount);
                sb.Append(' ');
                sb.Append(site.ControlResource);
                sb.Append(']');

                if (site.HasTotalControl)
                {
                    sb.Append("\n[TOTAL: +");
                    sb.Append(site.TotalControlAmount);
                    sb.Append(' ');
                    sb.Append(site.TotalControlResource);
                    sb.Append(']');
                }
            }
            else
            {
                {
                    sb.Append('\n');
                    sb.Append('(');
                    sb.Append(site.ControlAmount);
                    sb.Append(' ');
                    sb.Append(site.ControlResource);
                    sb.Append(')');
                }
            }
        }

        private void DrawSpies(SpriteBatch spriteBatch, Site site)
        {
            int spySize = 12;
            int startX = site.Bounds.X - (spySize / 2);
            int startY = site.Bounds.Y - (spySize / 2);
            int i = 0;

            // Pool rectangles outside loop for reuse (0 allocations per spy)
            using var spyRect = PooledRectangle.Rent(0, 0, spySize, spySize);
            using var spyBorder = PooledRectangle.Rent(0, 0, 0, 0);

            foreach (var spyColor in site.Spies)
            {
                Color drawColor = GetColor(spyColor);
                int spyX = startX + (i * (spySize + 2));
                int spyY = startY;

                // Update pooled rectangles
                spyRect.Value = new Rectangle(spyX, spyY, spySize, spySize);
                spyBorder.Value = new Rectangle(spyX - 1, spyY - 1, spySize + 2, spySize + 2);

                spriteBatch.Draw(_pixelTexture, spyBorder.Value, Color.Black);
                spriteBatch.Draw(_pixelTexture, spyRect.Value, drawColor);
                i++;
            }
        }

        private void DrawNodes(SpriteBatch spriteBatch, IReadOnlyList<MapNode> nodes, MapNode? hoveredNode)
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
                // Use pooled rectangle (0 allocations per node)
                using var rect = PooledRectangle.Rent(
                    (int)(node.Position.X - radius),
                    (int)(node.Position.Y - radius),
                    radius * 2,
                    radius * 2);

                spriteBatch.Draw(_nodeTexture, rect.Value, drawColor);
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
            Site? startSite = map.GetSiteForNode(node);
            Site? endSite = map.GetSiteForNode(neighbor);

            if (startSite is not null && startSite == endSite) return;

            Vector2 p1 = startSite is not null ? startSite.Bounds.Center.ToVector2() : node.Position;
            Vector2 p2 = endSite is not null ? endSite.Bounds.Center.ToVector2() : neighbor.Position;

            if (startSite is not null) p1 = GetIntersection(startSite.Bounds, p2, p1);
            if (endSite is not null) p2 = GetIntersection(endSite.Bounds, p1, p2);

            DrawLine(spriteBatch, p1, p2, Color.DarkGray, 2);
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, int thickness)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);

            // Use pooled rectangle and vector (0 allocations per line)
            using var lineRect = PooledRectangle.Rent((int)start.X, (int)start.Y, (int)edge.Length(), thickness);
            using var origin = PooledVector2.Rent(0, 0.5f);

            spriteBatch.Draw(_pixelTexture,
                lineRect.Value,
                null, color, angle, origin.Value, SpriteEffects.None, 0);
        }

        private static Vector2 GetIntersection(Rectangle rect, Vector2 start, Vector2 end)
        {
            var lStart = Core.Data.LogicVector2.FromVector2(start);
            var lEnd = Core.Data.LogicVector2.FromVector2(end);

            // Use pooled vectors for corner calculations (0 allocations per intersection)
            using var topLeftVec = PooledVector2.Rent(rect.Left, rect.Top);
            using var topRightVec = PooledVector2.Rent(rect.Right, rect.Top);
            using var bottomRightVec = PooledVector2.Rent(rect.Right, rect.Bottom);
            using var bottomLeftVec = PooledVector2.Rent(rect.Left, rect.Bottom);

            var topLeft = Core.Data.LogicVector2.FromVector2(topLeftVec.Value);
            var topRight = Core.Data.LogicVector2.FromVector2(topRightVec.Value);
            var bottomRight = Core.Data.LogicVector2.FromVector2(bottomRightVec.Value);
            var bottomLeft = Core.Data.LogicVector2.FromVector2(bottomLeftVec.Value);

            if (MapGeometry.TryGetLineIntersection(lStart, lEnd, topLeft, topRight, out var lr1)) return lr1.ToVector2();
            if (MapGeometry.TryGetLineIntersection(lStart, lEnd, topRight, bottomRight, out var lr2)) return lr2.ToVector2();
            if (MapGeometry.TryGetLineIntersection(lStart, lEnd, bottomRight, bottomLeft, out var lr3)) return lr3.ToVector2();
            if (MapGeometry.TryGetLineIntersection(lStart, lEnd, bottomLeft, topLeft, out var lr4)) return lr4.ToVector2();

            return end;
        }

        private static Color GetColor(PlayerColor p)
        {
            if (p == PlayerColor.Red) return Color.Red;
            if (p == PlayerColor.Blue) return Color.Blue;
            return Color.White;
        }
    }
}


