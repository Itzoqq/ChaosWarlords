using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;

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

        public IGameCommand HandleInput(InputManager input, IMarketManager market, IMapManager map, Player player, IActionSystem actionSystem)
        {
            if (!input.IsLeftMouseJustClicked()) return null;
            Vector2 mousePos = input.MousePosition;

            // Priority 1: Spy Selection Popup (State-Specific Logic)
            if (actionSystem.CurrentState == ActionState.SelectingSpyToReturn)
            {
                return GetSpySelectionCommand(mousePos, map, player, actionSystem.PendingSite);
            }

            // Priority 2: Fixed UI Buttons
            IGameCommand command = GetFixedUIButtonCommand(input);
            if (command != null) return command;

            // Priority 3: Market Cards
            command = GetMarketCardCommand(mousePos, market);
            if (command != null) return command;

            // Priority 4: Map Nodes (Highest priority map object)
            command = GetMapNodeCommand(mousePos, map);
            if (command != null) return command;

            // Priority 5: Map Sites (Background click for placing/returning spies)
            command = GetMapSiteCommand(mousePos, map);
            if (command != null) return command;

            // Priority 6: Hand Cards
            command = GetHandCardCommand(mousePos, player);
            if (command != null) return command;

            return null;
        }

        // ----------------------------------------------------------------
        // NEW PRIVATE HELPER METHODS (Low Complexity, Single Responsibility)
        // ----------------------------------------------------------------

        // Handles Priority 1: Spy Selection Popup
        private IGameCommand GetSpySelectionCommand(Vector2 mousePos, IMapManager map, Player player, Site pendingSite)
        {
            if (pendingSite == null) return new CancelActionCommand();

            // The original logic is now encapsulated here.
            var enemies = map.GetEnemySpiesAtSite(pendingSite, player);
            var distinctEnemies = new HashSet<PlayerColor>(enemies);

            int i = 0;
            foreach (var enemyColor in distinctEnemies)
            {
                // Recreate the button rect (same math as GameplayState.DrawSpySelectionUI)
                Rectangle btnRect = new Rectangle((int)pendingSite.Bounds.X + (i * 60), (int)pendingSite.Bounds.Y - 50, 50, 40);
                if (btnRect.Contains(mousePos))
                {
                    return new ResolveSpyCommand(enemyColor);
                }
                i++;
            }

            // If clicked outside the buttons, cancel selection
            return new CancelActionCommand();
        }

        // Handles Priority 2: Fixed UI Buttons
        private IGameCommand GetFixedUIButtonCommand(InputManager input)
        {
            if (IsMarketButtonHovered(input)) return new ToggleMarketCommand();
            if (IsAssassinateButtonHovered(input)) return new StartAssassinateCommand();
            if (IsReturnSpyButtonHovered(input)) return new StartReturnSpyCommand();
            return null;
        }

        // Handles Priority 3: Market Cards
        private IGameCommand GetMarketCardCommand(Vector2 mousePos, IMarketManager market)
        {
            if (market == null) return null;

            foreach (var card in market.MarketRow)
            {
                if (card.Bounds.Contains(mousePos)) return new BuyCardCommand(card);
            }
            return null;
        }

        // Handles Priority 4: Map Nodes
        private IGameCommand GetMapNodeCommand(Vector2 mousePos, IMapManager map)
        {
            var node = map.GetNodeAt(mousePos);
            if (node != null) return new MapNodeClickedCommand(node);
            return null;
        }

        // Handles Priority 5: Map Sites
        private IGameCommand GetMapSiteCommand(Vector2 mousePos, IMapManager map)
        {
            var siteHit = map.GetSiteAt(mousePos);
            if (siteHit != null) return new SiteClickedCommand(siteHit);
            return null;
        }

        // Handles Priority 6: Hand Cards
        private IGameCommand GetHandCardCommand(Vector2 mousePos, Player player)
        {
            // Iterate backwards (higher Z-order/top-most card first)
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