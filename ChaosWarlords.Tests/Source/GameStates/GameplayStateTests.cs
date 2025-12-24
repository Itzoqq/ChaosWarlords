using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Commands;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.States.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Contexts;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Views;
using System.Reflection;

namespace ChaosWarlords.Tests.States
{
    [TestClass]
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
        public void Update_EnterKey_TriggersEndTurn()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);
            _inputProvider.GetKeyboardState().Returns(new KeyboardState(Keys.Enter));

            // Run Update
            state.Update(new GameTime());

            // TurnManager.EndTurn() should have been called (or rewards distributed)
            _mapManager.Received().DistributeControlRewards(Arg.Any<Player>());
        }

        [TestMethod]
        public void Update_RightClick_CancelsMarket()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);
            state.IsMarketOpen = true;

            var pressedState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
            _inputProvider.GetMouseState().Returns(pressedState);

            state.Update(new GameTime());

            Assert.IsFalse(state.IsMarketOpen);
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode));
        }

        [TestMethod]
        public void Command_BuyCard_BuysCard_WhenAffordable()
        {
            // Setup
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            var player = state.MatchContext.ActivePlayer;
            player.Influence = 5;

            var cardToBuy = new Card("test", "Test Minion", 3, CardAspect.Warlord, 1, 1, 0);

            // Execute Command
            var cmd = new BuyCardCommand(cardToBuy);
            cmd.Execute(state);

            _marketManager.Received(1).TryBuyCard(player, cardToBuy);
        }

        [TestMethod]
        public void Command_DeployTroop_DeploysTroop()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            var player = state.MatchContext.ActivePlayer;
            player.Power = 2; // cost is 1

            var node = new MapNode(1, Vector2.Zero);
            _mapManager.TryDeploy(Arg.Any<Player>(), Arg.Any<MapNode>()).Returns(true);

            var cmd = new DeployTroopCommand(node);
            cmd.Execute(state);

            _mapManager.Received(1).TryDeploy(state.MatchContext.ActivePlayer, node);
        }





        [TestMethod]
        public void PlayCard_TriggersTargeting_SwitchInputMode_WhenTargetsExist()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            var card = new Card("assassin", "Assassin", 3, CardAspect.Shadow, 1, 1, 0);
            card.AddEffect(new CardEffect(EffectType.Assassinate, 1));

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
        internal class TestableGameplayState : GameplayState
        {
            private readonly IInputProvider _testInput;
            private readonly ICardDatabase _testDb;

            public TestableGameplayState(Game game, IInputProvider input, ICardDatabase db)
                : base(game, input, db)
            {
                _testInput = input;
                _testDb = db;
            }

            public void InitializeTestEnvironment(IMapManager map, IMarketManager market, IActionSystem action)
            {
                _inputManagerBacking = new InputManager(_testInput);
                _uiManagerBacking = new UIManager(800, 600);

                var p1 = new Player(PlayerColor.Red);
                var p2 = new Player(PlayerColor.Blue);
                var tm = new TurnManager(new List<Player> { p1, p2 });

                // Ensure Active Player is set and turn context exists
                // Note: Depending on your TurnManager impl, you might need tm.StartGame() or similar.
                // Assuming constructor sets p1 as active.

                _matchContext = new MatchContext(tm, map, market, action, _testDb);

                _matchController = new MatchController(_matchContext);

                // --- Initialize the missing Coordinators ---

                // 1. Interaction Mapper (View is null, but constructor tolerates it)
                var mapper = new InteractionMapper(_view);
                SetPrivateField("_interactionMapper", mapper);

                // 2. Input Coordinator (Depends on Managers initialized above)
                var coordinator = new GameplayInputCoordinator(this, _inputManagerBacking, _matchContext);
                SetPrivateField("_inputCoordinator", coordinator);

                // 3. CardPlaySystem (NEW dependency)
                // We must use reflection to set it because it's private and typically set in LoadContent
                var cardSystem = new CardPlaySystem(_matchContext, _matchController, () => SwitchToTargetingMode());
                SetPrivateField("_cardPlaySystem", cardSystem);

                // --- END FIX ---

                InitializeEventSubscriptions();
                SwitchToNormalMode();
            }

            // Reflection Helper
            private void SetPrivateField(string fieldName, object value)
            {
                typeof(GameplayState)
                    .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(this, value);
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
            var creditSourceCard = new Card("drow_noble", "Noble", 0, CardAspect.Blasphemy, 0, 0, 0);
            state.MatchContext.TurnManager.CurrentTurnContext.AddPromotionCredit(creditSourceCard, 1);

            // 2. CRITICAL: Configure the Mock ActionSystem to behave like the real one
            // The Coordinator reads 'CurrentState' to decide which mode to create. 
            // Since _actionSystem is a Mock, we must tell it: "When StartTargeting is called, update CurrentState."
            _actionSystem.CurrentState.Returns(ActionState.Normal); // Start Normal

            _actionSystem.When(x => x.StartTargeting(ActionState.SelectingCardToPromote, Arg.Any<Card>()))
                          .Do(x => _actionSystem.CurrentState.Returns(ActionState.SelectingCardToPromote));

            // Add a DIFFERENT card to PlayedCards to satisfy "Cannot promote self" rule
            var targetCard = new Card("minion", "Minion", 0, CardAspect.Blasphemy, 0, 0, 0);
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
            state.MatchContext.ActivePlayer.Hand.Add(new Card("test", "Test", 0, CardAspect.Warlord, 0, 0, 0));

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
            _mapManager.DidNotReceive().DistributeControlRewards(Arg.Any<Player>());
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
            _mapManager.Received(1).DistributeControlRewards(Arg.Any<Player>());
        }

        [TestMethod]
        public void PopupConfirm_EndsTurn()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            // Open Popup
            state.MatchContext.ActivePlayer.Hand.Add(new Card("test", "Test", 0, CardAspect.Warlord, 0, 0, 0));
            _inputProvider.GetKeyboardState().Returns(new KeyboardState(Keys.Enter));
            state.Update(new GameTime());
            
            Assert.IsTrue(state.IsConfirmationPopupOpen);

            // Confirm
            // We need to trigger OnPopupConfirm. As we use real UIManager in TestableGameplayState, 
            // we have to simulate a click on the confirm button.
            // We need to know where the button is.
            // HACK for test: We can use Reflection to invoke 'HandlePopupConfirm'.
            var method = typeof(GameplayState).GetMethod("HandlePopupConfirm", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "HandlePopupConfirm method not found via reflection");
            method.Invoke(state, new object[] { null!, EventArgs.Empty });

            Assert.IsFalse(state.IsConfirmationPopupOpen);
            _mapManager.Received(1).DistributeControlRewards(Arg.Any<Player>());
        }

        [TestMethod]
        public void PopupCancel_ClosesPopup_AndDoesNotEndTurn()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            // Open Popup
            state.MatchContext.ActivePlayer.Hand.Add(new Card("test", "Test", 0, CardAspect.Warlord, 0, 0, 0));
            _inputProvider.GetKeyboardState().Returns(new KeyboardState(Keys.Enter));
            state.Update(new GameTime());

            Assert.IsTrue(state.IsConfirmationPopupOpen);

            // Cancel
            var method = typeof(GameplayState).GetMethod("HandlePopupCancel", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "HandlePopupCancel method not found via reflection");
            method.Invoke(state, new object[] { null!, EventArgs.Empty });

            Assert.IsFalse(state.IsConfirmationPopupOpen);
            _mapManager.DidNotReceive().DistributeControlRewards(Arg.Any<Player>());
        }

    }
}