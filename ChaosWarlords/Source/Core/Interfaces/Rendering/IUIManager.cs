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
        // "I'm done" - explicit stop for a repeat-capable "up to N" effect (CardEffect.
        // AllowPartialRepeat, e.g. Council Member's "Move up to 2 enemy troops"). Alongside
        // right-click, not replacing it - see planning.txt's Phase 2 writeup.
        Rectangle DeclineRepeatButtonRect { get; }
        // Popup (Modal)
        Rectangle PopupBackgroundRect { get; }
        Rectangle PopupConfirmButtonRect { get; }
        Rectangle PopupCancelButtonRect { get; }

        // Input Handling
        void BindInputManager(IInputManager input);
        void Update(IInputManager input);

        // Events
        event EventHandler OnMarketToggleRequest;
        event EventHandler OnAssassinateRequest;
        event EventHandler OnReturnSpyRequest;
        // End Turn
        event EventHandler OnEndTurnRequest;
        event EventHandler OnDeclineRepeatRequest;
        // Popup (Modal)
        event EventHandler OnPopupConfirm;
        event EventHandler OnPopupCancel;

        // Querying state (Hovering)
        bool IsMarketHovered { get; }
        bool IsAssassinateHovered { get; }
        bool IsReturnSpyHovered { get; }
        // End Turn
        bool IsEndTurnHovered { get; }
        bool IsDeclineRepeatHovered { get; }
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

        /// <summary>
        /// True only while the simple yes/no "Confirm End Turn"-style popup is open - NOT
        /// while the optional-effect popup is open. Gates the generic PopupConfirmButtonRect/
        /// PopupCancelButtonRect (see UIManager.InitializeInteractiveElements). The optional-
        /// effect popup has its own dedicated Yes/No buttons and click handling
        /// (OptionalEffectPopup.HandleClick, routed via PlayerController) with screen bounds
        /// that happen to overlap this generic popup's buttons; gating these buttons on the
        /// combined IsPopupVisible let a single click fire both handlers and double-invoke
        /// optional-effect accept/decline (see planning.txt).
        /// </summary>
        bool IsConfirmationPopupVisible { get; set; }

        /// <summary>
        /// True whenever ActionSystem.IsTargeting() is true (synced each frame by
        /// UIEventMediator.Update(), same pattern as IsPaused/IsPopupVisible). Gates the
        /// Market/Assassinate/ReturnSpy/EndTurn buttons - without this, they stayed clickable
        /// throughout an unrelated in-progress targeting sequence, letting a click silently
        /// strand or permanently desync it instead of being rejected outright. See planning.txt.
        /// </summary>
        bool IsTargeting { get; set; }

        /// <summary>
        /// True whenever ActionSystem.CurrentEffect?.SourceEffect?.AllowPartialRepeat is true
        /// (synced each frame by UIEventMediator.Update()) - i.e. anywhere inside an "up to N"
        /// effect's targeting flow, at ANY sub-state, not just the boundary. Gates BOTH whether
        /// DeclineRepeatButtonRect is drawn at all (GameplayView) and whether it's clickable
        /// (UIManager.IsActive) - unlike the always-visible-but-sometimes-disabled Market/
        /// Assassinate/ReturnSpy/EndTurn buttons, this one only exists to begin with while a
        /// repeat-capable effect is actually in progress. Self-clears for free once the effect
        /// actually finishes (declined, or RemainingRepeats naturally exhausted) - no extra
        /// bookkeeping needed. See planning.txt's Phase 2 writeup.
        /// </summary>
        bool IsRepeatDeclinable { get; set; }

        bool IsResumeHovered { get; }
        bool IsMainMenuHovered { get; }
        bool IsExitHovered { get; }
        
        // Triggers
        void TriggerPopupConfirm();
    }
}



