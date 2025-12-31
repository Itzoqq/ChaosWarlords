using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Rendering.UI;
using ChaosWarlords.Source.Core.Interfaces.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ChaosWarlords.Source.Rendering.Views
{
    public class VictoryView : IVictoryView
    {
        private readonly UIRenderer _uiRenderer;
        private readonly IButtonManager _buttonManager;
        private readonly IGameLogger _logger;
        private readonly VictoryDto _victoryData;

        // UI Constants
        public Rectangle MainMenuButtonRect { get; private set; }
        public bool IsMainMenuHovered { get; set; }

        public VictoryView(GraphicsDevice graphicsDevice, IButtonManager buttonManager, VictoryDto victoryData, IGameLogger logger)
        {
            _buttonManager = buttonManager ?? throw new ArgumentNullException(nameof(buttonManager));
            _victoryData = victoryData ?? throw new ArgumentNullException(nameof(victoryData));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize Renderer
            // NOTE: In a real DI container this would be injected, but we are manually composing for now.
             // We reuse UIRenderer logic but need to handle its requirements.
             // Currently manual composition with dummy fonts if not provided (see second ctor).
             // However, UIRenderer ctor requires valid fonts.
             // This ctor is potentially unsafe if called without content.
             // We should probably remove it or ensure it fails explicitly if called?
             // Or assume default fonts? Can't assume without Content.
            // Unused constructor for now - unsafe
            _uiRenderer = null!; 
        }

        // Revised Constructor with Content
        public VictoryView(GraphicsDevice graphicsDevice, Microsoft.Xna.Framework.Content.ContentManager content, IButtonManager buttonManager, VictoryDto victoryData, IGameLogger logger)
        {
             _buttonManager = buttonManager;
            _victoryData = victoryData;
            _logger = logger;

            // Load Fonts
            var defaultFont = content.Load<SpriteFont>("Fonts/DefaultFont");
            var smallFont = content.Load<SpriteFont>("Fonts/SmallFont"); // Assuming existence or fallback

            _uiRenderer = new UIRenderer(graphicsDevice, defaultFont, smallFont);

            // Layout
            int width = graphicsDevice.Viewport.Width;
            int height = graphicsDevice.Viewport.Height;
            int btnWidth = 200;
            int btnHeight = 50;

            MainMenuButtonRect = new Rectangle(width - btnWidth - 20, height - btnHeight - 20, btnWidth, btnHeight);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            int width = spriteBatch.GraphicsDevice.Viewport.Width;
            int height = spriteBatch.GraphicsDevice.Viewport.Height;

            // 1. Draw Full Screen Background
            // We reuse VictoryPopup logic but make it full screen
            // Or just call DrawVictoryPopup with full screen args?
            // DrawVictoryPopup draws ISOLATED popup on top of game.
            // Here we want it as the main screen.
            
            // Let's use DrawVictoryPopup logic but centralized.
            _uiRenderer.DrawVictoryPopup(spriteBatch, _victoryData, width, height);

            // 2. Draw Main Menu Button
            _uiRenderer.DrawHorizontalButton(spriteBatch, MainMenuButtonRect, "MAIN MENU", IsMainMenuHovered, true, Color.MediumPurple);
        }

        public void Dispose()
        {
            _uiRenderer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
