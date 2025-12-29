using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Input;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.States.Input;
using NSubstitute;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Input.Controllers;

namespace ChaosWarlords.Tests.States
{
    [TestClass]

    [TestCategory("Integration")]
    public class GameplayStateTests
    {
        private IInputProvider _inputProvider = null!;
        private ICardDatabase _cardDatabase = null!;
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private IActionSystem _actionSystem = null!;

        [TestInitialize]
        public void Setup()
        {
            _inputProvider = Substitute.For<IInputProvider>();
            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _cardDatabase = Substitute.For<ICardDatabase>();
            _cardDatabase.GetAllMarketCards().Returns(new List<Card>());
        }

        [TestMethod]
        public void LoadContent_InitializesInfrastructure_WhenGameIsNull_HeadlessMode()
        {
            // Arrange
            // Passing null for Game, ensuring it doesn't crash
            var state = new GameplayState(null!, _inputProvider, _cardDatabase);

            // Act
            state.LoadContent();

            // Assert
            Assert.IsNotNull(state.InputManager);
            Assert.IsNotNull(state.UIManager);
        }

        [TestMethod]
        public void Draw_DelegatesToView_WhenViewIsPresent()
        {
            // Arrange
            var viewMock = Substitute.For<IGameplayView>();
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase, viewMock);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            // Act
            state.Draw(null!); // SpriteBatch null is fine as we mock the view

            // Assert
            viewMock.ReceivedWithAnyArgs(1).Draw(null!, null!, null!, null!, false, "", false, false);
        }

        [TestMethod]
        public void SwitchToTargetingMode_SetsCorrectInputMode()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            state.SwitchToTargetingMode();

