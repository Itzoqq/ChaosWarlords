using ChaosWarlords.Source.Input.Controllers;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State; 
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Input; 
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Core.Events;
using System;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities; // Added for Enums
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace ChaosWarlords.Tests.Integration.Input.Controllers
{
    [TestClass]
    [TestCategory("Integration")]
    public class PlayerControllerTests
    {
        private PlayerController _controller = null!;
        private TestGameplayState _stateFake = null!;
        private IInputManager _mockInputManager = null!;
        private IActionSystem _mockActionSystem = null!;
        private IInteractionMapper _mockMapper = null!; 
        private IGameplayInputCoordinator _mockCoordinator = null!; 

        [TestInitialize]
        public void Setup()
        {
            _mockInputManager = Substitute.For<IInputManager>();
            _mockActionSystem = Substitute.For<IActionSystem>();
            _mockMapper = Substitute.For<IInteractionMapper>();
            _mockCoordinator = Substitute.For<IGameplayInputCoordinator>();

            _stateFake = new TestGameplayState
            {
                InputManager = _mockInputManager,
                ActionSystem = _mockActionSystem
            };
            
            // Fix Constructor: Pass Coordinator
            _controller = new PlayerController(_stateFake, _mockInputManager, _mockCoordinator, _mockMapper);
        }

        [TestMethod]
        public void Update_DelegatesToInputManagerUpdate()
        {
            // Act
            _controller.Update();

            // Assert
            _mockCoordinator.Received(1).HandleInput();
        }
        
        [TestMethod]
        public void HandleEscapeKey_WhileNotBlocked_DoesNotHandleItDirectly()
        {
            // Escape's gameplay-facing meaning (cancel targeting / close market / open the
            // pause menu when nothing else applies) is exclusively GameplayInputCoordinator's
            // job now, via the active IInputMode - a second, competing handler here (racing
            // against the coordinator on the very same event) was the actual bug. See
            // planning.txt.
            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.Escape);

            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            Assert.IsFalse(_stateFake.EscapeHandled, "PlayerController must not handle Escape itself while nothing is blocking input.");
        }

        [TestMethod]
        public void HandleEnterKey_WhileNotBlocked_DoesNotEndTurnDirectly()
        {
            // Enter's "attempt to end the turn" meaning is exclusively
            // GameplayInputCoordinator's fallback job now (only reached once the active mode
            // itself declines to do anything with the event) - see planning.txt.
            _stateFake.IsPauseMenuOpen = false;
            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.Enter);

            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            Assert.IsFalse(_stateFake.EndTurnRequested, "PlayerController must not end the turn itself while nothing is blocking input.");
        }

        [TestMethod]
        public void HandleSpySelectionInput_WhilePauseMenuOpen_IsBlocked()
        {
            // IsInputBlocked() is the only gate left in PlayerController now (Escape/Enter/
            // popup handling all moved to GameplayInputCoordinator - see planning.txt) - this
            // proves it still correctly short-circuits the 2 responsibilities PlayerController
            // kept (spy/opponent selection) while a blocking overlay is open.
            _stateFake.IsPauseMenuOpen = true;

            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.CurrentState.Returns(ActionState.SelectingSpyToReturn);
            var mockSite = TestData.Sites.CitySite();
            mockActionSystem.PendingSite.Returns(mockSite);
            _stateFake.ActionSystem = mockActionSystem;
            _stateFake.InitializeMatchContext();

            var mockUIManager = Substitute.For<IUIManager>();
            mockUIManager.ScreenWidth.Returns(800);
            _stateFake.UIManager = mockUIManager;

            _mockMapper.GetClickedSpyReturnButton(Arg.Any<Point>(), mockSite, 800)
                .Returns(PlayerColor.Blue);

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            mockActionSystem.DidNotReceive().FinalizeSpyReturn(Arg.Any<PlayerColor>());
        }

        [TestMethod]
        public void HandleOneKey_SelectsCard_Index0()
        {
             // Arrange
            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.D1);

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            // Verify state interaction or UI interaction
        }

        [TestMethod]
        public void HandleRightClick_WhileMarketOpenAndNotBlocked_DoesNotCloseMarketDirectly()
        {
            // Closing the market via right-click is MarketInputMode's job now, via
            // GameplayInputCoordinator - a second, competing handler here (which used to run
            // BEFORE the coordinator's own mode dispatch on the exact same click) was the
            // actual bug. See planning.txt.
            _stateFake.MarketStateManager.OpenForBrowsing();
            var evt = new InputEventArgs(InputEventType.RightClick, Vector2.Zero);

            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            Assert.IsTrue(_stateFake.IsMarketOpen, "PlayerController must not close the market itself while nothing is blocking input.");
        }

        [TestMethod]
        public void HandleRightClick_WhileTargetingAndNotBlocked_DoesNotCancelTargetingDirectly()
        {
            // Cancelling/declining an in-progress targeting sequence via right-click is the
            // active IInputMode's job now, via GameplayInputCoordinator - a second, competing
            // handler here that could mutate ActionSystem BEFORE the mode's own (correct,
            // mode-aware) cancel/decline logic ever ran was the actual bug this whole pass
            // exists to fix. See planning.txt.
            _stateFake.MarketStateManager.Close();

            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.IsTargeting().Returns(true);
            _stateFake.ActionSystem = mockActionSystem; // Inject mock into fake
            _stateFake.InitializeMatchContext(); // Update MatchContext to use new mock

            var evt = new InputEventArgs(InputEventType.RightClick, Vector2.Zero);

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            mockActionSystem.DidNotReceive().CancelTargeting();
        }

        [TestMethod]
        public void HandleSpySelectionInput_RecordsAndExecutesTheResolveSpyCommand()
        {
            // FinalizeSpyReturn only constructs the ResolveSpyCommand - it never mutates state
            // or dispatches it itself, so a valid spy-return-button click must actually end up
            // in ExecutedCommands, not just call FinalizeSpyReturn and stop there.
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.CurrentState.Returns(ActionState.SelectingSpyToReturn);
            var mockSite = TestData.Sites.CitySite();
            mockActionSystem.PendingSite.Returns(mockSite);

            var resolveCommand = new ResolveSpyCommand(mockSite.Id, PlayerColor.Blue);
            mockActionSystem.FinalizeSpyReturn(PlayerColor.Blue).Returns(resolveCommand);

            _stateFake.ActionSystem = mockActionSystem;
            _stateFake.InitializeMatchContext();

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            var mockUIManager = Substitute.For<IUIManager>();
            mockUIManager.ScreenWidth.Returns(800);
            _stateFake.UIManager = mockUIManager;

            _mockMapper.GetClickedSpyReturnButton(Arg.Any<Point>(), mockSite, 800)
                .Returns(PlayerColor.Blue);

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            mockActionSystem.Received(1).FinalizeSpyReturn(PlayerColor.Blue);
            CollectionAssert.Contains(_stateFake.ExecutedCommands, resolveCommand);
        }

        [TestMethod]
        public void HandleSpySelectionInput_WhenFinalizeSpyReturnReturnsNull_DoesNotDispatchAnything()
        {
            // FinalizeSpyReturn returns null when its own internal validation fails (e.g.
            // PendingSite is null by the time the click resolves) - RecordAndExecuteCommand
            // must never be called with a null command in that case.
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.CurrentState.Returns(ActionState.SelectingSpyToReturn);
            var mockSite = TestData.Sites.CitySite();
            mockActionSystem.PendingSite.Returns(mockSite);
            mockActionSystem.FinalizeSpyReturn(PlayerColor.Blue).Returns((IGameCommand?)null);

            _stateFake.ActionSystem = mockActionSystem;
            _stateFake.InitializeMatchContext();

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            var mockUIManager = Substitute.For<IUIManager>();
            mockUIManager.ScreenWidth.Returns(800);
            _stateFake.UIManager = mockUIManager;

            _mockMapper.GetClickedSpyReturnButton(Arg.Any<Point>(), mockSite, 800)
                .Returns(PlayerColor.Blue);

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            Assert.IsEmpty(_stateFake.ExecutedCommands);
        }

        [TestMethod]
        public void HandleSpySelectionInput_WhenClickMissesEveryButton_DoesNothing()
        {
            // A click during SelectingSpyToReturn that doesn't land on any spy-color button
            // (GetClickedSpyReturnButton returns null) is a plain no-op here - it does NOT
            // cancel the targeting sequence. Right-click/Escape remain the only way to abandon
            // the return-spy action; TargetingInputMode itself never reacts to LeftClick during
            // this state at all (see TargetingInputModeTests.cs), so this is the sole real
            // click-handling path for it.
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.CurrentState.Returns(ActionState.SelectingSpyToReturn);
            var mockSite = TestData.Sites.CitySite();
            mockActionSystem.PendingSite.Returns(mockSite);

            _stateFake.ActionSystem = mockActionSystem;
            _stateFake.InitializeMatchContext();

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(0, 0));

            var mockUIManager = Substitute.For<IUIManager>();
            mockUIManager.ScreenWidth.Returns(800);
            _stateFake.UIManager = mockUIManager;

            _mockMapper.GetClickedSpyReturnButton(Arg.Any<Point>(), mockSite, 800)
                .Returns((PlayerColor?)null);

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            mockActionSystem.DidNotReceive().FinalizeSpyReturn(Arg.Any<PlayerColor>());
            mockActionSystem.DidNotReceive().CancelTargeting();
            Assert.IsEmpty(_stateFake.ExecutedCommands);
        }

        [TestMethod]
        public void HandleOpponentSelectionInput_RecordsAndExecutesSelectOpponentCommand()
        {
            // Matches HandleSpySelectionInput_FinalizesSpyReturn's depth/style - the sibling
            // this new method is built alongside (see planning.txt TIER 2 #6 / Cranium Rats).
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.CurrentState.Returns(ActionState.TargetingOpponentSelect);

            var craniumRats = new ChaosWarlords.Tests.CardBuilder()
                .WithEffect(ChaosWarlords.Source.Utilities.EffectType.SelectOpponent, 3)
                .Build();
            mockActionSystem.PendingCard.Returns(craniumRats);

            _stateFake.ActionSystem = mockActionSystem;
            _stateFake.InitializeMatchContext();

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            var mockUIManager = Substitute.For<IUIManager>();
            mockUIManager.ScreenWidth.Returns(800);
            _stateFake.UIManager = mockUIManager;

            _mockMapper.GetClickedOpponentSelectButton(Arg.Any<Point>(), Arg.Any<IReadOnlyList<Player>>(), Arg.Any<Player>(), 3, 800)
                .Returns(PlayerColor.Blue);

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            Assert.IsTrue(
                _stateFake.ExecutedCommands.Any(c => c is ChaosWarlords.Source.Commands.SelectOpponentCommand soc && soc.TargetPlayerColor == PlayerColor.Blue),
                "A SelectOpponentCommand targeting Blue should have been recorded and executed.");
        }

        [TestMethod]
        public void HandleOpponentSelectionInput_ReturnsFalse_WhenNotInTargetingOpponentSelectState()
        {
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.CurrentState.Returns(ActionState.Normal);
            _stateFake.ActionSystem = mockActionSystem;
            _stateFake.InitializeMatchContext();

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            Assert.IsEmpty(_stateFake.ExecutedCommands);
            _mockMapper.DidNotReceive().GetClickedOpponentSelectButton(
                Arg.Any<Point>(), Arg.Any<IReadOnlyList<Player>>(), Arg.Any<Player>(), Arg.Any<int>(), Arg.Any<int>());
        }
    }
}
