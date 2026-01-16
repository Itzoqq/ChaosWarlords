using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Rendering.UI
{
    /// <summary>
    /// A reusable UI component for browsing card collections (Inner Circle, Void, Discard Pile).
    /// Renders cards in a grid layout overlaying the screen.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class CardCollectionBrowser
    {
        private List<CardViewModel> _activeViewModels = new List<CardViewModel>();
        private bool _isVisible;
        private string _title = "Card Collection";
        private Point _mousePosition;

        // Layout Constants
        private const int CardSpacing = 20;
        private const int TopMargin = 100;
        private const int SideMargin = 100;

        public bool IsVisible => _isVisible;

        /// <summary>
        /// Access to currently displayed ViewModels for the InteractionMapper.
        /// </summary>
        public List<CardViewModel> ViewModels => _activeViewModels;

        public void Show(IEnumerable<Card> cards, string title)
        {
            _activeViewModels.Clear();
            _title = title;
            foreach (var card in cards)
            {
                _activeViewModels.Add(new CardViewModel(card));
            }
            _isVisible = true;

            // Recalculate layout immediately so positions are valid before first draw
            // We need screen dimensions, but we can assume standard 1920x1080 for initial layout or wait for Draw/Update
            // Better to defer layout to Draw/Update where ScreenWidth is available.
        }

        public void Hide()
        {
            _isVisible = false;
            _activeViewModels.Clear();
        }

        public void Update(Point mousePosition)
        {
            if (!_isVisible) return;
            _mousePosition = mousePosition;

            // Update Hover States
            foreach (var vm in _activeViewModels)
            {
                vm.IsHovered = vm.Contains(mousePosition);
            }
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D whitePixel, int screenWidth, int screenHeight, World.CardRenderer cardRenderer)
        {
            if (!_isVisible) return;

            // 1. Draw Full Screen Overlay (Semi-transparent black)
            // Pooled rectangle and vector for overlay and title (0 allocations)
            using var overlay = PooledRectangle.Rent(0, 0, screenWidth, screenHeight);
            spriteBatch.Draw(whitePixel, overlay.Value, Color.Black * 0.8f);

            // 2. Draw Title
            Vector2 titleSize = font.MeasureString(_title);
            using var titlePos = PooledVector2.Rent((screenWidth - titleSize.X) / 2, 40);
            spriteBatch.DrawString(font, _title, titlePos.Value, Color.Gold);

            // 3. Calculate Grid Layout
            int cardsPerRow = (screenWidth - (SideMargin * 2)) / (Card.Width + CardSpacing);
            if (cardsPerRow < 1) cardsPerRow = 1;

            int startX = SideMargin;
            int startY = TopMargin;

            // Pool vector outside loop for card positioning (0 allocations)
            using var cardPos = PooledVector2.Rent(0, 0);

            for (int i = 0; i < _activeViewModels.Count; i++)
            {
                var vm = _activeViewModels[i];

                int row = i / cardsPerRow;
                int col = i % cardsPerRow;

                int x = startX + (col * (Card.Width + CardSpacing));
                int y = startY + (row * (Card.Height + CardSpacing));

                cardPos.Value = new Vector2(x, y);
                vm.Position = cardPos.Value;

                // 4. Draw Card
                cardRenderer.Draw(spriteBatch, vm);
            }
        }
    }
}
