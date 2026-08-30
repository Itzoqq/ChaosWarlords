using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class PlaceSpyCommandTests
    {
        private TestGameplayState _state = null!;
        private CitySite _targetSite = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
            _targetSite = TestData.Sites.PowerCity();
            _targetSite.Id = 1;
            _state.MapManager.Sites.Returns(new List<Site> { _targetSite });
        }

        [TestMethod]
        public void Validate_Returns_False_When_TargetSite_NotFound()
        {
            // Arrange
            var command = new PlaceSpyCommand(targetSiteId: 999);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should return false when target site not found");
        }

        [TestMethod]
        public void Validate_Returns_False_When_NoSpiesInBarracks()
        {
            // Arrange
            var player = TestData.Players.PoorPlayer(); // 0 spies in barracks
            _state.TurnManager.ActivePlayer.Returns(player);

            var command = new PlaceSpyCommand(_targetSite.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Validate_Returns_False_When_PlayerAlreadyHasSpyAtSite()
        {
            // Arrange: mirrors SpySubsystem.HandlePlaceSpy - can't stack a second spy of your own
            // on a site you already occupy.
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _targetSite.AddSpy(PlayerColor.Red);

            var command = new PlaceSpyCommand(_targetSite.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should reject placing a second spy of the same color at one site");
        }

        [TestMethod]
        public void Validate_Returns_True_When_SpiesAvailable_AndSiteNotAlreadyOccupiedByPlayer()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _targetSite.AddSpy(PlayerColor.Blue); // an enemy spy is fine

            var command = new PlaceSpyCommand(_targetSite.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Execute_CallsMapManager_PlaceSpy_AndCompletesAction_WhenValid()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);

            var command = new PlaceSpyCommand(_targetSite.Id);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            _state.MapManager.Received(1).PlaceSpy(_targetSite, player);
            _state.ActionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void Execute_DoesNothing_When_TargetSite_NotFound()
        {
            // Arrange
            var command = new PlaceSpyCommand(targetSiteId: 999);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            _state.MapManager.DidNotReceive().PlaceSpy(Arg.Any<Site>(), Arg.Any<ChaosWarlords.Source.Entities.Actors.Player>());
            _state.ActionSystem.DidNotReceive().CompleteAction();
        }
    }
}
