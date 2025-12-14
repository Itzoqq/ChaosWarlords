using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace ChaosWarlords.Source.Views
{
    public class HandVisualsManager
    {
        private readonly Game _game;
        private readonly TurnManager _turnManager;
        private readonly InputManager _inputManager;

        // Position members are now internal properties/fields
        public int HandY { get; private set; }
        public int PlayedY { get; private set; }

        public HandVisualsManager(Game game, TurnManager turnManager, InputManager inputManager)
        {
            _game = game;
            _turnManager = turnManager;
            _inputManager = inputManager;

            // Initialize Y coordinates (Moved from GameplayState.LoadContent)
            int screenH = _game.GraphicsDevice.Viewport.Height;
            HandY = screenH - Card.Height - 20;
            PlayedY = HandY - (Card.Height / 2);
        }

        public void ArrangeHandVisuals()
        {
            if (_game == null) return;
            // Uses _turnManager.ActivePlayer for multi-player context
            var hand = _turnManager.ActivePlayer.Hand;
            int cardWidth = Card.Width;
            int gap = 10;

            int totalHandWidth = (hand.Count * cardWidth) + ((hand.Count - 1) * gap);
            int startX = (_game.GraphicsDevice.Viewport.Width - totalHandWidth) / 2;

            for (int i = 0; i < hand.Count; i++)
            {
                hand[i].Position = new Vector2(startX + (i * (cardWidth + gap)), HandY);
            }
        }

        public void UpdateHandVisuals()
        {
            Point mousePos = _inputManager.MousePosition.ToPoint();
            var hand = _turnManager.ActivePlayer.Hand;

            // We iterate backwards just like the click logic to handle overlap correctly
            bool foundHovered = false;
            for (int i = hand.Count - 1; i >= 0; i--)
            {
                var card = hand[i];
                if (!foundHovered && card.Bounds.Contains(mousePos))
                {
                    card.IsHovered = true;
                    foundHovered = true; // Only highlight the top-most card under mouse
                }
                else
                {
                    card.IsHovered = false;
                }
            }
        }
    }
}