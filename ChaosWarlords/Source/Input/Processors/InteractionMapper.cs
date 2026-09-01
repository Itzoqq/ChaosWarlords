using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;


namespace ChaosWarlords.Source.Input
{
    /// <summary>
    /// Translates Mouse Coordinates into Game Entities.
    /// Decouples Input logic from Rendering logic.
    /// </summary>
    public class InteractionMapper : IInteractionMapper
    {
        private readonly IGameplayView _view;

        public InteractionMapper(IGameplayView view)
        {
            _view = view;
        }

        public Card? GetHoveredHandCard()
        {
            return _view.HandViewModels.FirstOrDefault(vm => vm.IsHovered)?.Model;
        }

        public Card? GetHoveredMarketCard()
        {
            return _view.MarketViewModels.FirstOrDefault(vm => vm.IsHovered)?.Model;
        }

        public Card? GetHoveredPlayedCard(IInputManager input)
        {
            // We ask the ViewModels directly instead of recalculating rectangles
            return _view.PlayedViewModels.FirstOrDefault(vm => vm.Bounds.Contains(input.MousePosition))?.Model;
        }

        public Card? GetHoveredBrowserCard()
        {
            return _view.BrowserViewModels.FirstOrDefault(vm => vm.IsHovered)?.Model;
        }

        public PlayerColor? GetClickedSpyReturnButton(Point mousePos, Site site, int screenWidth)
        {
            // This logic was previously hidden in View. 
            // Ideally, the View should expose a list of "ButtonRects", but for now, we calculate it here.
            if (site is null) return null;

            // Note: In a production refactor, we would ask the UIRenderer for these bounds 
            // to ensure Drawing and Clicking never desync.
            // For now, we replicate the logic to strip it from the Draw() method.
            Vector2 headerSize = new Vector2(200, 20); // Approximation or fetch from resource
            float drawX = (screenWidth - headerSize.X) / 2;
            int yOffset = 40;
            int startY = 200;

            foreach (var spy in site.Spies)
            {
                Rectangle rect = new Rectangle((int)drawX, startY + yOffset, 200, 30);
                if (rect.Contains(mousePos)) return spy;
                yOffset += 40;
            }
            return null;
        }

        public PlayerColor? GetClickedOpponentSelectButton(Point mousePos, IReadOnlyList<Player> allPlayers, Player activePlayer, int eligibilityThreshold, int screenWidth)
        {
            // Mirrors GetClickedSpyReturnButton's geometry exactly - see
            // DrawOpponentSelectionUI, which MUST stay in sync with this rectangle math (same
            // caveat this codebase's spy-return equivalent already flags).
            Vector2 headerSize = new Vector2(200, 20);
            float drawX = (screenWidth - headerSize.X) / 2;
            int yOffset = 40;
            int startY = 200;

            foreach (var player in allPlayers)
            {
                if (player == activePlayer) continue;

                Rectangle rect = new Rectangle((int)drawX, startY + yOffset, 200, 30);
                if (rect.Contains(mousePos) && player.Hand.Count > eligibilityThreshold)
                {
                    return player.Color;
                }
                yOffset += 40;
            }
            return null;
        }
    }
}


