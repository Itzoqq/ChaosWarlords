using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosWarlords.Tests.Entities
{
    [TestClass]
    public class StartingSiteTests
    {
        [TestMethod]
        public void Constructor_InitializesCorrectly()
        {
            var site = new StartingSite("Test Starting Site", ResourceType.Power, 1, ResourceType.VictoryPoints, 2);

            Assert.AreEqual("Test Starting Site", site.Name);
            Assert.AreEqual(ResourceType.Power, site.ControlResource);
            Assert.AreEqual(1, site.ControlAmount);
            Assert.AreEqual(ResourceType.VictoryPoints, site.TotalControlResource);
            Assert.AreEqual(2, site.TotalControlAmount);
            Assert.IsFalse(site.IsCity, "StartingSite should be a NonCitySite (IsCity = false).");
        }

        [TestMethod]
        public void Inheritance_IsNonCitySite()
        {
            var site = new StartingSite("Inheritance Test", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);
            Assert.IsInstanceOfType(site, typeof(NonCitySite));
            Assert.IsInstanceOfType(site, typeof(Site));
        }
    }
}



