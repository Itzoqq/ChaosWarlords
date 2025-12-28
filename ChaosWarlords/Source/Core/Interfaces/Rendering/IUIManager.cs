using ChaosWarlords.Source.Core.Interfaces.Input;
using System;
using Microsoft.Xna.Framework;


namespace ChaosWarlords.Source.Core.Interfaces.Rendering
{
    public interface IUIManager
    {
        // Layout and Draw properties
        int ScreenWidth { get; }
        int ScreenHeight { get; }

        // --- Layout Data for Rendering ---
        Rectangle MarketButtonRect { get; }
        Rectangle AssassinateButtonRect { get; }
        Rectangle ReturnSpyButtonRect { get; }
        // End Turn
        Rectangle EndTurnButtonRect { get; }
        // Popup (Modal)
        Rectangle PopupBackgroundRect { get; }
        Rectangle PopupConfirmButtonRect { get; }
        Rectangle PopupCancelButtonRect { get; }

        // Input Handling
        void Update(IInputManager input);

        // Events
        event EventHandler OnMarketToggleRequest;
        event EventHandler OnAssassinateRequest;
        event EventHandler OnReturnSpyRequest;
        // End Turn
        event EventHandler OnEndTurnRequest;
        // Popup (Modal)
        event EventHandler OnPopupConfirm;
        event EventHandler OnPopupCancel;

        // Querying state (Hovering)
        bool IsMarketHovered { get; }
        bool IsAssassinateHovered { get; }
        bool IsReturnSpyHovered { get; }
        // End Turn
        bool IsEndTurnHovered { get; }
        // Popup (Modal)
        // Popup (Modal)
        bool IsPopupConfirmHovered { get; }
        bool IsPopupCancelHovered { get; }

        // --- Pause Menu ---
        Rectangle PauseMenuBackgroundRect { get; }
        Rectangle ResumeButtonRect { get; }
        Rectangle MainMenuButtonRect { get; }
        Rectangle ExitButtonRect { get; }

        event EventHandler OnResumeRequest;
        event EventHandler OnMainMenuRequest;
        event EventHandler OnExitRequest;

        // State Control
        bool IsPaused { get; set; }
        bool IsPopupVisible { get; set; }

        bool IsResumeHovered { get; }
        bool IsMainMenuHovered { get; }
        bool IsExitHovered { get; }
    }
}



