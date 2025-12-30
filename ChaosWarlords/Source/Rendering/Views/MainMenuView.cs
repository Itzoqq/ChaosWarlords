using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Rendering.UI;
using System.Diagnostics.CodeAnalysis;
using ChaosWarlords.Source.Utilities;
using System;

using ChaosWarlords.Source.Core.Interfaces.Services;
using System;

namespace ChaosWarlords.Source.Rendering.Views
{
    [ExcludeFromCodeCoverage]
    public class MainMenuView : IMainMenuView, IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly ContentManager _content;
        private readonly IButtonManager _buttonManager;
        private readonly IGameLogger _logger;

        private ButtonRenderer? _buttonRenderer;
        private Texture2D _backgroundTexture = null!;
        private bool _isBackgroundManual;

        public MainMenuView(GraphicsDevice graphicsDevice, ContentManager content, IButtonManager buttonManager, IGameLogger logger)
        {
            _graphicsDevice = graphicsDevice;
            _content = content;
            _buttonManager = buttonManager;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void LoadContent()
        {
            // 1. Load Font with Fallback
            SpriteFont? font = null;
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
                _logger.Log("Failed to load generic font.", LogChannel.Error);
            }

            // 2. Load Background with Fallback
            try
            {
                _backgroundTexture = _content.Load<Texture2D>("Textures/Backgrounds/MainMenuBG");
                _isBackgroundManual = false;
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to load background: {ex.Message}. Using fallback.", LogChannel.Warning);
                // Create a placeholder 1x1 texture
                _backgroundTexture = new Texture2D(_graphicsDevice, 1, 1);
                _backgroundTexture.SetData(new[] { Color.DarkSlateGray });
                _isBackgroundManual = true;
            }

            if (font is not null)
            {
                _buttonRenderer = new ButtonRenderer(_graphicsDevice, font);
            }
        }

        public void UnloadContent()
        {
            // View-specific cleanup if needed (e.g. dynamic textures)
            ((IDisposable)this).Dispose();
        }

        public void Dispose()
        {
             if (_isBackgroundManual) _backgroundTexture?.Dispose();
             _buttonRenderer?.Dispose();
             GC.SuppressFinalize(this);
        }

        public void Update(GameTime gameTime)
        {
            // View logic (animations, particles) goes here
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Draw Background
            if (_backgroundTexture is not null)
            {
                spriteBatch.Draw(_backgroundTexture, new Rectangle(0, 0, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height), Color.White);
            }

            // Draw Buttons
            _buttonRenderer?.Draw(spriteBatch, _buttonManager.GetButtons());
        }
    }
}


