using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.GameStates;
using ChaosWarlords.Source.Rendering.UI;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Tests.Integration.GameStates
{
    [TestClass]
    [TestCategory("Integration")]
    public class MatchSetupStateTests
    {
        [TestMethod]
        public void LoadContent_CyclesSelectedHalfDeckWithoutAllowingDuplicates()
        {
            var buttons = Substitute.For<IButtonManager>();
            SimpleButton? firstHalfDeckButton = null;
            buttons.When(manager => manager.AddButton(Arg.Any<SimpleButton>())).Do(call =>
            {
                var button = call.Arg<SimpleButton>();
                if (button.Text.StartsWith("First half-deck:")) firstHalfDeckButton = button;
            });
            var state = CreateState(buttons, Substitute.For<IStateManager>());

            state.LoadContent();
            firstHalfDeckButton!.OnClick();

            // Default is Drow+Dragons - cycling First skips Dragons (the other selected half-
            // deck) and lands on Elemental, the next MarketHalfDeck enum value.
            Assert.AreEqual(MarketHalfDeck.Elemental, state.MarketDeckSelection.First);
            Assert.AreNotEqual(state.MarketDeckSelection.First, state.MarketDeckSelection.Second);
            Assert.AreEqual("First half-deck: Elemental", firstHalfDeckButton.Text);
        }

        [TestMethod]
        public void LoadContent_WithOnlyDrowAndDragonsComplete_CyclingFirstHalfDeckStaysOnDrow()
        {
            // planning.txt TIER 1 item 14: with only Drow+Dragons reporting as a real, complete
            // 40-card half-deck, cycling "First" while Second=Dragons has nowhere else to go -
            // it must land back on Drow, never offer an incomplete half-deck like Elemental.
            var cardDatabase = Substitute.For<ICardDatabase>();
            cardDatabase.GetCompleteHalfDecks().Returns(new HashSet<MarketHalfDeck> { MarketHalfDeck.Drow, MarketHalfDeck.Dragons });
            var buttons = Substitute.For<IButtonManager>();
            SimpleButton? firstHalfDeckButton = null;
            buttons.When(manager => manager.AddButton(Arg.Any<SimpleButton>())).Do(call =>
            {
                var button = call.Arg<SimpleButton>();
                if (button.Text.StartsWith("First half-deck:")) firstHalfDeckButton = button;
            });
            var state = CreateState(buttons, Substitute.For<IStateManager>(), cardDatabase);

            state.LoadContent();
            firstHalfDeckButton!.OnClick();

            Assert.AreEqual(MarketHalfDeck.Drow, state.MarketDeckSelection.First);
        }

        [TestMethod]
        public void StartMatch_WithAnIncompleteHalfDeckSelected_RefusesToChangeState()
        {
            // Defense in depth alongside the cycling restriction above - if the current
            // selection is somehow incomplete when Start Match is pressed, it must not build a
            // real match from it.
            var cardDatabase = Substitute.For<ICardDatabase>();
            cardDatabase.GetCompleteHalfDecks().Returns(new HashSet<MarketHalfDeck> { MarketHalfDeck.Elemental });
            var buttons = Substitute.For<IButtonManager>();
            SimpleButton? startButton = null;
            buttons.When(manager => manager.AddButton(Arg.Any<SimpleButton>())).Do(call =>
            {
                var button = call.Arg<SimpleButton>();
                if (button.Text == "Start Match") startButton = button;
            });
            var stateManager = Substitute.For<IStateManager>();
            var state = CreateState(buttons, stateManager, cardDatabase);

            state.LoadContent();
            startButton!.OnClick();

            stateManager.DidNotReceive().ChangeState(Arg.Any<GameplayState>());
        }

        [TestMethod]
        public void StartMatch_WithNoCompleteHalfDecksAtAll_RefusesToChangeState()
        {
            // Pins the fail-closed contract for a catastrophically broken catalog (e.g. a bad
            // cards.json edit regressing every half-deck below 40 at once, including the shipped
            // Default) - GetCompleteHalfDecks reporting genuinely zero complete half-decks must
            // block Start Match exactly like reporting a non-empty-but-insufficient set does, not
            // be treated as "this database has no opinion, allow anything."
            var cardDatabase = Substitute.For<ICardDatabase>();
            cardDatabase.GetCompleteHalfDecks().Returns(new HashSet<MarketHalfDeck>());
            var buttons = Substitute.For<IButtonManager>();
            SimpleButton? startButton = null;
            buttons.When(manager => manager.AddButton(Arg.Any<SimpleButton>())).Do(call =>
            {
                var button = call.Arg<SimpleButton>();
                if (button.Text == "Start Match") startButton = button;
            });
            var stateManager = Substitute.For<IStateManager>();
            var state = CreateState(buttons, stateManager, cardDatabase);

            state.LoadContent();
            startButton!.OnClick();

            stateManager.DidNotReceive().ChangeState(Arg.Any<GameplayState>());
        }

        [TestMethod]
        public void StartMatch_UsesConfirmedHalfDeckSelectionToCreateGameplayState()
        {
            var buttons = Substitute.For<IButtonManager>();
            SimpleButton? startButton = null;
            buttons.When(manager => manager.AddButton(Arg.Any<SimpleButton>())).Do(call =>
            {
                var button = call.Arg<SimpleButton>();
                if (button.Text == "Start Match") startButton = button;
            });
            var stateManager = Substitute.For<IStateManager>();
            var state = CreateState(buttons, stateManager);

            state.LoadContent();
            startButton!.OnClick();

            stateManager.Received(1).ChangeState(Arg.Any<GameplayState>());
        }

        [TestMethod]
        public void Update_WaitsForInitialReleaseThenRoutesFreshClicksToButtons()
        {
            var buttons = Substitute.For<IButtonManager>();
            var input = Substitute.For<IInputProvider>();
            input.GetMouseState().Returns(
                new MouseState(0, 0, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
                new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
                new MouseState(10, 20, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
                new MouseState(10, 20, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            var state = new MatchSetupState(Substitute.For<Game1>(Utilities.TestLogger.Instance), input, Substitute.For<IStateManager>(),
                Substitute.For<ICardDatabase>(), Substitute.For<IReplayManager>(), Utilities.TestLogger.Instance, null, buttons);

            state.LoadContent();
            state.Update(new GameTime());
            state.Update(new GameTime());
            state.Update(new GameTime());
            state.Update(new GameTime());

            buttons.Received().Update(new Point(10, 20), true);
        }

        private static MatchSetupState CreateState(IButtonManager buttons, IStateManager stateManager, ICardDatabase? cardDatabase = null) => new(
            Substitute.For<Game1>(Utilities.TestLogger.Instance), Substitute.For<IInputProvider>(), stateManager,
            cardDatabase ?? PermissiveCardDatabase(), Substitute.For<IReplayManager>(), Utilities.TestLogger.Instance, null, buttons);

        // GetCompleteHalfDecks() has no automatic "unconfigured mock" fallback (MatchSetupState
        // trusts whatever it's told at face value - see its own doc comment) - a test that
        // doesn't care about half-deck completeness must say so explicitly, same as a real
        // ICardDatabase implementer that doesn't override the interface's own default method.
        private static ICardDatabase PermissiveCardDatabase()
        {
            var cardDatabase = Substitute.For<ICardDatabase>();
            cardDatabase.GetCompleteHalfDecks().Returns(new HashSet<MarketHalfDeck>(Enum.GetValues<MarketHalfDeck>()));
            return cardDatabase;
        }
    }
}
