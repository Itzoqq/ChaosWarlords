using System;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Source.Systems
{
    public interface IUISystem
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
        void Update(InputManager input);

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
        bool IsPopupConfirmHovered { get; }
        bool IsPopupCancelHovered { get; }
    }
}