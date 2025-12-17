using System;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class UIManager : IUISystem
    {
        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }

        // Internals (encapsulated)
        private Rectangle _marketButtonRect;
        private Rectangle _assassinateButtonRect;
        private Rectangle _returnSpyButtonRect;

        // Interface Implementation
        public event EventHandler OnMarketToggleRequest;
        public event EventHandler OnAssassinateRequest;
        public event EventHandler OnReturnSpyRequest;

        public bool IsMarketHovered { get; private set; }
        public bool IsAssassinateHovered { get; private set; }
        public bool IsReturnSpyHovered { get; private set; }

        public UIManager(int screenWidth, int screenHeight)
        {
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            RecalculateLayout();
        }

        private void RecalculateLayout()
        {
            // (Your existing layout logic here)
            int btnHeight = 100;
            int btnWidth = 40;
            int verticalGap = 25;

            _marketButtonRect = new Rectangle(0, (ScreenHeight / 2) - (btnHeight / 2), btnWidth, btnHeight);

            _assassinateButtonRect = new Rectangle(
                ScreenWidth - btnWidth,
                (ScreenHeight / 2) - btnHeight - (verticalGap / 2),
                btnWidth,
                btnHeight);

            _returnSpyButtonRect = new Rectangle(
                ScreenWidth - btnWidth,
                (ScreenHeight / 2) + (verticalGap / 2),
                btnWidth,
                btnHeight);
        }

        public void Update(InputManager input)
        {
            // 1. Update Hovers
            IsMarketHovered = input.IsMouseOver(_marketButtonRect);
            IsAssassinateHovered = input.IsMouseOver(_assassinateButtonRect);
            IsReturnSpyHovered = input.IsMouseOver(_returnSpyButtonRect);

            // 2. Handle Clicks - Fire Events!
            if (input.IsLeftMouseJustClicked())
            {
                if (IsMarketHovered) OnMarketToggleRequest?.Invoke(this, EventArgs.Empty);
                if (IsAssassinateHovered) OnAssassinateRequest?.Invoke(this, EventArgs.Empty);
                if (IsReturnSpyHovered) OnReturnSpyRequest?.Invoke(this, EventArgs.Empty);
            }
        }

        // Expose rects ONLY to the Renderer, or keep them internal and make UIManager responsible 
        // for passing data to UIRenderer. For now, we can add a getter if needed by Renderer, 
        // but Logic shouldn't touch them.
        public Rectangle MarketButtonRect => _marketButtonRect;
        public Rectangle AssassinateButtonRect => _assassinateButtonRect;
        public Rectangle ReturnSpyButtonRect => _returnSpyButtonRect;
    }
}