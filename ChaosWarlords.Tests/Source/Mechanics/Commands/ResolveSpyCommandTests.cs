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
    }
}
