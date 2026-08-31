using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Input.Controllers;
using ChaosWarlords.Source.Input;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NSubstitute;
using System;
using System.Collections.Generic;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Tests.Source.Integration.Input
{
    [TestClass]
    [TestCategory("Integration")]
    public class InputBlockingTests
    {
        private IGameplayState _gameState = null!;
        private IInputManager _inputManager = null!;
        private MatchContext _context = null!;
        private GameplayInputCoordinator _coordinator = null!;

        [TestInitialize]
        public void Setup()
        {
            _gameState = Substitute.For<IGameplayState>();
            _inputManager = Substitute.For<IInputManager>();
            
            var turnManager = Substitute.For<ITurnManager>();
            var mapManager = Substitute.For<IMapManager>();
            var marketManager = Substitute.For<IMarketManager>();
            var actionSystem = Substitute.For<IActionSystem>();
            var logger = Substitute.For<IGameLogger>();
            var marketState = Substitute.For<IMarketStateManager>();
            
            _gameState.MarketStateManager.Returns(marketState);
            _gameState.Logger.Returns(logger);
            
            _context = new MatchContext(
                turnManager,
                mapManager,
                marketManager,
                actionSystem,
                Substitute.For<ICardDatabase>(),
                Substitute.For<IPlayerStateManager>(),
                logger,
                123
            );
            
            // Setup Valid Map Interaction
            var node = new ChaosWarlords.Source.Entities.Map.MapNode(1, new ChaosWarlords.Source.Core.Data.LogicVector2(100 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor, 100 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor)); // Using real node, its safe
            mapManager.GetNodeAt(Arg.Any<ChaosWarlords.Source.Core.Data.LogicVector2>()).Returns(node);
            mapManager.CanDeployAt(node, Arg.Any<PlayerColor>()).Returns(true);
            
            _context.TurnManager.ActivePlayer.Returns(new Player(PlayerColor.Red));

            // Allow coordinator to switch to Normal Mode
            _context.ActionSystem.OnStateChanged += Raise.Event<EventHandler<ActionState>>(this, ActionState.Normal);
            
            _coordinator = new GameplayInputCoordinator(_gameState, _inputManager, _context);
        }

        [TestMethod]
        public void PopupOpen_BlocksGameplayInput()
        {
            // Arrange
            _gameState.IsOptionalEffectPopupOpen.Returns(true);
            
            // Trigger input event
            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100), null);
            
            // Raise the event on the mock manager
            _inputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(this, evt);

            // Assert
            // Coordinator delegates to NormalPlayInputMode which creates commands.
            // If NOT blocked, it would try to record command.
            // If Coordinator BLOCKS, it should NOT record command.
            // Currently (Buggy), it DOES NOT check blocking, so it WILL record command.
            
            // This assertion expects the fix to be present.
            // Running this BEFORE fix should FAIL.
            _gameState.DidNotReceive().RecordAndExecuteCommand(Arg.Any<IGameCommand>());
        }
    }
}
