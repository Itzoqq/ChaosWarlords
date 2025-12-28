using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Views;

using System.Diagnostics.CodeAnalysis;
using System;

namespace ChaosWarlords.Source.Rendering.Views
{
    /// <summary>
    /// Handles the "Presentation" layer.
    /// Responsible for rendering the game state, managing animations/view models, and UI layout.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class GameplayView : IGameplayView, IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;

        // --- Renderers & Assets ---
        private SpriteFont _defaultFont = null!;
        private SpriteFont _smallFont = null!;
        private Texture2D _pixelTexture = null!;

        private MapRenderer _mapRenderer = null!;
        private CardRenderer _cardRenderer = null!;
        private UIRenderer _uiRenderer = null!;

        // --- View Models ---
        // CHANGED: Public for Interface Implementation
        public List<CardViewModel> HandViewModels { get; private set; } = [];
        public List<CardViewModel> PlayedViewModels { get; private set; } = [];

        // Market is internal just in case, but kept as a backing property style
        public List<CardViewModel> MarketViewModels { get; private set; } = [];

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

        public void Draw(SpriteBatch spriteBatch, MatchContext context, InputManager inputManager, IUIManager uiManager, bool isMarketOpen, string targetingText, bool isPopupOpen, bool isPauseMenuOpen)
        {
            // 1. Draw Map
            MapNode? hoveredNode = context.MapManager.GetNodeAt(inputManager.MousePosition);
            Site? hoveredSite = context.MapManager.GetSiteAt(inputManager.MousePosition);
            _mapRenderer.Draw(spriteBatch, context.MapManager, hoveredNode, hoveredSite);

            // 2. Draw Cards (Hand & Played) - Skip hand during Setup
            if (context.CurrentPhase != MatchPhase.Setup)
            {
                foreach (var vm in HandViewModels) _cardRenderer.Draw(spriteBatch, vm);
            }
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

            // Draw End Turn Button
            bool canEndTurn = true;
            _uiRenderer.DrawHorizontalButton(spriteBatch, uiManager.EndTurnButtonRect, "END TURN", uiManager.IsEndTurnHovered, canEndTurn, Color.Green);

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

            // 7. Draw Confirmation Popup (Modal)
            if (isPopupOpen)
            {
                _uiRenderer.DrawConfirmationPopup(
                    spriteBatch,
                    "You have unplayed cards!\nEnd Turn anyway?",
                    uiManager.PopupBackgroundRect,
                    uiManager.PopupConfirmButtonRect,
                    uiManager.PopupCancelButtonRect,
                    uiManager.IsPopupConfirmHovered,
                    uiManager.IsPopupCancelHovered);
            }

            // 8. Draw Pause Menu (Top-most Modal)
            if (isPauseMenuOpen)
            {
                _uiRenderer.DrawPauseMenu(spriteBatch, uiManager);
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

            List<CardViewModel> sortedVMs = [];
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

        private static void UpdateVisualsHover(List<CardViewModel> vms, InputManager input)
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

        private void DrawSpySelectionUI(SpriteBatch sb, Site? site, int screenWidth)
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
        public void DrawSetupPhaseOverlay(SpriteBatch spriteBatch, Player activePlayer)
        {
            if (_defaultFont == null) return;

            string line1 = "INITIAL DEPLOYMENT ROUND";
            string line2 = $"Current Player: {activePlayer.Color}";

            Vector2 size1 = _defaultFont.MeasureString(line1);
            Vector2 size2 = _defaultFont.MeasureString(line2);

            int screenW = _graphicsDevice.Viewport.Width;

            // Position: Center middle of X axis, right below top bar (approx Y=60)
            // Top bar is usually small, assuming 40-50px.
            Vector2 pos1 = new Vector2((screenW - size1.X) / 2, 60);
            Vector2 pos2 = new Vector2((screenW - size2.X) / 2, 85);

            Color color = activePlayer.Color == PlayerColor.Red ? Color.Red : Color.Blue;

            // Draw a subtle background for contrast? Not strictly requested but good practice.
            // Skipping background to keep it simple as requested.

            spriteBatch.DrawString(_defaultFont, line1, pos1, Color.Yellow); // Yellow for attention on "Round"
            spriteBatch.DrawString(_defaultFont, line2, pos2, color);
        }

        public void Dispose()
        {
            _pixelTexture?.Dispose();
            _uiRenderer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}


