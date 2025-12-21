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

            state.SwitchToNormalMode();

            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode));
        }

        [TestMethod]
        public void ToggleMarket_SwitchesInputModesAndFlags()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);
            state.SwitchToNormalMode();

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
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);
            var initialPlayer = state.MatchContext.ActivePlayer;
            _inputProvider.GetKeyboardState().Returns(new KeyboardState(Keys.Enter));

            // Act
            state.Update(new GameTime());

            // Assert
            Assert.AreNotEqual(initialPlayer, state.MatchContext.ActivePlayer);
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
        }

        [TestMethod]
        public void Command_BuyCard_BuysCard_WhenAffordable()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);
            state.SwitchToNormalMode();
            state.IsMarketOpen = true;

            var player = state.MatchContext.ActivePlayer;
            player.Influence = 10;
            var cardToBuy = new Card("market_card", "Buy Me", 3, CardAspect.Sorcery, 1, 0, 0);

            _marketManager.MarketRow.Returns(new List<Card> { cardToBuy });
            _marketManager.TryBuyCard(Arg.Any<Player>(), Arg.Any<Card>()).Returns(true);

            var command = new BuyCardCommand(cardToBuy);
            command.Execute(state);

            _marketManager.Received(1).TryBuyCard(player, cardToBuy);
        }

        [TestMethod]
        public void Command_DeployTroop_DeploysTroop()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);
            state.SwitchToNormalMode();

            var node = new MapNode(1, Vector2.Zero);
            _mapManager.TryDeploy(Arg.Any<Player>(), Arg.Any<MapNode>()).Returns(true);

            var command = new DeployTroopCommand(node);
            command.Execute(state);

            _mapManager.Received(1).TryDeploy(state.MatchContext.ActivePlayer, node);
        }

        // --- NEW TESTS FOR FIXES ---

        [TestMethod]
        public void PlayCard_WithTargetingEffect_ButNoTargets_SkipsTargeting_AndPlaysCard()
        {
            // Verifies the "Whiff" logic (Rule: Do as much as you can)
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            // Card with Assassinate (Targeting) and Gain Power (Instant)
            var card = new Card("kill_fail", "Bad Assassin", 0, CardAspect.Shadow, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.Assassinate, 1));
            card.AddEffect(new CardEffect(EffectType.GainResource, 2, ResourceType.Power)); // Should still get this

            // Mock: NO Valid Targets
            _mapManager.HasValidAssassinationTarget(Arg.Any<Player>()).Returns(false);

            // Act
            state.PlayCard(card);

            // Assert
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode), "Should NOT switch to Targeting Mode if no targets exist");
            Assert.AreEqual(2, state.MatchContext.ActivePlayer.Power, "Should still gain resources (PlayCard was called)");
            Assert.Contains(card, state.MatchContext.ActivePlayer.PlayedCards, "Card should be played");
        }

        [TestMethod]
        public void PlayCard_WithPromote_DoesNotSwitchToTargeting()
        {
            // Verifies Promote is delayed to End Turn
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            var card = new Card("promo", "Promoter", 0, CardAspect.Warlord, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.Promote, 1));

            // Act
            state.PlayCard(card);

            // Assert
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode), "Promote should NOT trigger targeting immediately");
            // Check if credit was added
            Assert.AreEqual(1, state.MatchContext.TurnManager.CurrentTurnContext.PendingPromotionsCount, "Promote credit should be added");
        }

        [TestMethod]
        public void PlayCard_TriggersTargeting_SwitchInputMode_WhenTargetsExist()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            var card = new Card("kill", "Assassin", 0, CardAspect.Shadow, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.Assassinate, 1));

            // Mock: YES Valid Targets
            _mapManager.HasValidAssassinationTarget(Arg.Any<Player>()).Returns(true);

            state.PlayCard(card);

            Assert.IsInstanceOfType(state.InputMode, typeof(TargetingInputMode));
        }

        [TestMethod]
        public void Update_RightClick_CancelsTargeting_AndResetsInputMode()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            _actionSystem.IsTargeting().Returns(true);
            state.SwitchToTargetingMode();

            var rightClick = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
            _inputProvider.GetMouseState().Returns(rightClick);

            state.Update(new GameTime());

            _actionSystem.Received(1).CancelTargeting();
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

                _matchContext = new MatchContext(tm, map, market, action, _testDb);

                _matchController = new MatchController(_matchContext);
                InitializeEventSubscriptions();
                SwitchToNormalMode();
            }

            public new MatchContext MatchContext => base.MatchContext;
        }
    }
}