            Assert.IsInstanceOfType(state.InputMode, typeof(TargetingInputMode));
        }

        [TestMethod]
        public void SwitchToNormalMode_SetsCorrectInputMode()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            state.SwitchToTargetingMode(); // Switch away first
            state.SwitchToNormalMode();

            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode));
        }

        [TestMethod]
        public void ToggleMarket_SwitchesInputModesAndFlags()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            state.ToggleMarket();
            Assert.IsTrue(state.IsMarketOpen);
            Assert.IsInstanceOfType(state.InputMode, typeof(MarketInputMode));

            state.ToggleMarket();
            Assert.IsFalse(state.IsMarketOpen);
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode));
        }









        [TestMethod]
        public void PlayCard_TriggersTargeting_SwitchInputMode_WhenTargetsExist()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            var card = TestData.Cards.AssassinCard();

            // Add card to hand
            state.MatchContext.ActivePlayer.Hand.Add(card);

            // Setup: Map says valid targets EXIST
            _mapManager.HasValidAssassinationTarget(Arg.Any<Player>()).Returns(true);

            state.PlayCard(card);

            // Should switch to targeting
            Assert.IsInstanceOfType(state.InputMode, typeof(TargetingInputMode));
            _actionSystem.Received().StartTargeting(ActionState.TargetingAssassinate, card);

            // REGRESSION CHECK:
            // Ensure card is NOT moved to PlayedCards yet!
            Assert.Contains(card, state.MatchContext.ActivePlayer.Hand, "Card should remain in Hand during targeting.");
            Assert.DoesNotContain(card, state.MatchContext.ActivePlayer.PlayedCards, "Card should NOT be in PlayedCards during targeting.");
        }

        [TestMethod]
        public void Update_RightClick_CancelsTargeting_AndResetsInputMode()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            // Force into targeting
            _actionSystem.IsTargeting().Returns(true);
            state.SwitchToTargetingMode();

            var rightClick = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
            _inputProvider.GetMouseState().Returns(rightClick);

            state.Update(new GameTime());

            _actionSystem.Received().CancelTargeting();
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode));
        }

        [TestMethod]
        public void SubscribesToEvents_OnActionCompleted_ResetsInputMode()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            state.SwitchToTargetingMode();

            _actionSystem.OnActionCompleted += Raise.Event();

            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode));
        }

        // --- Helper Class ---
        // Marked Internal so we can assign internal fields directly
        internal class TestableGameplayState : GameplayState
        {
            private readonly IInputProvider _testInput;
            private readonly ICardDatabase _testDb;

            public TestableGameplayState(Game? game, IInputProvider input, ICardDatabase db, IGameplayView? view = null)
                : base(game, input, db, view ?? Substitute.For<IGameplayView>())
            {
                _testInput = input;
                _testDb = db;

                // Initialize View Mock Properties to avoid potential NREs
                if (_view != null)
                {
                    _view.HandViewModels.Returns(new List<CardViewModel>());
                    _view.PlayedViewModels.Returns(new List<CardViewModel>());
                    _view.MarketViewModels.Returns(new List<CardViewModel>());
                }
            }

            public void InitializeTestEnvironment(IMapManager map, IMarketManager market, IActionSystem action)
            {
                _inputManagerBacking = new InputManager(_testInput);
                _uiManagerBacking = Substitute.For<IUIManager>();

                var p1 = TestData.Players.RedPlayer();
                var p2 = TestData.Players.BluePlayer();
                var mockRandom = Substitute.For<IGameRandom>();
                var tm = new TurnManager(new List<Player> { p1, p2 }, mockRandom);

                var ps = new PlayerStateManager();
                _matchContext = new MatchContext(tm, map, market, action, _testDb, ps);
                _matchManager = new MatchManager(_matchContext);

                // --- DIRECT FIELD ACCESS (No Reflection) ---
                // Thanks to [InternalsVisibleTo] and 'internal' modifier

                // 1. Interaction Mapper
                _interactionMapper = new InteractionMapper(_view!);

                // 2. Input Coordinator
                _inputCoordinator = new GameplayInputCoordinator(this, _inputManagerBacking, _matchContext);

                // 3. CardPlaySystem
                _cardPlaySystem = new CardPlaySystem(_matchContext, _matchManager, () => SwitchToTargetingMode());

                // 4. UIEventMediator
                _uiEventMediator = new UIEventMediator(this, _uiManagerBacking, action, null!);
                _uiEventMediator.Initialize();

                // 5. PlayerController
                _playerController = new PlayerController(this, _inputManagerBacking, _inputCoordinator, _interactionMapper);

                SwitchToNormalMode();
            }

            public new MatchContext MatchContext => base.MatchContext;
        }

        [TestMethod]
        public void Update_EnterKey_WithPendingPromotions_SwitchesToPromoteInputMode()
        {
            // --- Arrange ---
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            // 1. Simulate having Pending Promotions
            // We assume the TurnManager is real (based on your TestableGameplayState setup)
            var creditSourceCard = TestData.Cards.NobleCard();
            state.MatchContext.TurnManager.CurrentTurnContext.AddPromotionCredit(creditSourceCard, 1);

            // 2. CRITICAL: Configure the Mock ActionSystem to behave like the real one
            // The Coordinator reads 'CurrentState' to decide which mode to create. 
            // Since _actionSystem is a Mock, we must tell it: "When StartTargeting is called, update CurrentState."
            _actionSystem.CurrentState.Returns(ActionState.Normal); // Start Normal

            _actionSystem.When(x => x.StartTargeting(ActionState.SelectingCardToPromote, Arg.Any<Card>()))
                          .Do(x => _actionSystem.CurrentState.Returns(ActionState.SelectingCardToPromote));

            // Add a DIFFERENT card to PlayedCards to satisfy "Cannot promote self" rule
            var targetCard = TestData.Cards.CheapCard();
            state.MatchContext.ActivePlayer.PlayedCards.Add(targetCard);

            // 3. Simulate pressing 'Enter'
            _inputProvider.GetKeyboardState().Returns(new KeyboardState(Keys.Enter));

            // --- Act ---
            state.Update(new GameTime());

            // --- Assert ---
            // 1. Verify your Did GameplayState tell ActionSystem to enter the Promote state?
            _actionSystem.Received(1).StartTargeting(ActionState.SelectingCardToPromote, null);

            // 2. Verify Result: Did the InputCoordinator correctly switch to PromoteInputMode?
            Assert.IsInstanceOfType(state.InputMode, typeof(PromoteInputMode));
        }









        [TestMethod]
        public void EndTurnRequest_WithUnplayedCards_OpensPopup_AndDoesNotEndTurn()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            // Add unplayed card
            state.MatchContext.ActivePlayer.Hand.Add(TestData.Cards.CheapCard());

            // Raise Request (simulating Button Click)
            // We need to access the mock UI system. TestableGameplayState creates a real UIManager, 
            // but we can trigger the event handler directly if we expose it or use the mock approach.
            // Since TestableGameplayState uses 'new UIManager', we can't easily retrieve it unless we expose it.
            // BETTER: Use the TestableGameplayState to inject our MockUISystem!

            // Re-initializing Test Environment with Mock UI would be cleaner, but let's stick to the current pattern.
            // We can invoke the private handler via reflection or just simulating the Enter Key which now calls HandleEndTurnRequest.

            _inputProvider.GetKeyboardState().Returns(new KeyboardState(Keys.Enter));
            state.Update(new GameTime());

            Assert.IsTrue(state.IsConfirmationPopupOpen);
            _mapManager.DidNotReceive().DistributeStartOfTurnRewards(Arg.Any<Player>());
        }

        [TestMethod]
        public void EndTurnRequest_WithNoUnplayedCards_EndsTurnImmediately()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            state.MatchContext.ActivePlayer.Hand.Clear();

            _inputProvider.GetKeyboardState().Returns(new KeyboardState(Keys.Enter));
            state.Update(new GameTime());

            Assert.IsFalse(state.IsConfirmationPopupOpen);
            _mapManager.Received(1).DistributeStartOfTurnRewards(Arg.Any<Player>());
        }

        [TestMethod]
        public void PopupConfirm_EndsTurn()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            // Open Popup
            state.MatchContext.ActivePlayer.Hand.Add(TestData.Cards.CheapCard());
            _inputProvider.GetKeyboardState().Returns(new KeyboardState(Keys.Enter));
            state.Update(new GameTime());

            Assert.IsTrue(state.IsConfirmationPopupOpen);

            // Confirm
            state.UIManager.OnPopupConfirm += Raise.Event();

            Assert.IsFalse(state.IsConfirmationPopupOpen);
            _mapManager.Received(1).DistributeStartOfTurnRewards(Arg.Any<Player>());
        }

        [TestMethod]
        public void PopupCancel_ClosesPopup_AndDoesNotEndTurn()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            // Open Popup
            state.MatchContext.ActivePlayer.Hand.Add(TestData.Cards.CheapCard());
            _inputProvider.GetKeyboardState().Returns(new KeyboardState(Keys.Enter));
            state.Update(new GameTime());

            Assert.IsTrue(state.IsConfirmationPopupOpen);

            // Cancel
            state.UIManager.OnPopupCancel += Raise.Event();

            Assert.IsFalse(state.IsConfirmationPopupOpen);
            _mapManager.DidNotReceive().DistributeStartOfTurnRewards(Arg.Any<Player>());
        }



        [TestMethod]
        public void GetTargetingText_ReturnsCorrectText()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            var text = state.GetTargetingText(ActionState.TargetingAssassinate);

            Assert.AreEqual("TargetingAssassinate", text);
        }

        [TestMethod]
        public void SwitchToPromoteMode_SetsActionSystemState()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            state.SwitchToPromoteMode(1);

            _actionSystem.Received(1).StartTargeting(ActionState.SelectingCardToPromote, null!);
        }

        [TestMethod]
        public void MoveCardToPlayed_DelegatesToMatchManager()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            var card = TestData.Cards.CheapCard();
            state.MatchContext.ActivePlayer.Hand.Add(card);

            state.MoveCardToPlayed(card);

            Assert.Contains(card, state.MatchContext.ActivePlayer.PlayedCards);
            Assert.DoesNotContain(card, state.MatchContext.ActivePlayer.Hand);
        }
    }
}


