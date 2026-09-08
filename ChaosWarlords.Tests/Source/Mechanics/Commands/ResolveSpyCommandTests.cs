using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class ResolveSpyCommandTests
    {
        [TestMethod]
        public void Execute_CallsFinalizeSpyReturnOnActionSystem()
        {
            // Arrange
            var stateFake = new TestGameplayState();

            var mockActionSystem = stateFake.ActionSystem;
            var mockMapManager = stateFake.MapManager;

            var site = TestData.Sites.NeutralSite();
            site.Id = 10;

            mockMapManager.Sites.Returns(new List<Site> { site });

            var command = new ResolveSpyCommand(10, PlayerColor.Blue);

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            // Command now delegates to MapManager.ReturnSpecificSpy
            mockMapManager.Received(1).ReturnSpecificSpy(site, Arg.Any<Player>(), PlayerColor.Blue);
        }

        // --- Validate() must re-derive the enemy-only restriction, not trust the caller (Red
        // Dragon: "Return an enemy spy" is this command's first non-base-action consumer) ---

        [TestMethod]
        public void Validate_ReturnsFalse_WhenSpyColorIsTheActivePlayersOwn()
        {
            var stateFake = new TestGameplayState();
            var red = TestData.Players.RedPlayer();
            stateFake.TurnManager.ActivePlayer.Returns(red);

            var site = TestData.Sites.NeutralSite();
            site.Id = 10;
            site.AddSpy(red.Color);
            stateFake.MapManager.Sites.Returns(new List<Site> { site });

            var command = new ResolveSpyCommand(10, red.Color);

            var result = command.Validate(stateFake.MatchContext);

            Assert.IsFalse(result, "A forged command naming the active player's OWN spy color must be rejected - use ReturnOwnSpyCommand instead.");
        }

        [TestMethod]
        public void Validate_ReturnsTrue_WhenSpyColorIsAnEnemys()
        {
            var stateFake = new TestGameplayState();
            var red = TestData.Players.RedPlayer();
            stateFake.TurnManager.ActivePlayer.Returns(red);

            var site = TestData.Sites.NeutralSite();
            site.Id = 10;
            site.AddSpy(PlayerColor.Blue);
            stateFake.MapManager.Sites.Returns(new List<Site> { site });

            var command = new ResolveSpyCommand(10, PlayerColor.Blue);

            var result = command.Validate(stateFake.MatchContext);

            Assert.IsTrue(result, "An enemy's spy color must still validate successfully - only the active player's own color is rejected.");
        }
    }
}
