using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Map;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Map
{
    [TestClass]
    public class MapRewardSystemTests
    {
        private SiteControlSystem _mockControlSystem = null!;
        private MapRewardSystem _rewardSystem = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockControlSystem = Substitute.For<SiteControlSystem>();
            _rewardSystem = new MapRewardSystem(_mockControlSystem);
        }

        [TestMethod]
        public void DistributeStartOfTurnRewards_DelegatesToControlSystem()
        {
            // Arrange
            var sites = new List<Site>();
            var player = new Player(PlayerColor.Red);

            // Act
            _rewardSystem.DistributeStartOfTurnRewards(sites, player);

            // Assert
            _mockControlSystem.Received(1).DistributeStartOfTurnRewards(sites, player);
        }

        [TestMethod]
        public void RecalculateSiteState_DelegatesToControlSystem()
        {
            // Arrange
            var site = new NonCitySite("TestSite", ResourceType.Influence, 1, ResourceType.VictoryPoints, 2);
            var player = new Player(PlayerColor.Red);

            // Act
            _rewardSystem.RecalculateSiteState(site, player);

            // Assert
            _mockControlSystem.Received(1).RecalculateSiteState(site, player);
        }
    }
}



