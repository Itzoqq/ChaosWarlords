using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards; // Added
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NSubstitute;
using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Data;
using System.Collections.Generic;
using System;

namespace ChaosWarlords.Tests.Integration.Input.Modes
{
    [TestClass]
    [TestCategory("Integration")]
    public class MarketInputModeTests
    {
        private MarketInputMode _mode = null!;
        private TestGameplayState _stateFake = null!;
        private IInputManager _mockInputManager = null!; // Renamed
        private IActionSystem _mockActionSystem = null!; // Renamed
        private IMarketManager _marketSub = null!;
        private IMapManager _mapSub = null!;
        private IUIManager _mockUI = null!; // Renamed
        private Player _activePlayer = null!;
        
        // Context dependencies
        private ITurnManager _turnManagerSub = null!;
        private IGameLogger _loggerSub = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInputManager = Substitute.For<IInputManager>();
            _mockActionSystem = Substitute.For<IActionSystem>();
            _marketSub = Substitute.For<IMarketManager>();
            _mapSub = Substitute.For<IMapManager>();
            _mockUI = Substitute.For<IUIManager>();
            _loggerSub = Substitute.For<IGameLogger>();
            _turnManagerSub = Substitute.For<ITurnManager>();

            // Setup MatchContext for State
            var cardDbMsg = Substitute.For<ICardDatabase>();
            var psMsg = new PlayerStateManager(_loggerSub);
            
            var matchContext = new MatchContext(
                _turnManagerSub,
                _mapSub,
                _marketSub,
                _mockActionSystem,
                cardDbMsg,
                psMsg,
                null, 
                _loggerSub
            );

            _stateFake = new TestGameplayState
            {
               ActionSystem = _mockActionSystem,
               MatchContext = matchContext,
               UIManager = _mockUI
            };

            _activePlayer = new Player(PlayerColor.Red);
            
            _mode = new MarketInputMode(_stateFake, _mockInputManager, matchContext);

            // Pump update loop to clear startup cooldown (COOLDOWN_FRAMES)
            for (int i = 0; i < 10; i++)
            {
                // HandleUpdate is responsible for cooldowns in MarketInputMode
                _mode.HandleUpdate(_mockInputManager, _mapSub, _activePlayer);
            }
        }

        [TestMethod]
        public void HandleInteraction_ClickingCard_ReturnsBuyCardCommand()
        {
            // 1. Arrange
            var card = TestData.Cards.PowerCard();

            // Mock the State to say "Yes, the mouse is hovering this card"
            _stateFake.HoveredMarketCard = card;

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(110, 110));

            // Force update frame counter 
            for(int i=0; i<10; i++) _mode.HandleUpdate(_mockInputManager, _mapSub, _activePlayer);
            
            // 2. Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _mockActionSystem);

            // 3. Assert
            Assert.IsNotNull(result, "Clicking a market card should return a command.");
            Assert.IsInstanceOfType(result, typeof(BuyCardCommand), "Should return a BuyCardCommand.");
        }

        [TestMethod]
        public void HandleInteraction_ClickingEmptySpace_ClosesMarket()
        {
            // 1. Arrange
            _stateFake.HoveredMarketCard = null;
            _stateFake.MarketStateManager.OpenForBrowsing(); // Ensure it's open initially

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(10, 10));

             // Pump
            for(int i=0; i<10; i++) _mode.HandleUpdate(_mockInputManager, _mapSub, _activePlayer);

            // 2. Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _mockActionSystem);

            // 3. Assert
            Assert.IsNull(result);
            Assert.IsFalse(_stateFake.IsMarketOpen, "Market should be closed after clicking empty space.");
        }

        [TestMethod]
        public void HandleInteraction_ClickingMarketButton_DoesNotCloseMarket()
        {
            _mockUI.IsMarketHovered.Returns(true);
            _stateFake.MarketStateManager.OpenForBrowsing();

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(10, 10));

             // Pump
            for(int i=0; i<10; i++) _mode.HandleUpdate(_mockInputManager, _mapSub, _activePlayer);

            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _mockActionSystem);

            Assert.IsNull(result);
            Assert.IsTrue(_stateFake.IsMarketOpen, "Market should remain open if UI button is clicked.");
        }

        [TestMethod]
        public void HandleInteraction_WithCallback_InvokesCallback_WhenCardClicked()
        {
            // 1. Arrange
            Card? callbackInvokedCard = null;
            Func<Card, IGameCommand?> onCardSelected = (c) => { callbackInvokedCard = c; return null; };

            // Set up state with callback
            _stateFake.MarketStateManager.OpenForDevour(onCardSelected);

            // Re-initialize (parameterless)
            _mode = new MarketInputMode(_stateFake, _mockInputManager, _stateFake.MatchContext);
            // Pump
            for(int i=0; i<10; i++) _mode.HandleUpdate(_mockInputManager, _mapSub, _activePlayer);

            var card = TestData.Cards.PowerCard();
            _stateFake.HoveredMarketCard = card;

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(110, 110));

            // 2. Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _mockActionSystem);

            // 3. Assert
            Assert.IsNull(result, "Should return null command when callback is handled.");
            Assert.AreEqual(card, callbackInvokedCard, "The callback should be invoked with the clicked card.");
        }
    }
}
