using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Rendering.UI;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Rendering.Views
{
    [ExcludeFromCodeCoverage]
    public class VictoryView : IVictoryView
    {
        private readonly UIRenderer _uiRenderer;
        private readonly IButtonManager _buttonManager;
        private readonly IGameLogger _logger;
        private readonly VictoryDto _victoryData;

        // UI Constants
        public Rectangle MainMenuButtonRect { get; private set; }
        public bool IsMainMenuHovered { get; set; }

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
            int btnWidth = GameConstants.UILayout.DefaultButtonWidth;
            int btnHeight = GameConstants.UILayout.LargeButtonHeight;

            MainMenuButtonRect = new Rectangle(width - btnWidth - GameConstants.UILayout.LargePadding, height - btnHeight - GameConstants.UILayout.LargePadding, btnWidth, btnHeight);
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
