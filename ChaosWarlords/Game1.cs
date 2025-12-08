using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;
using System;

namespace ChaosWarlords
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        // Assets
        private Texture2D _pixelTexture;
        private SpriteFont _defaultFont; 

        // Game State
        private List<Card> _hand = new List<Card>();
        private List<MapNode> _mapNodes = new List<MapNode>(); // <--- NEW: Map Data

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            try { _defaultFont = Content.Load<SpriteFont>("fonts/DefaultFont"); } 
            catch { }

            // --- INIT CARDS ---
            var card1 = CardFactory.CreateSoldier(_pixelTexture);
            card1.Position = new Vector2(100, 500); // Moved down to make room for map
            var card2 = CardFactory.CreateNoble(_pixelTexture);
            card2.Position = new Vector2(260, 500); 
            _hand.Add(card1);
            _hand.Add(card2);

            // --- INIT MAP ---
            _mapNodes = MapFactory.CreateTestMap(_pixelTexture);
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            var mouseState = Mouse.GetState();

            // Update Cards
            foreach (var card in _hand)
            {
                card.Update(gameTime, mouseState);
            }

            // Update Map Nodes
            foreach (var node in _mapNodes)
            {
                node.Update(mouseState);
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateBlue); 

            _spriteBatch.Begin();

            // 1. Draw Map Connections (Lines) FIRST so they are behind nodes
            foreach (var node in _mapNodes)
            {
                foreach(var neighbor in node.Neighbors)
                {
                    // Draw a line between node and neighbor
                    DrawLine(_spriteBatch, node.Position, neighbor.Position, Color.DarkGray, 3);
                }
            }

            // 2. Draw Map Nodes
            foreach(var node in _mapNodes)
            {
                node.Draw(_spriteBatch);
            }

            // 3. Draw Cards (UI Layer on top)
            foreach (var card in _hand)
            {
                card.Draw(_spriteBatch, _defaultFont);
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        // Helper to draw lines using just a pixel texture
        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, int thickness)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            
            spriteBatch.Draw(_pixelTexture,
                new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), thickness),
                null,
                color,
                angle,
                new Vector2(0, 0.5f), // Origin at left-middle
                SpriteEffects.None,
                0);
        }
    }
}