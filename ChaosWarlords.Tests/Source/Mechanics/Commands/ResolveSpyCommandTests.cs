using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

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
            
            var mockActionSystem = Substitute.For<IActionSystem>();
            var mockMapManager = Substitute.For<IMapManager>();
            
            stateFake.ActionSystem = mockActionSystem;
            stateFake.MapManager = mockMapManager;

            var site = TestData.Sites.NeutralSite();
            site.Id = 10;
            
            mockMapManager.Sites.Returns(new List<Site> { site });

            var command = new ResolveSpyCommand(10, PlayerColor.Blue);

            // Act
            command.Execute(stateFake);

            // Assert
            mockActionSystem.Received(1).PerformSpyReturn(site, PlayerColor.Blue, null);
        }
    }
}
