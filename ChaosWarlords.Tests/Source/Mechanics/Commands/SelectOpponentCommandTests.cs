using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    /// <summary>
    /// Unit-level Validate()/Execute() coverage for SelectOpponentCommand - the first "target a
    /// player" primitive in the codebase (see planning.txt TIER 2 #6 / Cranium Rats). Matches
    /// this repo's established per-command depth (AssassinateCommandTests/SupplantCommandTests)
    /// rather than the "1 file per IGameCommand" gap the 3 previously-added commands
    /// (DiscardCardCommand/ReturnOwnSpyCommand/PlayFromMarketCommand) shipped with before being
    /// backfilled (planning.txt TIER 1 finding B).
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class SelectOpponentCommandTests
    {
        private TestGameplayState _state = null!;
        private Player _active = null!;
        private Player _target = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
            _active = TestData.Players.RedPlayer();
            _target = TestData.Players.BluePlayer();

            _state.TurnManager.ActivePlayer.Returns(_active);
            _state.TurnManager.GetPlayerByColor(PlayerColor.Blue).Returns(_target);
            _state.ActionSystem.CurrentState.Returns(ActionState.TargetingOpponentSelect);
        }

        /// <summary>
        /// Builds a source card carrying a SelectOpponent effect with the given threshold
        /// (Amount) and wires it as ActionSystem.PendingCard - SelectOpponentCommand reads the
        /// threshold off PendingCard's own effect data, matching PlayFromMarketStrategy's
        /// established pattern (see SelectOpponentCommand.FindThreshold).
        /// </summary>
        private void SetPendingCardThreshold(int threshold)
        {
            var card = new CardBuilder().WithEffect(EffectType.SelectOpponent, threshold).Build();
            _state.ActionSystem.PendingCard.Returns(card);
        }

        private void SetHandCount(Player player, int count)
        {
            for (int i = 0; i < count; i++)
            {
                player.AddToHand(TestData.Cards.CheapCard());
            }
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenCurrentStateIsNotTargetingOpponentSelect()
        {
            _state.ActionSystem.CurrentState.Returns(ActionState.Normal);
            SetPendingCardThreshold(3);
            SetHandCount(_target, 4);

            var command = new SelectOpponentCommand(PlayerColor.Blue);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenTargetPlayerNotFound()
        {
            _state.TurnManager.GetPlayerByColor(PlayerColor.Black).Returns((Player?)null);
            SetPendingCardThreshold(3);

            var command = new SelectOpponentCommand(PlayerColor.Black);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenTargetIsTheActivePlayer()
        {
            // A player can't be forced to discard against themselves via this primitive - the
            // active player must choose an OPPONENT.
            _state.TurnManager.GetPlayerByColor(_active.Color).Returns(_active);
            SetPendingCardThreshold(0); // Threshold irrelevant - self-targeting is rejected first.
            SetHandCount(_active, 5);

            var command = new SelectOpponentCommand(_active.Color);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenTargetHandCountEqualsThreshold()
        {
            // Boundary value: the threshold is exclusive ("more than 3 cards"), not inclusive -
            // exactly 3 must NOT be eligible.
            SetPendingCardThreshold(3);
            SetHandCount(_target, 3);

            var command = new SelectOpponentCommand(PlayerColor.Blue);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsTrue_WhenTargetHandCountExceedsThresholdByOne()
        {
            SetPendingCardThreshold(3);
            SetHandCount(_target, 4);

            var command = new SelectOpponentCommand(PlayerColor.Blue);

            Assert.IsTrue(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenPendingCardIsNull()
        {
            // FindThreshold falls back to 0 when there's no PendingCard - a target with an
            // empty hand (0 cards, not > 0) must still be rejected.
            _state.ActionSystem.PendingCard.Returns((Card?)null);

            var command = new SelectOpponentCommand(PlayerColor.Blue);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Execute_DoesNothing_WhenTargetPlayerNotFound()
        {
            _state.TurnManager.GetPlayerByColor(PlayerColor.Black).Returns((Player?)null);
            var command = new SelectOpponentCommand(PlayerColor.Black);

            command.Execute(_state.MatchContext);

            _state.TurnManager.DidNotReceive().BeginForcedActingPlayer(Arg.Any<Player>());
            _state.ActionSystem.DidNotReceive().CompleteAction();
        }

        [TestMethod]
        public void Execute_BeginsForcedActingPlayer_ThenCompletesAction_WhenTargetFound()
        {
            // Order matters (see SelectOpponentCommand's own doc comment): BeginForcedActingPlayer
            // must run BEFORE CompleteAction(), so that when CompleteAction() resolves this
            // effect's OnSuccess chain, MatchContext.ActivePlayer already resolves to the chosen
            // opponent.
            var command = new SelectOpponentCommand(PlayerColor.Blue);

            command.Execute(_state.MatchContext);

            Received.InOrder(() =>
            {
                _state.TurnManager.BeginForcedActingPlayer(_target);
                _state.ActionSystem.CompleteAction();
            });
        }
    }
}
