using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;
using System.Linq;


namespace ChaosWarlords.Source.Managers
{
    public class UIManager : IUIManager
    {
        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }

        // State Control
        public bool IsPaused { get; set; }
        public bool IsPopupVisible { get; set; }

        // Internals (encapsulated)
        private class InteractiveElement
        {
            public required Func<Rectangle> GetBounds { get; set; }
            public required Action<bool> SetHover { get; set; }
            public required Action OnClick { get; set; }
            public Func<bool> IsActive { get; set; } = () => true; // Default to always active
        }

        private List<InteractiveElement> _elements = null!;

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
        public event EventHandler? OnMarketToggleRequest;
        public event EventHandler? OnAssassinateRequest;
        public event EventHandler? OnReturnSpyRequest;
        public event EventHandler? OnEndTurnRequest;
        public event EventHandler? OnPopupConfirm;
        public event EventHandler? OnPopupCancel;

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
            InitializeInteractiveElements();
        }

        private void InitializeInteractiveElements()
        {
            _elements =
            [
                // Pause Menu (Highest Priority)
                new InteractiveElement
                {
                    GetBounds = () => _resumeButtonRect,
                    SetHover = (v) => IsResumeHovered = v,
                    OnClick = () => { GameLogger.Log("UI: Resume Clicked", LogChannel.Info); OnResumeRequest?.Invoke(this, EventArgs.Empty); },
                    IsActive = () => IsPaused
                },
                new InteractiveElement
                {
                    GetBounds = () => _mainMenuButtonRect,
                    SetHover = (v) => IsMainMenuHovered = v,
                    OnClick = () => { GameLogger.Log("UI: MainMenu Clicked", LogChannel.Info); OnMainMenuRequest?.Invoke(this, EventArgs.Empty); },
                    IsActive = () => IsPaused
                },
                new InteractiveElement
                {
                    GetBounds = () => _exitButtonRect,
                    SetHover = (v) => IsExitHovered = v,
                    OnClick = () => { GameLogger.Log("UI: Exit Clicked", LogChannel.Info); OnExitRequest?.Invoke(this, EventArgs.Empty); },
                    IsActive = () => IsPaused
                },

                // Popups (High Priority)
                new InteractiveElement
                {
                    GetBounds = () => _popupConfirmButtonRect,
                    SetHover = (v) => IsPopupConfirmHovered = v,
                    OnClick = () => { GameLogger.Log("UI: Popup Confirm Clicked", LogChannel.Info); OnPopupConfirm?.Invoke(this, EventArgs.Empty); },
                    IsActive = () => IsPopupVisible
                },
                new InteractiveElement
                {
                    GetBounds = () => _popupCancelButtonRect,
                    SetHover = (v) => IsPopupCancelHovered = v,
                    OnClick = () => { GameLogger.Log("UI: Popup Cancel Clicked", LogChannel.Info); OnPopupCancel?.Invoke(this, EventArgs.Empty); },
                    IsActive = () => IsPopupVisible
                },

                // Main Game UI (Lowest Priority)
                new InteractiveElement
                {
                    GetBounds = () => _marketButtonRect,
                    SetHover = (v) => IsMarketHovered = v,
                    OnClick = () => { GameLogger.Log("UI: Market Clicked", LogChannel.Info); OnMarketToggleRequest?.Invoke(this, EventArgs.Empty); },
                    IsActive = () => !IsPaused && !IsPopupVisible
                },
                new InteractiveElement
                {
                    GetBounds = () => _assassinateButtonRect,
                    SetHover = (v) => IsAssassinateHovered = v,
                    OnClick = () => { GameLogger.Log("UI: Assassinate Clicked", LogChannel.Info); OnAssassinateRequest?.Invoke(this, EventArgs.Empty); },
                    IsActive = () => !IsPaused && !IsPopupVisible
                },
                new InteractiveElement
                {
                    GetBounds = () => _returnSpyButtonRect,
                    SetHover = (v) => IsReturnSpyHovered = v,
                    OnClick = () => { GameLogger.Log("UI: ReturnSpy Clicked", LogChannel.Info); OnReturnSpyRequest?.Invoke(this, EventArgs.Empty); },
                    IsActive = () => !IsPaused && !IsPopupVisible
                },
                new InteractiveElement
                {
                    GetBounds = () => _endTurnButtonRect,
                    SetHover = (v) => IsEndTurnHovered = v,
                    OnClick = () => { GameLogger.Log("UI: EndTurn Clicked", LogChannel.Info); OnEndTurnRequest?.Invoke(this, EventArgs.Empty); },
                    IsActive = () => !IsPaused && !IsPopupVisible
                },
            ];
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

        public event EventHandler? OnResumeRequest;
        public event EventHandler? OnMainMenuRequest;
        public event EventHandler? OnExitRequest;

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
        public void Update(IInputManager input)
        {
            UpdateHovers(input);
            HandleClicks(input);
        }

        private void UpdateHovers(IInputManager input)
        {
            foreach (var element in _elements)
            {
                // Optimized check: only do bounds check if active
                if (element.IsActive())
                {
                    bool isOver = input.IsMouseOver(element.GetBounds());
                    element.SetHover(isOver);
                }
                else
                {
                    // Ensure state is cleared if inactive
                    element.SetHover(false);
                }
            }
        }

        private void HandleClicks(IInputManager input)
        {
            if (!input.IsLeftMouseJustClicked()) return;

            var clickedElement = _elements.FirstOrDefault(e => e.IsActive() && input.IsMouseOver(e.GetBounds()));
            clickedElement?.OnClick?.Invoke();
        }
    }
}
