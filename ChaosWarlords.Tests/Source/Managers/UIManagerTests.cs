using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using NSubstitute;
using System;

namespace ChaosWarlords.Tests.Source.Managers
{
    [TestClass]
    [TestCategory("Unit")]
    public class UIManagerTests
    {
        private IGameLogger _logger = null!;
        private UIManager _uiManager = null!;
        private IInputManager _mockInput = null!;

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<IGameLogger>();
            _mockInput = Substitute.For<IInputManager>();
            
            // Screen 800x600 for deterministic coordinates
            _uiManager = new UIManager(800, 600, _logger);
            _uiManager.BindInputManager(_mockInput);
        }

        [TestMethod]
        public void HandleInputEvent_Ignores_NonLeftClick()
        {
            bool eventFired = false;
            _uiManager.OnMarketToggleRequest += (s, e) => eventFired = true;

            // Simulate Right Click on Market Button
            var rect = _uiManager.MarketButtonRect;
            var center = rect.Center;
            
            _mockInput.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(
                this, 
                new InputEventArgs(InputEventType.RightClick, new Vector2(center.X, center.Y), Microsoft.Xna.Framework.Input.Keys.None));

            Assert.IsFalse(eventFired, "Should ignore non-left click");
        }

        [TestMethod]
        public void HandleInputEvent_Invokes_ActiveElement_MarketButton()
        {
            // Initial State: Not Paused, Not Popup
            _uiManager.IsPaused = false;
            _uiManager.IsPopupVisible = false;

            bool eventFired = false;
            _uiManager.OnMarketToggleRequest += (s, e) => eventFired = true;

            var rect = _uiManager.MarketButtonRect;
            var center = rect.Center;

            _mockInput.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(
                this, 
                new InputEventArgs(InputEventType.LeftClick, new Vector2(center.X, center.Y), Microsoft.Xna.Framework.Input.Keys.None));

            Assert.IsTrue(eventFired, "Market Button click should fire event");
        }

        [TestMethod]
        public void HandleInputEvent_Ignores_InactiveElement_MarketButton_WhenPaused()
        {
            // State: Paused
            _uiManager.IsPaused = true; 
            _uiManager.IsPopupVisible = false;

            bool eventFired = false;
            _uiManager.OnMarketToggleRequest += (s, e) => eventFired = true;

            var rect = _uiManager.MarketButtonRect;
            var center = rect.Center;

            _mockInput.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(
                this, 
                new InputEventArgs(InputEventType.LeftClick, new Vector2(center.X, center.Y), Microsoft.Xna.Framework.Input.Keys.None));

            Assert.IsFalse(eventFired, "Market Button should be inactive when paused");
        }

        [TestMethod]
        public void HandleInputEvent_Ignores_InactiveElement_MarketButton_WhenTargeting()
        {
            // Without this, the Market/Assassinate/ReturnSpy/EndTurn buttons stayed clickable
            // throughout an unrelated in-progress targeting sequence, letting a click silently
            // strand or permanently desync it instead of being rejected outright. See
            // planning.txt.
            _uiManager.IsPaused = false;
            _uiManager.IsPopupVisible = false;
            _uiManager.IsTargeting = true;

            bool eventFired = false;
            _uiManager.OnMarketToggleRequest += (s, e) => eventFired = true;

            var rect = _uiManager.MarketButtonRect;
            var center = rect.Center;

            _mockInput.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(
                this,
                new InputEventArgs(InputEventType.LeftClick, new Vector2(center.X, center.Y), Microsoft.Xna.Framework.Input.Keys.None));

            Assert.IsFalse(eventFired, "Market Button should be inactive while a targeting sequence is in progress.");
        }

        [TestMethod]
        public void HandleInputEvent_Ignores_InactiveElement_EndTurnButton_WhenTargeting()
        {
            // The concrete bug repro this fixes: pressing End Turn while a "up to N" promotion
            // redemption (or any other targeting sequence) is still in progress - see
            // planning.txt.
            _uiManager.IsPaused = false;
            _uiManager.IsPopupVisible = false;
            _uiManager.IsTargeting = true;

            bool eventFired = false;
            _uiManager.OnEndTurnRequest += (s, e) => eventFired = true;

            var rect = _uiManager.EndTurnButtonRect;
            var center = rect.Center;

            _mockInput.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(
                this,
                new InputEventArgs(InputEventType.LeftClick, new Vector2(center.X, center.Y), Microsoft.Xna.Framework.Input.Keys.None));

            Assert.IsFalse(eventFired, "End Turn Button should be inactive while a targeting sequence is in progress.");
        }

        [TestMethod]
        public void HandleInputEvent_Invokes_ActiveElement_DeclineRepeatButton_WhenRepeatDeclinable()
        {
            // Unlike Market/Assassinate/ReturnSpy/EndTurn, this button is ACTIVE precisely
            // while a repeat-capable "up to N" effect is in progress (IsRepeatDeclinable),
            // the opposite gating condition to IsTargeting. See planning.txt's Phase 2 writeup.
            _uiManager.IsPaused = false;
            _uiManager.IsPopupVisible = false;
            _uiManager.IsRepeatDeclinable = true;

            bool eventFired = false;
            _uiManager.OnDeclineRepeatRequest += (s, e) => eventFired = true;

            var rect = _uiManager.DeclineRepeatButtonRect;
            var center = rect.Center;

            _mockInput.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(
                this,
                new InputEventArgs(InputEventType.LeftClick, new Vector2(center.X, center.Y), Microsoft.Xna.Framework.Input.Keys.None));

            Assert.IsTrue(eventFired, "Decline Repeat Button should fire when a repeat-capable effect is in progress.");
        }

