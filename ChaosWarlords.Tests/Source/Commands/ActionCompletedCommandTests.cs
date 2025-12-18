using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.States.Input;
using System.Collections.Generic;
using System;
using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Tests.Source.Commands
{
    [TestClass]
    public class ActionCompletedCommandTests
    {
        // ------------------------------------------------------------------------
        // 1. DEDICATED MOCKS (Tailored for Action Completion Logic)
        // ------------------------------------------------------------------------

        private class MockActionSystem : IActionSystem
        {
            public ActionState CurrentState { get; set; } = ActionState.Normal;
            public Card? PendingCard { get; set; } // Settable for tests
            public Site? PendingSite { get; set; }

            public bool CancelTargetingCalled { get; private set; }

            public void CancelTargeting() => CancelTargetingCalled = true;

            // Stubs
            public bool IsTargeting() => false;
            public void SetCurrentPlayer(Player p) { }
            public void StartTargeting(ActionState state, Card card) { }
            public void TryStartAssassinate() { }
            public void TryStartReturnSpy() { }
            public void FinalizeSpyReturn(PlayerColor color) { }
            public void HandleTargetClick(MapNode node, Site site) { }
            public event EventHandler? OnActionCompleted;
            public event EventHandler<string>? OnActionFailed;
            public void RaiseActionCompleted() => OnActionCompleted?.Invoke(this, EventArgs.Empty);
            public void RaiseActionFailed(string s) => OnActionFailed?.Invoke(this, s);
        }

        private class MockGameplayState : IGameplayState
        {
            public IActionSystem ActionSystem { get; }

            // Verification Flags
            public bool ResolveCardEffectsCalled { get; private set; }
            public bool MoveCardToPlayedCalled { get; private set; }
            public bool SwitchToNormalModeCalled { get; private set; }
            public Card? LastResolvedCard { get; private set; }
            public Card? LastMovedCard { get; private set; }

            public MockGameplayState(IActionSystem actionSystem)
            {
                ActionSystem = actionSystem;
            }

            public void ResolveCardEffects(Card card)
            {
                ResolveCardEffectsCalled = true;
                LastResolvedCard = card;
            }

            public void MoveCardToPlayed(Card card)
            {
                MoveCardToPlayedCalled = true;
                LastMovedCard = card;
            }

            public void SwitchToNormalMode()
            {
                SwitchToNormalModeCalled = true;
            }

            // Stubs
            public InputManager InputManager => null!;
            public IUISystem UIManager => null!;
            public IMapManager MapManager => null!;
            public IMarketManager MarketManager => null!;
            public TurnManager TurnManager => null!;
            public IInputMode InputMode { get; set; } = null!;
            public bool IsMarketOpen { get; set; }
            public int HandY => 0;
            public int PlayedY => 0;
            public void PlayCard(Card card) { }
            public void SwitchToTargetingMode() { }
            public void ToggleMarket() { }
            public void CloseMarket() { }
            public void EndTurn() { }
            public void ArrangeHandVisuals() { }
            public string GetTargetingText(ActionState state) => "";
            public void LoadContent() { }
            public void UnloadContent() { }
            public void Update(GameTime gameTime) { }
            public void Draw(SpriteBatch spriteBatch) { }
        }

        // ------------------------------------------------------------------------
        // 2. UNIT TESTS
        // ------------------------------------------------------------------------

        [TestInitialize]
        public void Setup()
        {
            // Ensure logger is ready (prevents static crashes)
            GameLogger.Initialize();
        }

        [TestMethod]
        public void ActionCompleted_WithPendingCard_FinalizesCard_AndResetsState()
        {
            // 1. Arrange
            var mockAction = new MockActionSystem();
            var mockState = new MockGameplayState(mockAction);

            var card = new Card("test_id", "Test Card", 0, CardAspect.Neutral, 0, 0);
            mockAction.PendingCard = card;

            var command = new ActionCompletedCommand();

            // 2. Act
            command.Execute(mockState);

            // 3. Assert
            // It MUST resolve effects (gain power, etc.)
            Assert.IsTrue(mockState.ResolveCardEffectsCalled, "Should resolve card effects.");
            Assert.AreEqual(card, mockState.LastResolvedCard);

            // It MUST move card to discard pile
            Assert.IsTrue(mockState.MoveCardToPlayedCalled, "Should move card to played pile.");
            Assert.AreEqual(card, mockState.LastMovedCard);

            // It MUST reset the UI state
            Assert.IsTrue(mockAction.CancelTargetingCalled, "Should cancel targeting on the backend.");
            Assert.IsTrue(mockState.SwitchToNormalModeCalled, "Should switch Input Mode back to Normal.");
        }

        [TestMethod]
        public void ActionCompleted_NoPendingCard_JustResetsState()
        {
            // 1. Arrange
            var mockAction = new MockActionSystem();
            var mockState = new MockGameplayState(mockAction);

            // Case: Completing a pure map action (e.g. Return Spy) that didn't involve a card
            mockAction.PendingCard = null;

            var command = new ActionCompletedCommand();

            // 2. Act
            command.Execute(mockState);

            // 3. Assert
            Assert.IsFalse(mockState.ResolveCardEffectsCalled, "Should NOT resolve effects if no card pending.");
            Assert.IsFalse(mockState.MoveCardToPlayedCalled, "Should NOT move card if no card pending.");

            // Still needs to reset state
            Assert.IsTrue(mockAction.CancelTargetingCalled);
            Assert.IsTrue(mockState.SwitchToNormalModeCalled);
        }
    }
}