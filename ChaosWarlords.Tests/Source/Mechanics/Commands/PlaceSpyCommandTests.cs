using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Utilities;
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

        [TestMethod]
        public void Execute_SetsPendingSiteForChain_BeforeCompletingAction()
        {
            // Arrange: Banshee/Infiltrator's conditional OnSuccess (ConditionType.
            // OpponentPresentAtSite) reads ActionSystem.PendingSite while CompleteAction()
            // synchronously resolves the chain - SetPendingSiteForChain must therefore be
            // called BEFORE CompleteAction(), not after (see PlaceSpyCommand.Execute's own
            // comment). NSubstitute records call order, so this pins that ordering directly
            // rather than just each call happening at all.
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);

            var command = new PlaceSpyCommand(_targetSite.Id);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            Received.InOrder(() =>
            {
                _state.ActionSystem.SetPendingSiteForChain(_targetSite);
                _state.ActionSystem.CompleteAction();
            });
        }

        [TestMethod]
        public void ToDto_ThenHydrate_RoundTripsSiteIdAndCardId()
        {
            // Arrange: DTO round-trip (planning.txt's testing-policy matrix row 9) - confirms
            // this still holds along Banshee/Infiltrator's new conditional OnSuccess chain,
            // even though the chain itself lives on the card's CardEffect tree, not on this
            // command's own DTO shape.
            var command = new PlaceSpyCommand(_targetSite.Id, "banshee");

            // Act
            var dto = command.ToDto();
            var hydrated = ChaosWarlords.Source.Core.Utilities.DtoMapper.HydrateCommand(dto, _state.MatchContext) as PlaceSpyCommand;

            // Assert
            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetSiteId, hydrated!.TargetSiteId);
            Assert.AreEqual(command.CardId, hydrated.CardId);
        }
    }
}
