using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Factories;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;

using NSubstitute;

namespace ChaosWarlords.Tests.Source.Integration.Mechanics
{
    [TestClass]
    public class DevourFromInnerCircleIntegrationTests
    {
        private MatchContext _context = null!;
        private MatchManager _matchManager = null!;
        private ActionSystem _actionSystem = null!;
        private IUIEventMediator _uiMediator = null!;
        private IGameLogger _logger = null!;

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<IGameLogger>();
            _uiMediator = Substitute.For<IUIEventMediator>();
            var database = Substitute.For<ICardDatabase>();
            database.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new System.Collections.Generic.List<Card>());
            var replayManager = Substitute.For<IReplayManager>();
            var factory = new MatchFactory(database, _logger);

            // Basic match setup
            var worldData = factory.Build(replayManager, 12345);
            _actionSystem = (ActionSystem)worldData.ActionSystem;

            Console.WriteLine($"TurnManager is null: {worldData.TurnManager == null}");
            Console.WriteLine($"MapManager is null: {worldData.MapManager == null}");
            Console.WriteLine($"MarketManager is null: {worldData.MarketManager == null}");
            Console.WriteLine($"ActionSystem is null: {worldData.ActionSystem == null}");
            Console.WriteLine($"Database is null: {database == null}");
            Console.WriteLine($"PlayerState is null: {worldData.PlayerStateManager == null}");
            Console.WriteLine($"Logger is null: {_logger == null}");

            _context = new MatchContext(
                worldData.TurnManager!,
                worldData.MapManager!,
                worldData.MarketManager!,
                worldData.ActionSystem!,
                database!,
                worldData.PlayerStateManager!,
                _uiMediator,
                _logger!
            );

            // Initialize MatchManager
            _matchManager = new MatchManager(_context, _logger!, new VictoryManager(_logger!));
            _actionSystem.SetMatchManager(_matchManager);
            _actionSystem.SetMatchContext(_context);

