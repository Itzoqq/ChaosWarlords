using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Views
{
    /// <summary>
    /// Handles the "Presentation" layer.
    /// Responsible for rendering the game state, managing animations/view models, and UI layout.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class GameplayView
    {
        private readonly GraphicsDevice _graphicsDevice;

        // --- Renderers & Assets ---
        private SpriteFont _defaultFont;
        private SpriteFont _smallFont;
        private Texture2D _pixelTexture;

        private MapRenderer _mapRenderer;
        private CardRenderer _cardRenderer;
        private UIRenderer _uiRenderer;

        // --- View Models ---
        // CHANGED: Converted to Internal Properties so Tests can access them
        internal List<CardViewModel> HandViewModels { get; private set; } = new List<CardViewModel>();
        internal List<CardViewModel> PlayedViewModels { get; private set; } = new List<CardViewModel>();

        // Market is internal just in case, but kept as a backing property style
        internal List<CardViewModel> MarketViewModels { get; private set; } = new List<CardViewModel>();

        // --- Layout State ---
        public int HandY { get; private set; }
        public int PlayedY { get; private set; }

        public GameplayView(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        public void LoadContent(ContentManager content)
        {
            _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            try { _defaultFont = content.Load<SpriteFont>("fonts/DefaultFont"); } catch { }
            try { _smallFont = content.Load<SpriteFont>("fonts/SmallFont"); } catch { }

            _uiRenderer = new UIRenderer(_graphicsDevice, _defaultFont, _smallFont);
            _mapRenderer = new MapRenderer(_pixelTexture, _pixelTexture, _defaultFont);
            _cardRenderer = new CardRenderer(_pixelTexture, _defaultFont);

            int screenH = _graphicsDevice.Viewport.Height;
            HandY = screenH - Card.Height - 20;
            PlayedY = HandY - Card.Height - 10;
        }

        public void Update(MatchContext context, InputManager inputManager, bool isMarketOpen)
        {
            SyncHandVisuals(context.ActivePlayer.Hand);
            SyncPlayedVisuals(context.ActivePlayer.PlayedCards);
            SyncMarketVisuals(context.MarketManager.MarketRow);

            UpdateVisualsHover(HandViewModels, inputManager);
            if (isMarketOpen) UpdateVisualsHover(MarketViewModels, inputManager);
        }

        public void Draw(SpriteBatch spriteBatch, MatchContext context, InputManager inputManager, UIManager uiManager, bool isMarketOpen, string targetingText)
        {
            // 1. Draw Map
            MapNode hoveredNode = context.MapManager.GetNodeAt(inputManager.MousePosition);
            Site hoveredSite = context.MapManager.GetSiteAt(inputManager.MousePosition);
            _mapRenderer.Draw(spriteBatch, context.MapManager, hoveredNode, hoveredSite);

            // 2. Draw Cards (Hand & Played)
            foreach (var vm in HandViewModels) _cardRenderer.Draw(spriteBatch, vm);
            foreach (var vm in PlayedViewModels) _cardRenderer.Draw(spriteBatch, vm);

            // 3. Draw Market Overlay
            if (isMarketOpen)
            {
                _uiRenderer.DrawMarketOverlay(spriteBatch, context.MarketManager, uiManager.ScreenWidth, uiManager.ScreenHeight);
                foreach (var vm in MarketViewModels) _cardRenderer.Draw(spriteBatch, vm);
            }

            // 4. Draw UI Elements
            _uiRenderer.DrawMarketButton(spriteBatch, uiManager);
            _uiRenderer.DrawActionButtons(spriteBatch, uiManager, context.ActivePlayer);
            _uiRenderer.DrawTopBar(spriteBatch, context.ActivePlayer, uiManager.ScreenWidth);

            // 5. Draw Indicators
            DrawTurnIndicator(spriteBatch, context.ActivePlayer);
            if (!string.IsNullOrEmpty(targetingText))
            {
                spriteBatch.DrawString(_defaultFont, targetingText, inputManager.MousePosition + new Vector2(20, 20), Color.Red);
            }

            // 6. Draw Spy Selection (if active)
            if (context.ActionSystem.CurrentState == ActionState.SelectingSpyToReturn)
            {
                DrawSpySelectionUI(spriteBatch, context.ActionSystem.PendingSite, uiManager.ScreenWidth);
            }
        }

        // --- Internal Render Logic ---

        private void SyncHandVisuals(List<Card> hand)
        {
            HandViewModels.RemoveAll(vm => !hand.Contains(vm.Model));
            foreach (var card in hand)
            {
                if (!HandViewModels.Any(vm => vm.Model == card))
                    HandViewModels.Add(new CardViewModel(card));
            }

            int cardWidth = Card.Width;
            int gap = 10;
            int totalWidth = (hand.Count * cardWidth) + ((hand.Count - 1) * gap);
            int viewportWidth = _graphicsDevice.Viewport.Width;
            int startX = (viewportWidth - totalWidth) / 2;

            var sortedVMs = new List<CardViewModel>();
            for (int i = 0; i < hand.Count; i++)
            {
                var vm = HandViewModels.FirstOrDefault(v => v.Model == hand[i]);
                if (vm != null)
                {
                    vm.Position = new Vector2(startX + (i * (cardWidth + gap)), HandY);
                    sortedVMs.Add(vm);
                }
            }
            HandViewModels = sortedVMs;
        }

        private void SyncMarketVisuals(List<Card> marketRow)
        {
            MarketViewModels.RemoveAll(vm => !marketRow.Contains(vm.Model));
            foreach (var card in marketRow)
            {
                if (!MarketViewModels.Any(vm => vm.Model == card))
                    MarketViewModels.Add(new CardViewModel(card));
            }

            int startX = 100;
            int startY = 100;
            int gap = 10;
            for (int i = 0; i < marketRow.Count; i++)
            {
                var vm = MarketViewModels.FirstOrDefault(v => v.Model == marketRow[i]);
                if (vm != null)
                {
                    vm.Position = new Vector2(startX + (i * (Card.Width + gap)), startY);
                }
            }
        }

        private void SyncPlayedVisuals(List<Card> played)
        {
            PlayedViewModels.RemoveAll(vm => !played.Contains(vm.Model));
            foreach (var card in played)
            {
                if (!PlayedViewModels.Any(vm => vm.Model == card))
                    PlayedViewModels.Add(new CardViewModel(card));
            }

            int cardWidth = Card.Width;
            int gap = 10;
            int totalWidth = (played.Count * cardWidth) + ((played.Count - 1) * gap);
            int viewportWidth = _graphicsDevice.Viewport.Width;
            int startX = (viewportWidth - totalWidth) / 2;

            for (int i = 0; i < played.Count; i++)
            {
                var vm = PlayedViewModels.FirstOrDefault(v => v.Model == played[i]);
                if (vm != null)
                {
                    vm.Position = new Vector2(startX + (i * (cardWidth + gap)), PlayedY);
                }
            }
        }

        private void UpdateVisualsHover(List<CardViewModel> vms, InputManager input)
        {
            Point mousePos = input.MousePosition.ToPoint();
            bool foundHovered = false;
            for (int i = vms.Count - 1; i >= 0; i--)
            {
                var vm = vms[i];
                if (!foundHovered && vm.Bounds.Contains(mousePos))
                {
                    vm.IsHovered = true;
                    foundHovered = true;
                }
                else
                {
                    vm.IsHovered = false;
                }
            }
        }

        private void DrawTurnIndicator(SpriteBatch sb, Player activePlayer)
        {
            Color c = activePlayer.Color == PlayerColor.Red ? Color.Red : Color.Blue;
            string text = $"-- {activePlayer.Color}'s Turn --";
            sb.DrawString(_defaultFont, text, new Vector2(20, 50), c);
        }

        private void DrawSpySelectionUI(SpriteBatch sb, Site site, int screenWidth)
        {
            if (site == null) return;

            string header = "Select Spy to Return:";
            Vector2 size = _defaultFont.MeasureString(header);
            Vector2 startPos = new Vector2((screenWidth - size.X) / 2, 200);

            sb.DrawString(_defaultFont, header, startPos, Color.White);

            int yOffset = 40;
            foreach (var spy in site.Spies)
            {
                string btnText = spy.ToString();
                Rectangle rect = new Rectangle((int)startPos.X, (int)startPos.Y + yOffset, 200, 30);

                sb.Draw(_pixelTexture, rect, Color.Gray);
                sb.DrawString(_defaultFont, btnText, new Vector2(rect.X + 10, rect.Y + 5), Color.Black);

                yOffset += 40;
            }
        }
    }
}