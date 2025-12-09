using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Entities
{
    public class MapNode
    {
        // Data
        public int Id { get; private set; }
        public Vector2 Position { get; set; }
        public PlayerColor Occupant { get; set; } = PlayerColor.None;

        // Navigation: Which other nodes are connected to this one?
        // We will use this later to calculate "Presence"
        public List<MapNode> Neighbors { get; private set; } = new List<MapNode>();

        // Visuals
        private const int Radius = 20; // Size of the circle
        private Texture2D _texture;
        public bool IsHovered { get; private set; }

        public MapNode(int id, Vector2 position, Texture2D texture)
        {
            Id = id;
            Position = position;
            _texture = texture;
        }

        public void AddNeighbor(MapNode node)
        {
            if (!Neighbors.Contains(node))
            {
                Neighbors.Add(node);
                // Bi-directional connection
                node.Neighbors.Add(this);
            }
        }

        public void Update(MouseState mouseState)
        {
            // Circular collision detection
            // We check if the distance between mouse and center is less than radius
            float distance = Vector2.Distance(new Vector2(mouseState.X, mouseState.Y), Position);
            IsHovered = distance < Radius;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Determine color based on occupant
            Color drawColor = Color.Gray; // Default empty
            if (Occupant == PlayerColor.Red) drawColor = Color.Red;
            else if (Occupant == PlayerColor.Blue) drawColor = Color.Blue;
            else if (Occupant == PlayerColor.Neutral) drawColor = Color.White;

            // Highlight if hovered
            if (IsHovered) drawColor = Color.Lerp(drawColor, Color.Yellow, 0.5f);

            // Draw the circle (centered)
            Vector2 origin = new Vector2(_texture.Width / 2f, _texture.Height / 2f);

            // Scale: simple math to make our 1x1 pixel into a circle of 'Radius' size
            // (Note: In a real game we would load a circular PNG, but we are using the magic pixel for now)
            Rectangle rect = new Rectangle(
                (int)(Position.X - Radius),
                (int)(Position.Y - Radius),
                Radius * 2,
                Radius * 2);

            spriteBatch.Draw(_texture, rect, drawColor);
        }
    }
}