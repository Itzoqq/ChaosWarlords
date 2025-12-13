using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Entities;

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

        // LOGIC: Pure Hit Testing
        public bool IsMarketButtonHovered(InputManager input) => input.IsMouseOver(MarketButtonRect);
        public bool IsAssassinateButtonHovered(InputManager input) => input.IsMouseOver(AssassinateButtonRect);
        public bool IsReturnSpyButtonHovered(InputManager input) => input.IsMouseOver(ReturnSpyButtonRect);

        // Note: Drawing logic removed entirely
    }
}