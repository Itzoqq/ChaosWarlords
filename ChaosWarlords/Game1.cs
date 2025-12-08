using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;

namespace ChaosWarlords
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        // Assets
        private Texture2D _pixelTexture;
        private SpriteFont _defaultFont; // We will need to create a font file later, but I'll handle null for now

        // Game State
        private List<Card> _hand = new List<Card>();

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

            // Create our "Magic Pixel" for drawing flat colors
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            // Try to load a font, but wrap in try/catch in case you haven't made one yet
            try { _defaultFont = Content.Load<SpriteFont>("fonts/DefaultFont"); } 
            catch { /* No font found yet, that's okay */ }

            // --- TEST: Create a fake "Hand" of cards ---
            var card1 = CardFactory.CreateSoldier(_pixelTexture);
            card1.Position = new Vector2(100, 400);

            var card2 = CardFactory.CreateNoble(_pixelTexture);
            card2.Position = new Vector2(260, 400); // Shifted right

            var card3 = CardFactory.CreateSoldier(_pixelTexture);
            card3.Position = new Vector2(420, 400);

            _hand.Add(card1);
            _hand.Add(card2);
            _hand.Add(card3);
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            var mouseState = Mouse.GetState();

            // Update all cards in our hand
            foreach (var card in _hand)
            {
                card.Update(gameTime, mouseState);
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateBlue); // Nicer background color

            _spriteBatch.Begin();

            // Draw all cards
            foreach (var card in _hand)
            {
                card.Draw(_spriteBatch, _defaultFont);
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}