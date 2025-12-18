using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Commands;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.States.Input;

namespace ChaosWarlords.Tests.States
{
    [TestClass]
    public class GameplayStateTests
    {
        private GameplayState _state = null!;
        private InputManager _input = null!;
        private MockInputProvider _mockInputProvider = null!;
        private Player _player = null!;
        private TurnManager _turnManager = null!;
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private UIManager _uiManager = null!;
        private IActionSystem _actionSystem = null!;
        private List<MapNode> _mutableNodes = null!;
        private List<Site> _mutableSites = null!;

        [TestInitialize]
        public void Setup()
        {
            // 1. Create the Input Mock
            _mockInputProvider = new MockInputProvider();

            // 2. Inject Input Mock into InputManager
            _input = new InputManager(_mockInputProvider);

            // 3. Setup Player (Red - Active Player in tests)
            _player = new Player(PlayerColor.Red);
            for (int i = 0; i < 10; i++)
            {
                _player.Deck.Add(CardFactory.CreateSoldier());
            }
            _player.DrawCards(5);

            // Setup Turn Manager
            _turnManager = new TurnManager(new List<Player> { _player });

            // 4. Setup other Systems
            _mutableNodes = new List<MapNode>();
            _mutableSites = new List<Site>();

            _mapManager = new MapManager(_mutableNodes, _mutableSites);
            _marketManager = new MarketManager();

            // 4b. Setup Action System - Needs the ActivePlayer from the TurnManager
            _actionSystem = new ActionSystem(_turnManager.ActivePlayer, (MapManager)_mapManager);

            // CHANGE: Assign to the class field instead of 'var testUiManager'
            _uiManager = new UIManager(800, 600);

            // Create the Mock DB
            var mockDb = new MockCardDatabase();

            _state = new GameplayState(null!, _mockInputProvider, mockDb);

            // CHANGE: Use the field here
            _state.InjectDependencies(_input, _uiManager, _mapManager, _marketManager, _actionSystem, _turnManager);
        }

        [TestMethod]
        public void EndTurn_ResetsResources_AndDrawsCards()
        {
            // Use ActivePlayer from TurnManager
            var activePlayer = _turnManager.ActivePlayer;

            activePlayer.Hand.Clear();
            activePlayer.Deck.Clear();
            activePlayer.DiscardPile.Clear();
            activePlayer.PlayedCards.Clear();

            // Add 5 cards so the logic can actually find 5 to draw
            for (int i = 0; i < 5; i++)
            {
                activePlayer.Deck.Add(CardFactory.CreateSoldier());
            }

            activePlayer.Power = 5;

            // 2. Act
            _state.EndTurn();

            // 3. Assert
            Assert.AreEqual(0, activePlayer.Power, "Power should reset to 0.");
            Assert.HasCount(5, activePlayer.Hand, "Should draw exactly 5 cards.");
        }

        [TestMethod]
        public void HandleGlobalInput_EnterKey_EndsTurn()
        {
            // Step 1: Ensure Key is UP (Previous Frame)
            _mockInputProvider.SetKeyboardState();
            _input.Update();

            // Step 2: Press Key DOWN (Current Frame)
            _mockInputProvider.SetKeyboardState(Keys.Enter);
            _input.Update();

            // Act
            // Use ActivePlayer from TurnManager
            _turnManager.ActivePlayer.Power = 10;
            _state.HandleGlobalInput();

            // Assert
            // Use ActivePlayer from TurnManager
            Assert.AreEqual(0, _turnManager.ActivePlayer.Power, "Power should be 0 after EndTurn triggered by Enter key.");
        }

        [TestMethod]
        public void PlayCard_ResolvesResourceEffects()
        {
            var card = new Card("test", "Resource Card", 0, CardAspect.Neutral, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 3, ResourceType.Power));
            // Use ActivePlayer from TurnManager
            _turnManager.ActivePlayer.Hand.Add(card);

            _state.PlayCard(card);

