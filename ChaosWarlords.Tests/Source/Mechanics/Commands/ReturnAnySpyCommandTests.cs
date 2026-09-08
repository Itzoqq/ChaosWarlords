using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    // Unit-level Validate()/Execute() coverage for ReturnAnySpyCommand (EffectType.
    // ReturnUnitOrSpy's spy-side half, Intellect Devourer) - mirrors ReturnOwnSpyCommandTests.cs's
    // shape, plus the own-vs-enemy Power-cost branch ResolveSpyCommand/ReturnOwnSpyCommand each
    // only handle one side of.
    [TestClass]
    [TestCategory("Unit")]
    public class ReturnAnySpyCommandTests
    {
        private TestGameplayState _state = null!;
        private ChaosWarlords.Source.Entities.Actors.Player _player = null!;
        private Site _targetSite = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
            _player = new PlayerBuilder().WithColor(PlayerColor.Red).WithPower(5).Build();
            _state.TurnManager.ActivePlayer.Returns(_player);

            _targetSite = TestData.Sites.PowerCity();
            _targetSite.Id = 1;
            _state.MapManager.Sites.Returns(new List<Site> { _targetSite });
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenTargetSiteNotFound()
        {
            var command = new ReturnAnySpyCommand(targetSiteId: 999, PlayerColor.Blue);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_DelegatesToMapManager_CanReturnAnySpy_ReturnsTrue()
        {
            _state.MapManager.CanReturnAnySpy(_targetSite, _player, PlayerColor.Blue).Returns(true);
            var command = new ReturnAnySpyCommand(_targetSite.Id, PlayerColor.Blue);

            Assert.IsTrue(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_DelegatesToMapManager_CanReturnAnySpy_ReturnsFalse()
        {
            // e.g. no spy of that color at the site, or an enemy spy with no Presence.
            _state.MapManager.CanReturnAnySpy(_targetSite, _player, PlayerColor.Blue).Returns(false);
            var command = new ReturnAnySpyCommand(_targetSite.Id, PlayerColor.Blue);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Execute_WhenTargetSiteNotFound_DoesNothing()
        {
            var command = new ReturnAnySpyCommand(targetSiteId: 999, PlayerColor.Blue);

            command.Execute(_state.MatchContext);

            _state.MapManager.DidNotReceive().ReturnAnySpy(Arg.Any<Site>(), Arg.Any<ChaosWarlords.Source.Entities.Actors.Player>(), Arg.Any<PlayerColor>());
            _state.ActionSystem.DidNotReceive().CompleteAction();
        }

        [TestMethod]
        public void Execute_WhenReturnAnySpyFails_NotifiesFailureAndDoesNotCompleteAction()
        {
            _state.MapManager.ReturnAnySpy(_targetSite, _player, PlayerColor.Blue).Returns(false);
            var command = new ReturnAnySpyCommand(_targetSite.Id, PlayerColor.Blue);

            command.Execute(_state.MatchContext);

            _state.ActionSystem.DidNotReceive().CompleteAction();
            _state.ActionSystem.Received(1).NotifyFailure(Arg.Any<string>());
        }

        [TestMethod]
        public void Execute_ReturningAnEnemySpyWithoutACard_SpendsPowerAndCompletesAction()
        {
            // The base "Return an enemy spy" 3-Power cost still applies when this command isn't
            // funded by a card - matching ResolveSpyCommand's identical rule.
            _state.MapManager.ReturnAnySpy(_targetSite, _player, PlayerColor.Blue).Returns(true);
            var command = new ReturnAnySpyCommand(_targetSite.Id, PlayerColor.Blue, cardId: null);

            command.Execute(_state.MatchContext);

            Assert.AreEqual(5 - GameConstants.ReturnSpyPowerCost, _player.Power);
            _state.ActionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void Execute_ReturningAnEnemySpyFundedByACard_SpendsNoPower()
        {
            _state.MapManager.ReturnAnySpy(_targetSite, _player, PlayerColor.Blue).Returns(true);
            var command = new ReturnAnySpyCommand(_targetSite.Id, PlayerColor.Blue, cardId: "intellect_devourer_abc123");

            command.Execute(_state.MatchContext);

            Assert.AreEqual(5, _player.Power, "Card-funded return must not spend Power.");
            _state.ActionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void Execute_ReturningTheActivePlayersOwnSpyWithoutACard_SpendsNoPowerEither()
        {
            // Returning your OWN spy is never a paid basic action at all (only ever card-driven
            // in the real rules) - matching ReturnOwnSpyCommand's own no-cost design, even in the
            // (currently unreachable via the input layer) case of no cardId.
            _state.MapManager.ReturnAnySpy(_targetSite, _player, _player.Color).Returns(true);
            var command = new ReturnAnySpyCommand(_targetSite.Id, _player.Color, cardId: null);

            command.Execute(_state.MatchContext);

            Assert.AreEqual(5, _player.Power);
            _state.ActionSystem.Received(1).CompleteAction();
        }
    }
}
