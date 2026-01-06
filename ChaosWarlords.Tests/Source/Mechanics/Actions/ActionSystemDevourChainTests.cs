using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Commands;
using NSubstitute;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Reflection;

namespace ChaosWarlords.Tests.Mechanics.Actions
{
    [TestClass]
    [TestCategory("Unit")]
    public class ActionSystemDevourChainTests
    {
        private ActionSystem _actionSystem = null!;
        private ITurnManager _turnManager = null!;
        private IMapManager _mapManager = null!;
        private IMatchManager _matchManager = null!;

        [TestInitialize]
        public void Setup()
        {
            ChaosWarlords.Tests.Utilities.TestLogger.Initialize();
            _turnManager = Substitute.For<ITurnManager>();
            _mapManager = Substitute.For<IMapManager>();
            _matchManager = Substitute.For<IMatchManager>();

            _actionSystem = new ActionSystem(_turnManager, _mapManager, ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            _actionSystem.SetMatchManager(_matchManager);
        }

        [TestMethod]
        public void AdvanceDevourChain_Wight_TransitionsToSupplant()
        {
            // Arrange
            // Wight: Devour -> OnSuccess: Supplant
            var wight = new Card("wight", "Wight", 3, CardAspect.Sorcery, 1, 1, 0);
            var devEff = new CardEffect(EffectType.Devour, 1);
            devEff.OnSuccess = new CardEffect(EffectType.Supplant, 1);
            wight.AddEffect(devEff);
            
            // Set initial state
            _actionSystem.StartTargeting(ActionState.TargetingDevourHand, wight);

            IGameCommand? executedCmd = null;
            _actionSystem.OnAutoExecuteCommand += (cmd) => executedCmd = cmd;

            // Act
            _actionSystem.AdvanceDevourChain(wight);

            // Assert
            Assert.AreEqual(ActionState.TargetingSupplant, _actionSystem.CurrentState, "Should transition to next targeting state");
            Assert.IsNull(executedCmd, "Should NOT auto-execute play command yet");
        }

        [TestMethod]
        public void AdvanceDevourChain_Corruptor_FinishesPlay()
        {
            // Arrange
            // Corruptor: Devour -> OnSuccess: GainResource (Non-Targeting)
            var corruptor = new Card("corruptor", "Corruptor", 3, CardAspect.Sorcery, 1, 1, 0);
            var devEffC = new CardEffect(EffectType.Devour, 1);
            devEffC.OnSuccess = new CardEffect(EffectType.GainResource, 3, ResourceType.Influence);
            corruptor.AddEffect(devEffC);

            _actionSystem.StartTargeting(ActionState.TargetingDevourMarket, corruptor);

            IGameCommand? executedCmd = null;
            _actionSystem.OnAutoExecuteCommand += (cmd) => executedCmd = cmd;

            // Act
            _actionSystem.AdvanceDevourChain(corruptor);

            // Assert
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "Should clear state");
            Assert.IsNull(executedCmd, "Should NOT auto-execute play command (avoid double play)");
            _matchManager.Received(1).ResumeDevourChain(corruptor);
        }
    }
}
