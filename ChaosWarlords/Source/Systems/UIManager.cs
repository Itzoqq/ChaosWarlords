using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class UIManager
    {
        // Public properties so the Renderer knows where to draw
        public Rectangle MarketButtonRect { get; private set; }
        public Rectangle AssassinateButtonRect { get; private set; }
        public Rectangle ReturnSpyButtonRect { get; private set; }

        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }

        // Note: No Texture2D or SpriteFont here anymore!

        public UIManager(int screenWidth, int screenHeight)
        {
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            RecalculateLayout();
        }

        public void RecalculateLayout()
        {
            int btnHeight = 100;
            int btnWidth = 40;
            int verticalGap = 25;

            // 1. Market (Left - Centered)
            MarketButtonRect = new Rectangle(0, (ScreenHeight / 2) - (btnHeight / 2), btnWidth, btnHeight);

            // 2. Assassinate (Right - Shifted UP)
            AssassinateButtonRect = new Rectangle(
                ScreenWidth - btnWidth,
                (ScreenHeight / 2) - btnHeight - verticalGap,
                btnWidth,
                btnHeight
            );

            // 3. Return Spy (Right - Shifted DOWN)
            ReturnSpyButtonRect = new Rectangle(
                ScreenWidth - btnWidth,
                (ScreenHeight / 2) + verticalGap,
                btnWidth,
                btnHeight
            );
        }

        public IGameCommand HandleInput(InputManager input, MarketManager market, MapManager map, Player player, ActionSystem actionSystem)
        {
            if (!input.IsLeftMouseJustClicked()) return null;
            Vector2 mousePos = input.MousePosition;

            // 1. SPY SELECTION POPUP (High Priority)
            if (actionSystem.CurrentState == ActionState.SelectingSpyToReturn)
            {
                Site site = actionSystem.PendingSite;
                if (site != null)
                {
                    var enemies = map.GetEnemySpiesAtSite(site, player);
                    // Match the distinct logic used in drawing
                    var distinctEnemies = new System.Collections.Generic.HashSet<PlayerColor>(enemies);

                    int i = 0;
                    foreach (var enemyColor in distinctEnemies)
                    {
                        // Recreate the button rect (same math as GameplayState.DrawSpySelectionUI)
                        Rectangle btnRect = new Rectangle((int)site.Bounds.X + (i * 60), (int)site.Bounds.Y - 50, 50, 40);
                        if (btnRect.Contains(mousePos))
                        {
                            return new ResolveSpyCommand(enemyColor);
                        }
                        i++;
                    }
                }
                // If clicked outside the buttons, cancel selection
                return new CancelActionCommand();
            }

            // 2. UI Buttons
            if (IsMarketButtonHovered(input)) return new ToggleMarketCommand();
            if (IsAssassinateButtonHovered(input)) return new StartAssassinateCommand();
            if (IsReturnSpyButtonHovered(input)) return new StartReturnSpyCommand();

            // 3. Market Cards
            if (market != null)
            {
                foreach (var card in market.MarketRow)
                {
                    if (card.Bounds.Contains(mousePos)) return new BuyCardCommand(card);
                }
            }

            // 4. Map Nodes (Highest priority map object)
            var node = map.GetNodeAt(mousePos);
            if (node != null) return new MapNodeClickedCommand(node);

            // 5. Map Sites (Background click for placing/returning spies)
            var siteHit = map.GetSiteAt(mousePos);
            if (siteHit != null) return new SiteClickedCommand(siteHit);

            // 6. Hand Cards
            for (int i = player.Hand.Count - 1; i >= 0; i--)
            {
                var card = player.Hand[i];
                if (card.Bounds.Contains(mousePos)) return new PlayCardCommand(card);
            }

            return null;
        }

        // LOGIC: Pure Hit Testing
        public bool IsMarketButtonHovered(InputManager input) => input.IsMouseOver(MarketButtonRect);
        public bool IsAssassinateButtonHovered(InputManager input) => input.IsMouseOver(AssassinateButtonRect);
        public bool IsReturnSpyButtonHovered(InputManager input) => input.IsMouseOver(ReturnSpyButtonRect);

        // Note: Drawing logic removed entirely
    }
}