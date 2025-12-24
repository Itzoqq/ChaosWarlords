using System;
using Microsoft.Xna.Framework;

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
        private Rectangle _endTurnButtonRect;

        // Popup
        private Rectangle _popupBackgroundRect;
        private Rectangle _popupConfirmButtonRect;
        private Rectangle _popupCancelButtonRect;

        // Interface Implementation
        public event EventHandler OnMarketToggleRequest;
        public event EventHandler OnAssassinateRequest;
        public event EventHandler OnReturnSpyRequest;
        public event EventHandler OnEndTurnRequest;
        public event EventHandler OnPopupConfirm;
        public event EventHandler OnPopupCancel;

        public bool IsMarketHovered { get; private set; }
        public bool IsAssassinateHovered { get; private set; }
        public bool IsReturnSpyHovered { get; private set; }
        public bool IsEndTurnHovered { get; private set; }
        
        public bool IsPopupConfirmHovered { get; private set; }
        public bool IsPopupCancelHovered { get; private set; }

        public UIManager(int screenWidth, int screenHeight)
        {
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            RecalculateLayout();
        }

        private void RecalculateLayout()
        {
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

            _endTurnButtonRect = new Rectangle(
                ScreenWidth - 150,
                ScreenHeight - 60,
                120, 
                40);

            // Popup Layout (Centered)
            int popupW = 400;
            int popupH = 200;
            _popupBackgroundRect = new Rectangle((ScreenWidth - popupW) / 2, (ScreenHeight - popupH) / 2, popupW, popupH);

            int pBtnW = 100;
            int pBtnH = 30;
            int pGap = 40;
            int pBtnY = _popupBackgroundRect.Y + popupH - pBtnH - 20;

            _popupConfirmButtonRect = new Rectangle(_popupBackgroundRect.X + (_popupBackgroundRect.Width / 2) - pBtnW - (pGap / 2), pBtnY, pBtnW, pBtnH);
            _popupCancelButtonRect = new Rectangle(_popupBackgroundRect.X + (_popupBackgroundRect.Width / 2) + (pGap / 2), pBtnY, pBtnW, pBtnH);
        }

        public void Update(InputManager input)
        {
            // 1. Update Hovers
            IsMarketHovered = input.IsMouseOver(_marketButtonRect);
            IsAssassinateHovered = input.IsMouseOver(_assassinateButtonRect);
            IsReturnSpyHovered = input.IsMouseOver(_returnSpyButtonRect);
            IsEndTurnHovered = input.IsMouseOver(_endTurnButtonRect);

            // Note: GameplayState controls if Popup is visible. 
            // If we want UIManager to handle popup input, we need to know if it's open.
            // For now, checks are always running, but events only fire if clicked.
            IsPopupConfirmHovered = input.IsMouseOver(_popupConfirmButtonRect);
            IsPopupCancelHovered = input.IsMouseOver(_popupCancelButtonRect);

            // 2. Handle Clicks - Fire Events!
            if (input.IsLeftMouseJustClicked())
            {
                if (IsPopupConfirmHovered) { OnPopupConfirm?.Invoke(this, EventArgs.Empty); return; } // Prioritize Popup
                if (IsPopupCancelHovered) { OnPopupCancel?.Invoke(this, EventArgs.Empty); return; }

                if (IsMarketHovered) OnMarketToggleRequest?.Invoke(this, EventArgs.Empty);
                if (IsAssassinateHovered) OnAssassinateRequest?.Invoke(this, EventArgs.Empty);
                if (IsReturnSpyHovered) OnReturnSpyRequest?.Invoke(this, EventArgs.Empty);
                if (IsEndTurnHovered) OnEndTurnRequest?.Invoke(this, EventArgs.Empty);
            }
        }

        // Expose rects ONLY to the Renderer, or keep them internal and make UIManager responsible 
        // for passing data to UIRenderer. For now, we can add a getter if needed by Renderer, 
        // but Logic shouldn't touch them.
        public Rectangle MarketButtonRect => _marketButtonRect;
        public Rectangle AssassinateButtonRect => _assassinateButtonRect;
        public Rectangle ReturnSpyButtonRect => _returnSpyButtonRect;
        public Rectangle EndTurnButtonRect => _endTurnButtonRect;
        public Rectangle PopupBackgroundRect => _popupBackgroundRect;
        public Rectangle PopupConfirmButtonRect => _popupConfirmButtonRect;
        public Rectangle PopupCancelButtonRect => _popupCancelButtonRect;
    }
}