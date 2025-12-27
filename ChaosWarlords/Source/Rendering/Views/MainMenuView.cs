using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Core.Interfaces;
using ChaosWarlords.Source.Rendering.UI;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Rendering.Views
{
    [ExcludeFromCodeCoverage]
    public class MainMenuView : IMainMenuView
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly ContentManager _content;
        private readonly IButtonManager _buttonManager;
        
        private ButtonRenderer _buttonRenderer;
        private Texture2D _backgroundTexture;

        public MainMenuView(GraphicsDevice graphicsDevice, ContentManager content, IButtonManager buttonManager)
        {
            _graphicsDevice = graphicsDevice;
            _content = content;
            _buttonManager = buttonManager;
        }

        public void LoadContent()
        {
            // 1. Load Font with Fallback
            SpriteFont font = null;
            try
            {
                font = _content.Load<SpriteFont>("Fonts/DefaultFont");
            }
            catch
            {
                // If font fails, we can't easily create a fallback font procedurally without advanced manual bitmap generation.
                // However, ButtonRenderer might handle null font or we could try a system font if supported.
                // For now, we will suppress the crash, but buttons might be invisible text-wise.
                // Ideally, we log this.
                System.Diagnostics.Debug.WriteLine("Failed to load generic font.");
            }

            // 2. Load Background with Fallback
            try
            {
                _backgroundTexture = _content.Load<Texture2D>("Textures/Backgrounds/MainMenuBG");
            }
            catch
            {
                 // Create a placeholder 1x1 texture
                _backgroundTexture = new Texture2D(_graphicsDevice, 1, 1);
                _backgroundTexture.SetData(new[] { Color.DarkSlateGray });
            }
            
            _buttonRenderer = new ButtonRenderer(_graphicsDevice, font);
        }

        public void UnloadContent()
        {
            // View-specific cleanup if needed (e.g. dynamic textures)
        }

        public void Update(GameTime gameTime)
        {
            // View logic (animations, particles) goes here
        }

        public void Draw(SpriteBatch spriteBatch)
        {
             // Draw Background
            if (_backgroundTexture != null)
            {
                spriteBatch.Draw(_backgroundTexture, new Rectangle(0, 0, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height), Color.White);
            }

            // Draw Buttons
            _buttonRenderer?.Draw(spriteBatch, _buttonManager.GetButtons());
        }
    }
}