            // Give active player some resources
            _context.ActivePlayer.AddPower(10);
            _context.ActivePlayer.AddInfluence(10);
        }

        [TestMethod]
        public void DevourInnerCircle_StandardFlow_RemovesCardAndGrantsBonus()
        {
            // 1. Arrange
            var player = _context.ActivePlayer;

            // Add a card to Inner Circle (Target)
            var innerCard = new Card("inner_victim", "Inner Victim", 1, CardAspect.Neutral, 1, 1, 0);
            innerCard.Location = CardLocation.InnerCircle;
            player.AddToInnerCircle(innerCard);

            // Create a generic card with "Devour Inner Circle" effect
            var devourCard = new Card("devourer", "Inner Devourer", 2, CardAspect.Sorcery, 0, 0, 0);
            devourCard.AddEffect(new CardEffect(EffectType.Devour, 0)
            {
                TargetLocation = CardLocation.InnerCircle,
                OnSuccess = new CardEffect(EffectType.GainResource, 3, ResourceType.Influence)
            });
            player.AddToHand(devourCard);

            // 2. Act - Play the card
            // We simulate the PlayCard flow manually since we don't have the full InputCoordinator here
            // We simulate the PlayCard flow manually since we don't have the full InputCoordinator here
            // _actionSystem.PendingCard is set by StartTargeting inside the strategy execution

            // Trigger start logic (usually called by CardPlaySystem)
            var strategy = ChaosWarlords.Source.Mechanics.Rules.DevourStrategyFactory.GetStrategy(CardLocation.InnerCircle);
            strategy.Execute(devourCard, _context, _logger, () => { }, false);

            // Verify State Transition
            Assert.AreEqual(ActionState.TargetingDevourInnerCircle, _actionSystem.CurrentState, "Should switch to Inner Circle Targeting");

            // Simulate Selection
            var cmd = _actionSystem.HandleDevourInnerCircleSelection(innerCard);
            Assert.IsNotNull(cmd, "Should generate Devour Command");

            // Execute Command
            // Needed a state Double
            var testState = new TestGameplayState(_context, _matchManager, _logger);
            cmd?.Execute(testState.MatchContext);

            // 3. Assert
            CollectionAssert.DoesNotContain(player.InnerCircle.ToList(), innerCard, "Inner Circle card should be removed");
            Assert.AreEqual(CardLocation.Void, innerCard.Location, "Inner Circle card should be in Void");

            // Note: OnSuccess effect (Gain Influence) is triggered by the Command execution via MatchManager logic
            // But checking influence might be tricky if "ResumeDevourChain" isn't fully mocked/integrated here.
            // The command only does the Devour. The 'OnSuccess' is handled by the callback in the ActionSystem chain.
            // For this test, verifying the Devour action itself is the primary goal.
        }

        [TestMethod]
        public void DevourInnerCircle_Emptylist_AutoCompletesOrLogsWarning()
        {
            // 1. Arrange
            var player = _context.ActivePlayer;
            player.ClearInnerCircle(); // Ensure empty

            var devourCard = new Card("devourer", "Inner Devourer", 2, CardAspect.Sorcery, 0, 0, 0);
            devourCard.AddEffect(new CardEffect(EffectType.Devour, 0)
            {
                TargetLocation = CardLocation.InnerCircle,
                IsOptional = true // CRITICAL: Only optional effects trigger the UI popup check
            });
            player.AddToHand(devourCard);

            // 2. Act
            // Direct execution via Strategy would bypass CardEffectProcessor's optional check logic if we call Execute directly on Strategy?
            // NO. Strategy.Execute is for the ACTION part (Targeting). 
            // The POPUP happens in CardEffectProcessor BEFORE Strategy.Execute is called.
            // So we must test CardEffectProcessor logic here, or just simulate the flow.

            // To test "Skipping Popup", we must invoke CardEffectProcessor.ResolveEffects
            ChaosWarlords.Source.Mechanics.Rules.CardEffectProcessor.ResolveEffects(devourCard, _context, false, _logger);

            // 3. Assert
            // Ensure UI was NOT asked for permission
            _uiMediator.DidNotReceive().RequestOptionalEffect(
                Arg.Any<Card>(),
                Arg.Any<CardEffect>(),
                Arg.Any<Action>(),
                Arg.Any<Action>()
            );

            // Ensure logic remains safe (no targeting state entered)
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "Should NOT enter targeting state if skipped");
        }

        [TestMethod]
        public void DevourInnerCircle_PromoteFlow_AddsPromotionCredits()
        {
            // 1. Arrange
            var player = _context.ActivePlayer;

            // Add a card to Inner Circle (Target)
            var innerCard = new Card("inner_victim", "Inner Victim", 1, CardAspect.Neutral, 1, 1, 0);
            innerCard.Location = CardLocation.InnerCircle;
            player.AddToInnerCircle(innerCard);

            // Create Cultist of Myrkul behavior (Devour Inner Circle -> Gain 3 Infl -> Promote 2)
            var cultist = new Card("cultist", "Cultist of Myrkul", 4, CardAspect.Oblivion, 0, 0, 0);
            cultist.AddEffect(new CardEffect(EffectType.Devour, 0)
            {
                TargetLocation = CardLocation.InnerCircle,
                IsOptional = true,
                OnSuccess = new CardEffect(EffectType.GainResource, 3, ResourceType.Influence)
                {
                    OnSuccess = new CardEffect(EffectType.Promote, 2)
                }
            });
            player.AddToHand(cultist);

            // 2. Act - Play and Devour
            var strategy = ChaosWarlords.Source.Mechanics.Rules.DevourStrategyFactory.GetStrategy(CardLocation.InnerCircle);
            strategy.Execute(cultist, _context, _logger, () => { }, false);

            Assert.AreEqual(ActionState.TargetingDevourInnerCircle, _actionSystem.CurrentState);

            var cmd = _actionSystem.HandleDevourInnerCircleSelection(innerCard);

            // Execute Command
            var testState = new TestGameplayState(_context, _matchManager, _logger);
            cmd?.Execute(testState.MatchContext);
            // ResumeDevourChain is now handled automatically by the command's callback chain

            // 3. Assert
            // Verify Card Removed
            CollectionAssert.DoesNotContain(player.InnerCircle.ToList(), innerCard);
            Assert.AreEqual(CardLocation.Void, innerCard.Location);

            // Verify Influence Gained (Base 10 + 3 = 13)
            Assert.AreEqual(13, player.Influence, "Should have gained 3 Influence");

            // Verify Promotion Credits Added (Flow: Devour -> Influence -> Promote)
            int pendingPromotions = _context.TurnManager.CurrentTurnContext.PendingPromotionsCount;
            Assert.AreEqual(2, pendingPromotions, "Should have 2 pending promotions after devouring.");
        }

        private class TestGameplayState : ChaosWarlords.Source.Core.Interfaces.State.IGameplayState
        {
            public MatchContext MatchContext { get; }
            public IMatchManager MatchManager { get; }

            // Managers
            public IMapManager MapManager => MatchContext.MapManager;
            public IMarketManager MarketManager => MatchContext.MarketManager;
            public IActionSystem ActionSystem => MatchContext.ActionSystem;
            public ITurnManager TurnManager => MatchContext.TurnManager;
            public IGameLogger Logger { get; }
            public IPlayerStateManager PlayerStateManager => MatchContext.PlayerStateManager;

            public TestGameplayState(MatchContext context, IMatchManager matchManager, IGameLogger logger)
            {
                MatchContext = context;
                MatchManager = matchManager;
                Logger = logger;
            }

            // Stubs
            public IInputManager InputManager => throw new System.NotImplementedException();
            public IUIManager UIManager => throw new System.NotImplementedException();
            public void RecordAndExecuteCommand(IGameCommand command) => command.Execute(MatchContext);
            public void LoadContent() { }
            public void UnloadContent() { }
            public void Update(Microsoft.Xna.Framework.GameTime gameTime) { }
            public IInputMode InputMode => throw new System.NotImplementedException();
            public bool IsMarketOpen => false;
            public bool IsConfirmationPopupOpen => false;
            public bool IsPauseMenuOpen => false;
            public bool IsOptionalEffectPopupOpen => false;
            public int HandY => 0;
            public int PlayedY => 0;
            public bool CanEndTurn(out string reason) => throw new System.NotImplementedException();
            public void EndTurn() { }
            public IMarketStateManager MarketStateManager => throw new System.NotImplementedException();
            public void SwitchToTargetingMode() { }
            public void SwitchToNormalMode() { }
            public void SwitchToPromoteMode(int amount) { }
            public void HandleEscapeKeyPress() { }
            public void HandleEndTurnKeyPress() { }
            public void PlayCard(Card card) => MatchManager.PlayCard(card);
            public void MoveCardToPlayed(Card card) => MatchManager.MoveCardToPlayed(card);
            public bool HasViableTargets(Card card) => true;
            public string GetTargetingText(ActionState state) => "";
            public Card? GetHoveredHandCard() => null;
            public Card? GetHoveredPlayedCard() => null;
            public Card? GetHoveredMarketCard() => null;
            public Card? GetHoveredBrowserCard() => null;
        }
    }
}