        [TestMethod]
        public void HandleInputEvent_Ignores_InactiveElement_DeclineRepeatButton_WhenNotRepeatDeclinable()
        {
            // No repeat-capable effect in progress - the button doesn't exist to begin with,
            // so a click landing on its (otherwise-unused) screen rect must be a no-op.
            _uiManager.IsPaused = false;
            _uiManager.IsPopupVisible = false;
            _uiManager.IsRepeatDeclinable = false;

            bool eventFired = false;
            _uiManager.OnDeclineRepeatRequest += (s, e) => eventFired = true;

            var rect = _uiManager.DeclineRepeatButtonRect;
            var center = rect.Center;

            _mockInput.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(
                this,
                new InputEventArgs(InputEventType.LeftClick, new Vector2(center.X, center.Y), Microsoft.Xna.Framework.Input.Keys.None));

            Assert.IsFalse(eventFired, "Decline Repeat Button should be inactive when no repeat-capable effect is in progress.");
        }

        [TestMethod]
        public void HandleInputEvent_Ignores_InactiveElement_DeclineRepeatButton_WhenPaused()
        {
            // Even mid-effect, the pause menu takes priority - same convention as every other
            // interactive element in this class (IsActive always ANDs in !IsPaused).
            _uiManager.IsPaused = true;
            _uiManager.IsPopupVisible = false;
            _uiManager.IsRepeatDeclinable = true;

            bool eventFired = false;
            _uiManager.OnDeclineRepeatRequest += (s, e) => eventFired = true;

            var rect = _uiManager.DeclineRepeatButtonRect;
            var center = rect.Center;

            _mockInput.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(
                this,
                new InputEventArgs(InputEventType.LeftClick, new Vector2(center.X, center.Y), Microsoft.Xna.Framework.Input.Keys.None));

            Assert.IsFalse(eventFired, "Decline Repeat Button should be inactive while paused.");
        }

        [TestMethod]
        public void HandleInputEvent_Invokes_PopupConfirm_WhenConfirmationPopupVisible()
        {
            _uiManager.IsConfirmationPopupVisible = true;

            bool eventFired = false;
            _uiManager.OnPopupConfirm += (s, e) => eventFired = true;

            var rect = _uiManager.PopupConfirmButtonRect;
            var center = rect.Center;

            _mockInput.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(
                this,
                new InputEventArgs(InputEventType.LeftClick, new Vector2(center.X, center.Y), Microsoft.Xna.Framework.Input.Keys.None));

            Assert.IsTrue(eventFired, "Popup Confirm should fire when the confirmation popup is visible");
        }

        [TestMethod]
        public void HandleInputEvent_Ignores_PopupConfirm_WhenNotVisible()
        {
            _uiManager.IsConfirmationPopupVisible = false;

            bool eventFired = false;
            _uiManager.OnPopupConfirm += (s, e) => eventFired = true;

            var rect = _uiManager.PopupConfirmButtonRect;
            var center = rect.Center;

            _mockInput.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(
                this,
                new InputEventArgs(InputEventType.LeftClick, new Vector2(center.X, center.Y), Microsoft.Xna.Framework.Input.Keys.None));

            Assert.IsFalse(eventFired, "Popup Confirm should be inactive when not visible");
        }

        [TestMethod]
        public void HandleInputEvent_Ignores_PopupConfirm_WhenOnlyOptionalEffectPopupVisible()
        {
            // Regression test: the optional-effect popup sets the combined IsPopupVisible
            // flag (it gates the Main Game UI buttons too) but must NOT also activate
            // UIManager's generic PopupConfirmButtonRect - that button's screen bounds
            // overlap OptionalEffectPopup's own dedicated Yes button, and both used to be
            // wired active off the same combined flag, so a single click on "Yes" fired
            // BOTH handlers and double-invoked optional-effect accept (see planning.txt).
            _uiManager.IsPopupVisible = true;
            _uiManager.IsConfirmationPopupVisible = false;

            bool eventFired = false;
            _uiManager.OnPopupConfirm += (s, e) => eventFired = true;

            var rect = _uiManager.PopupConfirmButtonRect;
            var center = rect.Center;

            _mockInput.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(
                this,
                new InputEventArgs(InputEventType.LeftClick, new Vector2(center.X, center.Y), Microsoft.Xna.Framework.Input.Keys.None));

            Assert.IsFalse(eventFired, "Popup Confirm should stay inactive while only the optional-effect popup is open");
        }

        [TestMethod]
        public void HandleInputEvent_Invokes_ResumeButton_WhenPaused()
        {
            _uiManager.IsPaused = true;

            bool eventFired = false;
            _uiManager.OnResumeRequest += (s, e) => eventFired = true;

            var rect = _uiManager.ResumeButtonRect;
            var center = rect.Center;

            _mockInput.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(
                this, 
                new InputEventArgs(InputEventType.LeftClick, new Vector2(center.X, center.Y), Microsoft.Xna.Framework.Input.Keys.None));

            Assert.IsTrue(eventFired, "Resume should fire when paused");
        }
    }
}
