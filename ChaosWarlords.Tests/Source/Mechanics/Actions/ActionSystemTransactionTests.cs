using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Commands;
using NSubstitute;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    [TestCategory("Unit")]
    public class ActionSystemTransactionTests
    {
        private Player _player1 = null!;
        private IMapManager _mapManager = null!; 
        private ITurnManager _turnManager = null!; 
        private IMatchManager _matchManager = null!;
        private ActionSystem _actionSystem = null!; 

        private MapNode _node1 = null!;

        [TestInitialize]
        public void Setup()
        {
            ChaosWarlords.Tests.Utilities.TestLogger.Initialize();
            _player1 = TestData.Players.RedPlayer();

            _mapManager = Substitute.For<IMapManager>();
            _turnManager = Substitute.For<ITurnManager>();
            _matchManager = Substitute.For<IMatchManager>();

            _turnManager.ActivePlayer.Returns(_player1);

            _node1 = TestData.MapNodes.Node1();
            _mapManager.Nodes.Returns(new List<MapNode> { _node1 });

            // Create System
            _actionSystem = new ActionSystem(_turnManager, _mapManager, ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            _actionSystem.SetMatchManager(_matchManager);
            
            // Mock PlayerStateManager
            var psm = Substitute.For<IPlayerStateManager>();
            _actionSystem.SetPlayerStateManager(psm);

            // Give player some cards including a transactional source
            _player1.Hand.Clear();
        }

        [TestMethod]
        public void TryStartDevourHand_Deferred_DoesNotExecuteImmediately()
        {
            // Arrange
            var sourceCard = TestData.Cards.DevourCard();
            var fodderCard = TestData.Cards.CheapCard();
            _player1.Hand.Add(fodderCard);

            // Act
            _actionSystem.TryStartDevourHand(sourceCard, null, deferExecution: true);

            // Handle Selection
            _actionSystem.HandleDevourSelection(fodderCard);

            // Assert
            // 1. Devour should NOT have happened on MatchManager
            _matchManager.DidNotReceive().DevourCard(Arg.Any<Card>());
            
            // 2. PendingDevourCard should be set
            Assert.AreEqual(fodderCard, _actionSystem.PendingDevourCard);
            
            // 3. State should be cleared (CompleteAction called)
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState); 
            // Note: In a real flow, the callback would start the next targeting. 
            // Here, we didn't pass a callback that starts new targeting, so it went to Normal.
        }

        [TestMethod]
        public void DeferredDevour_IsIncludedIn_SupplantCommand()
        {
            // Arrange
            var sourceCard = TestData.Cards.SupplantCard(); // Assume this triggers the chain
            var fodderCard = TestData.Cards.CheapCard();
            _player1.Hand.Add(fodderCard);
            _player1.TroopsInBarracks = 5;

            // Step 1: Start Devour (Deferred)
            _actionSystem.TryStartDevourHand(sourceCard, () => 
            {
                // Callback simulates transitioning to Supplant
                 _actionSystem.StartTargeting(ActionState.TargetingSupplant, sourceCard);
            }, deferExecution: true);

            // Step 2: Select Fodder
            _actionSystem.HandleDevourSelection(fodderCard);

            // Assert Intermediate
            Assert.AreEqual(fodderCard, _actionSystem.PendingDevourCard);
            Assert.AreEqual(ActionState.TargetingSupplant, _actionSystem.CurrentState);

            // Step 3: Select Supplant Target
            _mapManager.CanAssassinate(_node1, _player1).Returns(true);
            
            // Act
            var command = _actionSystem.HandleTargetClick(_node1, null);

            // Assert Command
            Assert.IsNotNull(command);
            Assert.IsInstanceOfType(command, typeof(SupplantCommand));
            var supplantCmd = (SupplantCommand)command;
            
            Assert.AreEqual(sourceCard.Id, supplantCmd.CardId);
            Assert.AreEqual(fodderCard.Id, supplantCmd.DevourCardId);
        }

        [TestMethod]
        public void CancelTargeting_ClearsPendingDevour()
        {
            // Arrange
            var sourceCard = TestData.Cards.DevourCard();
            var fodderCard = TestData.Cards.CheapCard();
            _player1.Hand.Add(fodderCard);

            _actionSystem.TryStartDevourHand(sourceCard, null, deferExecution: true);
            _actionSystem.HandleDevourSelection(fodderCard);
            
            // Verify buffering
            Assert.AreEqual(fodderCard, _actionSystem.PendingDevourCard);

            // Act
            _actionSystem.CancelTargeting();

            // Assert
            Assert.IsNull(_actionSystem.PendingDevourCard);
        }
    }
}
