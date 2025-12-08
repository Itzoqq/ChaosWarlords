using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems; // Add this
using System.Collections.Generic;
using System;

namespace ChaosWarlords
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Texture2D _pixelTexture;
        private SpriteFont _defaultFont; 

        // Game State
        private List<Card> _hand = new List<Card>();
        private MapManager _mapManager; // Replaces List<MapNode>

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

            try { _defaultFont = Content.Load<SpriteFont>("fonts/DefaultFont"); } catch { }

            // --- INIT MAP ---
            var nodes = MapFactory.CreateTestMap(_pixelTexture);
            _mapManager = new MapManager(nodes);
            _mapManager.PixelTexture = _pixelTexture; // Give it the texture for lines

            // --- INIT CARDS ---
            var card1 = CardFactory.CreateSoldier(_pixelTexture);
            card1.Position = new Vector2(100, 500);
            var card2 = CardFactory.CreateNoble(_pixelTexture);
            card2.Position = new Vector2(260, 500); 
            _hand.Add(card1);
            _hand.Add(card2);
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            var mouseState = Mouse.GetState();

            // Handle Map Logic (Presence, Clicking, etc)
            // We are pretending to be "PlayerColor.Red"
            _mapManager.Update(mouseState, PlayerColor.Red);

            foreach (var card in _hand)
            {
                card.Update(gameTime, mouseState);
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateBlue); 

            _spriteBatch.Begin();

            // 1. Draw Map (Manager handles nodes and lines now)
            _mapManager.Draw(_spriteBatch);

            // 2. Draw Cards
            foreach (var card in _hand)
            {
                card.Draw(_spriteBatch, _defaultFont);
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}