using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Commands;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.States.Input;

namespace ChaosWarlords.Tests.States
{
    // Define the Mock Provider inside the Test project
    public class MockInputProvider : IInputProvider
    {
        // Backing fields
        public MouseState MouseState { get; private set; }
        public KeyboardState KeyboardState { get; private set; }

        public MockInputProvider()
        {
            // Initialize with default (Released, 0,0) states
            MouseState = new MouseState();
            KeyboardState = new KeyboardState();
        }

        // Interface Implementation
        public MouseState GetMouseState() => MouseState;
        public KeyboardState GetKeyboardState() => KeyboardState;

        // --- Helper Methods for Tests ---

        public void QueueRightClick()
        {
            MouseState = new MouseState(
                MouseState.X,
                MouseState.Y,
                MouseState.ScrollWheelValue,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Pressed, // Right Click
                ButtonState.Released,
                ButtonState.Released
            );
        }

        public void QueueLeftClick()
        {
            MouseState = new MouseState(
                MouseState.X,
                MouseState.Y,
                MouseState.ScrollWheelValue,
                ButtonState.Pressed, // Left Click
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released
            );
        }

        public void SetMousePosition(int x, int y)
        {
            // Preserve button state if needed, but for simple moves, reset works
            MouseState = new MouseState(
                x,
                y,
                MouseState.ScrollWheelValue,
                MouseState.LeftButton,
                MouseState.MiddleButton,
                MouseState.RightButton,
                MouseState.XButton1,
                MouseState.XButton2
            );
        }

        public void Reset()
        {
            MouseState = new MouseState();
            KeyboardState = new KeyboardState();
        }

        public void SetKeyboardState(params Keys[] keys)
        {
            KeyboardState = new KeyboardState(keys);
        }

        public void SetMouseState(MouseState state)
        {
            MouseState = state;
        }
    }

    public class MockCardDatabase : ICardDatabase
    {
        public List<Card> GetAllMarketCards()
        {
            return new List<Card>(); // Return empty list for tests
        }

        public Card? GetCardById(string id)
        {
            return null;
        }
    }

    [TestClass]
    public class GameplayStateTests
    {
        private GameplayState _state = null!;
        private InputManager _input = null!;
        private MockInputProvider _mockInputProvider = null!;
        private Player _player = null!;
        private TurnManager _turnManager = null!;
        private MapManager _mapManager = null!;
        private MarketManager _marketManager = null!;
        private ActionSystem _actionSystem = null!;
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
            _mutableNodes = new List<MapNode>(); // <--- FIX 1: Initialize mutable list
            _mutableSites = new List<Site>();   // <--- FIX 1: Initialize mutable list
                                                // _mapManager = new MapManager(new List<MapNode>(), new List<Site>()); // OLD
            _mapManager = new MapManager(_mutableNodes, _mutableSites); // <--- FIX 2: Pass mutable lists to constructor
            _marketManager = new MarketManager();

            // 4b. Setup Action System - Needs the ActivePlayer from the TurnManager
            _actionSystem = new ActionSystem(_turnManager.ActivePlayer, _mapManager);

            // We must create a real UIManager for tests
            var testUiManager = new UIManager(800, 600);

            // Create the Mock DB
            var mockDb = new MockCardDatabase();

            // Pass it to the constructor
            _state = new GameplayState(null!, _mockInputProvider, mockDb);

            // FIX: Pass the TurnManager object instead of _player
            _state.InjectDependencies(_input, testUiManager, _mapManager, _marketManager, _actionSystem, _turnManager);
        }

        [TestMethod]
        public void EndTurn_ResetsResources_AndDrawsCards()
        {
            // FIX: Use ActivePlayer from TurnManager
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
            // FIX: Use ActivePlayer from TurnManager
            _turnManager.ActivePlayer.Power = 10;
            _state.HandleGlobalInput();

            // Assert
            // FIX: Use ActivePlayer from TurnManager
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
        public void UpdateMarketLogic_ClosesMarket_WhenClickedOutside()
        {
            // This tests that if we click but NO command is generated (clicked empty space), market closes.
            _state.IsMarketOpen = true;

            // Simulate a click that hits nothing (UIManager will return null)
            _mockInputProvider.Reset();
            _input.Update();
            _mockInputProvider.QueueLeftClick();
            _input.Update();

            // Act
            // We call UpdateMarketLogic directly as per the original test structure
            _state.UpdateMarketLogic();

            // Assert
            Assert.IsFalse(_state.IsMarketOpen, "Market should close if clicked outside/no command executed.");
        }

        [TestMethod]
        public void Command_BuyCard_BuysCard_WhenAffordable()
        {
            // Arrange
            _state.IsMarketOpen = true;
            // FIX: Use ActivePlayer from TurnManager
            _turnManager.ActivePlayer.Influence = 10;

            var cardToBuy = new Card("market_card", "Buy Me", 3, CardAspect.Sorcery, 1, 0);
            _marketManager.MarketRow.Clear();
            _marketManager.MarketRow.Add(cardToBuy);

            // Act: Execute the command directly
            var command = new BuyCardCommand(cardToBuy);
            command.Execute(_state);

            // Assert
            // FIX: Use ActivePlayer from TurnManager
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
            // FIX: Use ActivePlayer from TurnManager
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
            // Set the backend system to "Targeting"
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, CardFactory.CreateSoldier());

            // Set the frontend Input Mode to match (simulating a real game state)
            _state.InputMode = new TargetingInputMode(
                _state,
                _input,
                _mapManager,
                _turnManager,
                _actionSystem
            );

            // 2. ACT
            // Queue a Right Click in the Mock Provider
            _mockInputProvider.QueueRightClick();

            // Run a full Game Loop Frame
            // This calls InputManager.Update() -> HandleGlobalInput() -> CancelTargeting()
            _state.Update(new GameTime());

            // 3. ASSERT
            // Check Backend (The Data)
            Assert.IsFalse(_actionSystem.IsTargeting(), "Backend state should return to Normal.");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);

            // Check Frontend (The UI/Input) - This is the "Integration" check
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
            // 1. Arrange
            // FIX: Use ActivePlayer from TurnManager
            var card = _turnManager.ActivePlayer.Hand[_turnManager.ActivePlayer.Hand.Count - 1];

            // 2. Act: Call Logic Directly
            _state.PlayCard(card);

            // 3. Assert
            // FIX: Use ActivePlayer from TurnManager
            Assert.DoesNotContain(card, _turnManager.ActivePlayer.Hand, "The clicked card should be removed from Hand.");
            Assert.Contains(card, _turnManager.ActivePlayer.PlayedCards, "The clicked card should be in PlayedCards.");
        }

        [TestMethod]
        public void Update_PressingEnter_EndsTurn()
        {
            // 1. Arrange
            // FIX: Use ActivePlayer from TurnManager
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

            // FIX: Use ActivePlayer from TurnManager
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
            // FIX: Use ActivePlayer from TurnManager
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
            // FIX: Use ActivePlayer from TurnManager
            _turnManager.ActivePlayer.Hand.Add(assassin);

            // Ensure the system starts from a known 'None' state.
            _actionSystem.CurrentState = ActionState.Normal;

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
    }
}