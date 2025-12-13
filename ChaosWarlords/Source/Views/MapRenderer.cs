using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Views
{
    public class MapRenderer
    {
        private Texture2D _pixelTexture;
        private Texture2D _nodeTexture; // Ideally a circle sprite, we'll use pixel for now
        private SpriteFont _font;

        public MapRenderer(Texture2D pixelTexture, Texture2D nodeTexture, SpriteFont font)
        {
            _pixelTexture = pixelTexture;
            _nodeTexture = nodeTexture;
            _font = font;
        }

        public void Draw(SpriteBatch spriteBatch, MapManager map, MapNode hoveredNode, Site hoveredSite)
        {
            DrawSites(spriteBatch, map.Sites);
            DrawRoutes(spriteBatch, map);
            DrawNodes(spriteBatch, map.Nodes, hoveredNode);
        }

        private void DrawSites(SpriteBatch spriteBatch, List<Site> sites)
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
            string text = site.Name.ToUpper();
            if (site.Owner != PlayerColor.None)
            {
                text += $"\n[Control: +{site.ControlAmount} {site.ControlResource}]";
                if (site.HasTotalControl)
                {
                    text += $"\n[TOTAL BONUS: +{site.TotalControlAmount} {site.TotalControlResource}]";
                }
            }
            else
            {
                text += $"\n({site.ControlAmount} {site.ControlResource})";
            }

            Vector2 textPos = new Vector2(site.Bounds.X + 10, site.Bounds.Y + 10);
            spriteBatch.DrawString(_font, text, textPos + new Vector2(1, 1), Color.Black);
            spriteBatch.DrawString(_font, text, textPos, site.IsCity ? Color.Gold : Color.LightGray);
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

        private void DrawNodes(SpriteBatch spriteBatch, List<MapNode> nodes, MapNode hoveredNode)
        {
            foreach (var node in nodes)
            {
                Color drawColor = Color.Gray;
                if (node.Occupant == PlayerColor.Red) drawColor = Color.Red;
                else if (node.Occupant == PlayerColor.Blue) drawColor = Color.Blue;
                else if (node.Occupant == PlayerColor.Neutral) drawColor = Color.White;

                // Highlight if hovered
                if (node == hoveredNode) drawColor = Color.Lerp(drawColor, Color.Yellow, 0.5f);

                int radius = MapNode.Radius;
                Rectangle rect = new Rectangle(
                    (int)(node.Position.X - radius),
                    (int)(node.Position.Y - radius),
                    radius * 2,
                    radius * 2);

                spriteBatch.Draw(_nodeTexture, rect, drawColor);
            }
        }

        private void DrawRoutes(SpriteBatch spriteBatch, MapManager map)
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

        private void DrawSingleRoute(SpriteBatch spriteBatch, MapManager map, MapNode node, MapNode neighbor)
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
            // Re-using the geometry logic extracted from MapManager
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