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
        private Rectangle _endTurnButtonRect;

        public Rectangle MarketButtonRect => _marketButtonRect;
        public Rectangle AssassinateButtonRect => _assassinateButtonRect;
        public Rectangle ReturnSpyButtonRect => _returnSpyButtonRect;
        public Rectangle EndTurnButtonRect => _endTurnButtonRect;

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
            RecalculatePauseMenuLayout();
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

        public Rectangle PopupBackgroundRect => _popupBackgroundRect;
        public Rectangle PopupConfirmButtonRect => _popupConfirmButtonRect;
        public Rectangle PopupCancelButtonRect => _popupCancelButtonRect;

        // --- Pause Menu Implementation ---
        private Rectangle _pauseMenuBackgroundRect;
        private Rectangle _resumeButtonRect;
        private Rectangle _mainMenuButtonRect;
        private Rectangle _exitButtonRect;

        public Rectangle PauseMenuBackgroundRect => _pauseMenuBackgroundRect;
        public Rectangle ResumeButtonRect => _resumeButtonRect;
        public Rectangle MainMenuButtonRect => _mainMenuButtonRect;
        public Rectangle ExitButtonRect => _exitButtonRect;

        public event EventHandler OnResumeRequest;
        public event EventHandler OnMainMenuRequest;
        public event EventHandler OnExitRequest;

        public bool IsResumeHovered { get; private set; }
        public bool IsMainMenuHovered { get; private set; }
        public bool IsExitHovered { get; private set; }

        private void RecalculatePauseMenuLayout()
        {
            int menuW = 300;
            int menuH = 400;
            int btnW = 200;
            int btnH = 50;
            int gap = 30;

            _pauseMenuBackgroundRect = new Rectangle(
                (ScreenWidth - menuW) / 2,
                (ScreenHeight - menuH) / 2,
                menuW,
                menuH);

            int startY = _pauseMenuBackgroundRect.Y + 80; // Offset for title

            _resumeButtonRect = new Rectangle(
                (ScreenWidth - btnW) / 2,
                startY,
                btnW,
                btnH);

            _mainMenuButtonRect = new Rectangle(
                (ScreenWidth - btnW) / 2,
                startY + btnH + gap,
                btnW,
                btnH);

            _exitButtonRect = new Rectangle(
                (ScreenWidth - btnW) / 2,
                startY + (btnH + gap) * 2,
                btnW,
                btnH);
        }

        // Updated Update Loop to Check Pause Menu
        public void Update(InputManager input)
        {
            UpdateHovers(input);
            HandleClicks(input);
        }

        private void UpdateHovers(InputManager input)
        {
            // 1. Update Hovers
            IsMarketHovered = input.IsMouseOver(_marketButtonRect);
            IsAssassinateHovered = input.IsMouseOver(_assassinateButtonRect);
            IsReturnSpyHovered = input.IsMouseOver(_returnSpyButtonRect);
            IsEndTurnHovered = input.IsMouseOver(_endTurnButtonRect);

            IsPopupConfirmHovered = input.IsMouseOver(_popupConfirmButtonRect);
            IsPopupCancelHovered = input.IsMouseOver(_popupCancelButtonRect);

            // Pause Menu Hovers
            IsResumeHovered = input.IsMouseOver(_resumeButtonRect);
            IsMainMenuHovered = input.IsMouseOver(_mainMenuButtonRect);
            IsExitHovered = input.IsMouseOver(_exitButtonRect);
        }

        private void HandleClicks(InputManager input)
        {
            // 2. Handle Clicks - Fire Events!
            if (input.IsLeftMouseJustClicked())
            {
                // NOTE: The caller (GameplayState) is responsible for gating these checks
                // based on what is visible (Popup vs Pause vs Game). 
                // However, if we click a pause button, we should fire the event regardless, 
                // and the listener decides if it cares.
                
                if (IsResumeHovered) { GameLogger.Log("UI: Resume Clicked", LogChannel.Info); OnResumeRequest?.Invoke(this, EventArgs.Empty); return; }
                if (IsMainMenuHovered) { GameLogger.Log("UI: MainMenu Clicked", LogChannel.Info); OnMainMenuRequest?.Invoke(this, EventArgs.Empty); return; }
                if (IsExitHovered) { GameLogger.Log("UI: Exit Clicked", LogChannel.Info); OnExitRequest?.Invoke(this, EventArgs.Empty); return; }

                if (IsPopupConfirmHovered) { GameLogger.Log("UI: Popup Confirm Clicked", LogChannel.Info); OnPopupConfirm?.Invoke(this, EventArgs.Empty); return; } 
                if (IsPopupCancelHovered) { GameLogger.Log("UI: Popup Cancel Clicked", LogChannel.Info); OnPopupCancel?.Invoke(this, EventArgs.Empty); return; }

                if (IsMarketHovered) { GameLogger.Log("UI: Market Clicked", LogChannel.Info); OnMarketToggleRequest?.Invoke(this, EventArgs.Empty); }
                if (IsAssassinateHovered) { GameLogger.Log("UI: Assassinate Clicked", LogChannel.Info); OnAssassinateRequest?.Invoke(this, EventArgs.Empty); }
                if (IsReturnSpyHovered) { GameLogger.Log("UI: ReturnSpy Clicked", LogChannel.Info); OnReturnSpyRequest?.Invoke(this, EventArgs.Empty); }
                if (IsEndTurnHovered) { GameLogger.Log("UI: EndTurn Clicked", LogChannel.Info); OnEndTurnRequest?.Invoke(this, EventArgs.Empty); }
            }
        }
    }
}