            // Use ActivePlayer from TurnManager
            Assert.AreEqual(3, _turnManager.ActivePlayer.Power);
            Assert.Contains(card, _turnManager.ActivePlayer.PlayedCards);
            Assert.DoesNotContain(card, _turnManager.ActivePlayer.Hand);
        }

        [TestMethod]
        public void PlayCard_TriggersTargeting_SwitchInputMode()
        {
            var card = new Card("kill", "Assassin", 0, CardAspect.Shadow, 0, 0);
            card.AddEffect(new CardEffect(EffectType.Assassinate, 1));

            // Ensure we start in Normal
            _state.SwitchToNormalMode();

            // Act
            _state.PlayCard(card);

            // Assert
            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState);

            // THE MISSING CHECK:
            Assert.IsInstanceOfType(_state.InputMode, typeof(TargetingInputMode),
                "Playing a targeting card must switch the Input Mode.");
        }

        [TestMethod]
        public void Command_BuyCard_BuysCard_WhenAffordable()
        {
            // Arrange
            _state.IsMarketOpen = true;
            // Use ActivePlayer from TurnManager
            _turnManager.ActivePlayer.Influence = 10;

            var cardToBuy = new Card("market_card", "Buy Me", 3, CardAspect.Sorcery, 1, 0);
            _marketManager.MarketRow.Clear();
            _marketManager.MarketRow.Add(cardToBuy);

            // Act: Execute the command directly
            var command = new BuyCardCommand(cardToBuy);
            command.Execute(_state);

            // Assert
            // Use ActivePlayer from TurnManager
            Assert.AreEqual(7, _turnManager.ActivePlayer.Influence, "Influence should decrease by cost (10 - 3 = 7).");
            Assert.Contains(cardToBuy, _turnManager.ActivePlayer.DiscardPile, "Card should be moved to discard pile.");
            Assert.DoesNotContain(cardToBuy, _marketManager.MarketRow, "Card should be removed from market row.");
        }

        [TestMethod]
        public void MoveCardToPlayed_PreservesXPosition_AndPopsUpY()
        {
            // Arrange: Setup the "Screen" logic manually since we have no GraphicsDevice
            _state._handYBacking = 500;
            _state._playedYBacking = 400; // The "Pop Up" target
            // Use ActivePlayer from TurnManager
            var activePlayer = _turnManager.ActivePlayer;

            // Create 2 cards in hand
            var card1 = new Card("c1", "Left Card", 0, CardAspect.Neutral, 0, 0);
            var card2 = new Card("c2", "Right Card", 0, CardAspect.Neutral, 0, 0);

            // Manually position them like they would be on screen
            card1.Position = new Vector2(100, 500); // X=100, Y=HandY
            card2.Position = new Vector2(250, 500); // X=250, Y=HandY

            activePlayer.Hand.Add(card1);
            activePlayer.Hand.Add(card2);

            // Act: Play the first card
            _state.MoveCardToPlayed(card1);

            // Assert 1: Card1 should have moved UP (Y changes) but NOT sideways (X stays 100)
            Assert.AreEqual(100, card1.Position.X, "Played card should maintain its X position (Visual Gap).");
            Assert.AreEqual(400, card1.Position.Y, "Played card should move to the Played Y position.");

            // Assert 2: Card2 should NOT have moved at all (Prevents the 'sliding' bug)
            Assert.AreEqual(250, card2.Position.X, "Remaining cards should not slide over.");
            Assert.AreEqual(500, card2.Position.Y, "Remaining cards should stay in the hand row.");

            // Assert 3: Lists are correct
            Assert.Contains(card1, activePlayer.PlayedCards);
            Assert.DoesNotContain(card1, activePlayer.Hand);
        }

        [TestMethod]
        public void Update_RightClick_CancelsTargeting_AndResetsInputMode()
        {
            // 1. ARRANGE
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, CardFactory.CreateSoldier());

            // Pass the _uiManager dependency
            _state.InputMode = new TargetingInputMode(
                _state,
                _input,
                _uiManager,
                _mapManager,
                _turnManager,
                _actionSystem
            );

            // 2. ACT
            _mockInputProvider.QueueRightClick();

            // Run a full Game Loop Frame
            _state.Update(new GameTime());

            // 3. ASSERT
            Assert.IsFalse(_actionSystem.IsTargeting(), "Backend state should return to Normal.");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);

            Assert.IsInstanceOfType(_state.InputMode, typeof(NormalPlayInputMode),
                "Right-clicking should switch the Input Processor back to Normal Mode.");
        }

        [TestMethod]
        public void Command_ToggleMarket_TogglesState()
        {
            // 1. Arrange
            _state.IsMarketOpen = true;

            // 2. Act: Execute Command
            var command = new ToggleMarketCommand();
            command.Execute(_state);

            // 3. Assert
            Assert.IsFalse(_state.IsMarketOpen, "Toggle command should close the market if open.");
        }

        [TestMethod]
        public void PlayCard_Directly_PlaysCard()
        {
            var card = _turnManager.ActivePlayer.Hand[_turnManager.ActivePlayer.Hand.Count - 1];
            int initialCount = _turnManager.ActivePlayer.Hand.Count;

            _state.PlayCard(card);

            Assert.HasCount(initialCount - 1, _turnManager.ActivePlayer.Hand);
            Assert.Contains(card, _turnManager.ActivePlayer.PlayedCards);
        }

        [TestMethod]
        public void Update_PressingEnter_EndsTurn()
        {
            // 1. Arrange
            // Use ActivePlayer from TurnManager
            var activePlayer = _turnManager.ActivePlayer;
            int initialDeckCount = activePlayer.Deck.Count;
            // Play a card so we have something to clean up
            var card = activePlayer.Hand[0];
            _state.PlayCard(card);

            // 2. Act: Press Enter
            _mockInputProvider.SetKeyboardState(Keys.Enter);
            _state.Update(new GameTime());

            // 3. Assert
            Assert.IsEmpty(activePlayer.PlayedCards, "Played cards should be discarded/cleaned up.");
            Assert.HasCount(5, activePlayer.Hand, "Player should have drawn a new hand of 5.");
        }

        [TestMethod]
        public void Command_DeployTroop_DeploysTroop()
        {
            // 1. Arrange
            var node = new MapNode(1, new Vector2(500, 500));
            var baseNode = new MapNode(2, new Vector2(550, 550));
            baseNode.Occupant = PlayerColor.Red;

            node.Neighbors.Add(baseNode);
            baseNode.Neighbors.Add(node);

            _mutableNodes.Add(node);
            _mutableNodes.Add(baseNode);

            // Use ActivePlayer from TurnManager
            var activePlayer = _turnManager.ActivePlayer;
            activePlayer.TroopsInBarracks = 1;
            activePlayer.Power = 1;

            // 2. Act: Execute Command
            var command = new DeployTroopCommand(node);
            command.Execute(_state);

            // 3. Assert
            Assert.AreEqual(PlayerColor.Red, node.Occupant, "Command should deploy a Red troop.");
        }

        [TestMethod]
        public void PlayCard_GainsResources()
        {
            // 1. Arrange
            // Use ActivePlayer from TurnManager
            var activePlayer = _turnManager.ActivePlayer;
            activePlayer.Influence = 0;
            activePlayer.Power = 0;

            var richNoble = new Card("rich_noble", "Rich Noble", 0, CardAspect.Order, 0, 0);
            richNoble.Effects.Add(new CardEffect(EffectType.GainResource, 3, ResourceType.Influence));

            activePlayer.Hand.Add(richNoble);

            // 2. Act
            _state.PlayCard(richNoble);

            // 3. Assert
            Assert.AreEqual(3, activePlayer.Influence, "Playing the card should grant 3 Influence.");
        }

        [TestMethod]
        // New name reflects the test's true purpose: verifying the state is ready for the input handler.
        public void Logic_PlayTargetedActionCard_SetsActionStateCorrectly()
        {
            // 1. Arrange
            // We only need the card that triggers the state change.
            var assassin = new Card("assassin", "Assassin", 0, CardAspect.Shadow, 0, 0);
            assassin.Effects.Add(new CardEffect(EffectType.Assassinate, 0));

            // Ensure active player has it (assuming _turnManager.ActivePlayer is set up)
            // Use ActivePlayer from TurnManager
            _turnManager.ActivePlayer.Hand.Add(assassin);

            // Ensure the system starts from a known 'None' state.
            _actionSystem.CancelTargeting();

            // 2. Act
            _state.PlayCard(assassin);

            // 3. Assert
            // The responsibility of GameplayState is to transition the state.
            // The validation and execution logic of the target click is now in TargetingInputMode.
            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState, "Playing a targeted action card should move the ActionSystem to the correct targeting state.");

            // NOTE: The logic for target validation, execution, and cleanup must be moved 
            // to the new 'TargetingInputModeTests.cs' file.
        }

        [TestMethod]
        public void ToggleMarket_SwitchesInputMode()
        {
            // Arrange
            _state.SwitchToNormalMode(); // Ensure we start in Normal
            Assert.IsInstanceOfType(_state.InputMode, typeof(NormalPlayInputMode));

            // Act
            _state.ToggleMarket();

            // Assert
            // 1. Check the Flag (Visuals)
            Assert.IsTrue(_state.IsMarketOpen);

            // 2. CRITICAL: Check the Object Type (Logic)
            // This assertion would have FAILED on your old code
            Assert.IsInstanceOfType(_state.InputMode, typeof(MarketInputMode),
                "Opening the market must switch the input processor to MarketInputMode.");
        }

        [TestMethod]
        public void LoadContent_SubscribesToEvents()
        {
            // 1. Arrange: Create local mocks specifically for this test
            // We can't use the class fields (_mapManager) because they are 'Real' objects,
            // and we need 'Mock' objects to simulate the events manually.
            var mockMapManager = new MockMapManager();
            var mockMarketManager = new MockMarketManager();
            var mockActionSystem = new MockActionSystem();

            // Use TestableGameplayState (which exposes the dependency injection logic)
            // We pass the LOCAL mocks we just created
            var state = new TestableGameplayState(
                null!,
                _mockInputProvider, // This field DOES exist in this class
                new MockCardDatabase(),
                mockMapManager,
                mockMarketManager,
                mockActionSystem,
                _turnManager // This field DOES exist in this class
            );

            // Inject the dependencies using the LOCAL mocks
            state.InjectDependencies(
                _input, // Field exists
                _uiManager, // Field exists
                mockMapManager,
                mockMarketManager,
                mockActionSystem,
                _turnManager
            );

            // 2. Pre-Assert: Verify we start in Normal Mode
            state.SwitchToNormalMode();
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode));

            // 3. Act: Simulate the event firing on our LOCAL mock
            mockActionSystem.SimulateActionCompleted();

            // 4. Assert
            // To verify the event was caught, let's verify the state transition.
            // First, force it into Targeting Mode manually to ensure a change happens.
            state.InputMode = new TargetingInputMode(state, _input, _uiManager, mockMapManager, _turnManager, mockActionSystem);
            Assert.IsInstanceOfType(state.InputMode, typeof(TargetingInputMode), "Setup failed: should be in targeting mode.");

            // Fire the event again
            mockActionSystem.SimulateActionCompleted();

            // The handler should have switched us back to Normal Mode
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode),
                "Integration Failure: GameplayState did not listen to ActionSystem.OnActionCompleted event!");
        }

        // --- HELPER CLASS ---
        // This class inherits from GameplayState to test the REAL logic,
        // but provides a convenient constructor for the test harness.
        private class TestableGameplayState : GameplayState
        {
            public TestableGameplayState(
                Game game,
                IInputProvider input,
                ICardDatabase db,
                IMapManager map,
                IMarketManager market,
                IActionSystem action,
                TurnManager turn)
                : base(game, input, db)
            {
                // We rely on InjectDependencies in the test, 
                // but this constructor signature allows the test code to compile 
                // matching your requested structure.
            }
        }
    }
}