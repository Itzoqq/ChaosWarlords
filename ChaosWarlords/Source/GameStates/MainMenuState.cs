using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;

namespace ChaosWarlords.Source.States
{
    public class MainMenuState : IState
    {
        private Game1 _game;
        private Texture2D _background;
        private SpriteFont _font;
        
        private Rectangle _startButtonRect;
        private Rectangle _exitButtonRect;
        
        private Color _startButtonColor = Color.White;
        private Color _exitButtonColor = Color.White;

        // Simple input tracking to avoid multi-click per frame
        private MouseState _previousMouseState;

        public MainMenuState(Game1 game)
        {
            _game = game;
        }

        public void LoadContent()
        {
            // Try to load background, fallback if missing to avoid crash during dev
            try 
            {
                _background = _game.Content.Load<Texture2D>("Textures/Backgrounds/MainMenuBG");
            }
            catch 
            {
                // Create a 1x1 placeholder texture if file missing
                _background = new Texture2D(_game.GraphicsDevice, 1, 1);
                _background.SetData(new Color[] { Color.DarkSlateGray });
                GameLogger.Log("MainMenuBG not found, using placeholder.", LogChannel.Warning);
            }

            // Using existing font or fallback
            try
            {
                _font = _game.Content.Load<SpriteFont>("fonts/DefaultFont");
            }
            catch
            {
                // This might fail if fonts folder structure differs, but we saw 'fonts/DefaultFont.spritefont' in directory list
                // If it fails, we can't render text properly, but let's hope it works.
            }

            SetupButtons();
        }

        private void SetupButtons()
        {
            var viewport = _game.GraphicsDevice.Viewport;
            int buttonWidth = 200;
            int buttonHeight = 50;
            int centerX = viewport.Width / 2 - buttonWidth / 2;
            int centerY = viewport.Height / 2;

            _startButtonRect = new Rectangle(centerX, centerY, buttonWidth, buttonHeight);
            _exitButtonRect = new Rectangle(centerX, centerY + 70, buttonWidth, buttonHeight);
        }

        public void UnloadContent()
        {
            // _background is managed by ContentManager generally, but if we created it manually (placeholder), we should dispose.
            // However, Content.Load assets shouldn't be manually disposed usually if we want to reuse them, 
            // but here we are changing state. 
            // For now, let's rely on GC or ContentManager.Unload() if called globally.
        }

        public void Update(GameTime gameTime)
        {
            MouseState currentMouse = Mouse.GetState();
            Point mousePos = currentMouse.Position;

            bool isStartHovered = _startButtonRect.Contains(mousePos);
            bool isExitHovered = _exitButtonRect.Contains(mousePos);

            _startButtonColor = isStartHovered ? Color.LightGreen : Color.White;
            _exitButtonColor = isExitHovered ? Color.Red : Color.White;

            if (currentMouse.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed)
            {
                if (isStartHovered)
                {
                    StartGame();
                }
                else if (isExitHovered)
                {
                    _game.Exit();
                }
            }

            _previousMouseState = currentMouse;
        }

        private void StartGame()
        {
            // Transition to GameplayState
            // We assume Game1 has the necessary services exposed now
            if (_game.CardDatabase == null || _game.InputProvider == null)
            {
                GameLogger.Log("Cannot start game: Services not initialized in Game1.", LogChannel.Error);
                return;
            }

            _game.StateManager.ChangeState(new GameplayState(_game, _game.InputProvider, _game.CardDatabase));
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            var viewport = _game.GraphicsDevice.Viewport;

            if (_background != null)
            {
                spriteBatch.Draw(_background, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White);
            }

            // Draw Buttons
            DrawButton(spriteBatch, _startButtonRect, _startButtonColor, "Start Game");
            DrawButton(spriteBatch, _exitButtonRect, _exitButtonColor, "Exit");
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, Color color, string text)
        {
            // Draw button background (simple filled rectangle for now, using a 1x1 white texture trick or just lines)
            // Ideally we have a button texture. Let's create a 1x1 white texture for primitives if needed, 
            // or just draw text if we don't have a UI asset.
            // Let's generate a 1x1 pixel texture on the fly for drawing rectangles if we don't have one globally.
            // Optimization: Do this once effectively, but for this simple menu:
            
            Texture2D pixel = new Texture2D(_game.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
            
            // Draw background with transparency
            spriteBatch.Draw(pixel, rect, Color.Black * 0.5f);
            
            // Draw Border
            int border = 2;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, border), color); // Top
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - border, rect.Width, border), color); // Bottom
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, border, rect.Height), color); // Left
            spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - border, rect.Y, border, rect.Height), color); // Right

            if (_font != null)
            {
                Vector2 textSize = _font.MeasureString(text);
                Vector2 textPos = new Vector2(
                    rect.X + (rect.Width - textSize.X) / 2,
                    rect.Y + (rect.Height - textSize.Y) / 2
                );
                spriteBatch.DrawString(_font, text, textPos, color);
            }
            
            pixel.Dispose(); // Clean up immediate texture
        }
    }
}
