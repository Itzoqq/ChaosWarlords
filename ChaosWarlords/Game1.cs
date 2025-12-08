using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        // This is our "building block" for drawing shapes
        private Texture2D _pixelTexture;
        
        // Let's define a "Card" just as a position and size for now
        private Rectangle _cardRect = new Rectangle(100, 100, 150, 200);
        private Color _cardColor = Color.DarkSlateGray;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true; // Essential for a board game!
        }

        protected override void Initialize()
        {
            // Set the window size
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // MAGIC TRICK: Create a 1x1 white pixel texture in memory
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        protected override void Update(GameTime gameTime)
        {
            // Exit if Escape is pressed
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // Simple Interaction: Change color if mouse hovers over the card
            var mouseState = Mouse.GetState();
            if (_cardRect.Contains(mouseState.Position))
            {
                _cardColor = Color.Crimson; // Hover color
                
                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    _cardColor = Color.Gold; // Click color
                }
            }
            else
            {
                _cardColor = Color.DarkSlateGray; // Normal color
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin();

            // DRAWING THE CARD
            // We draw the pixel, at the location of _cardRect, tinted with _cardColor
            _spriteBatch.Draw(_pixelTexture, _cardRect, _cardColor);

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}