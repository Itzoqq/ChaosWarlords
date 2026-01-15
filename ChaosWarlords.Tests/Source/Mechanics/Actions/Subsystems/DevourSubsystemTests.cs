using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Mechanics.Actions.Subsystems;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Mechanics.Actions.Subsystems
{
    [TestClass]
    [TestCategory("Unit")]
    public class DevourSubsystemTests
    {
        private DevourSubsystem _subsystem = null!;
        private ITurnManager _turnManager = null!;
        private IActionSystem _actionSystem = null!;
        private IGameLogger _logger = null!;
        private IMatchManager _matchManager = null!;
        private IMarketManager _marketManager = null!;
        private IPlayerStateManager _playerStateManager = null!;

        private Player _activePlayer = null!;

        [TestInitialize]
        public void Setup()
        {
            _turnManager = Substitute.For<ITurnManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _logger = Substitute.For<IGameLogger>();
            _matchManager = Substitute.For<IMatchManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _playerStateManager = Substitute.For<IPlayerStateManager>();

            _activePlayer = new Player(PlayerColor.Red);
            _turnManager.ActivePlayer.Returns(_activePlayer);

            _subsystem = new DevourSubsystem(_turnManager, _actionSystem, _logger);
            _subsystem.SetMatchManager(_matchManager);
            _subsystem.SetMarketManager(_marketManager);
            _subsystem.SetPlayerStateManager(_playerStateManager);
        }

        private Card CreateTestCard(string name, CardLocation location = CardLocation.Hand)
        {
            var card = new Card($"test_{name}", name, 0, CardAspect.Neutral, 0, 0, 0)
            {
                Location = location
            };
            return card;
        }

        [TestMethod]
        public void TryStartDevourHand_ChecksForPreTarget()
        {
            // Arrange
            var card = CreateTestCard("Devourer", CardLocation.Hand);
            var preTarget = CreateTestCard("Food", CardLocation.Hand);

            _actionSystem.GetAndClearPreTarget(card, ActionState.TargetingDevourHand).Returns(preTarget);

            // Act
            _subsystem.TryStartDevourHand(card, () => { });

            // Assert
            _matchManager.Received(1).DevourCard(preTarget);
        }

        [TestMethod]
        public void TryStartDevourHand_StartsTargeting_IfNoPreTarget()
        {
            // Arrange
            var card = CreateTestCard("Devourer", CardLocation.Hand);
            _activePlayer.Hand.Add(CreateTestCard("Other1"));
            _activePlayer.Hand.Add(CreateTestCard("Other2")); // Ensure >1 count

            _actionSystem.GetAndClearPreTarget(card, ActionState.TargetingDevourHand).Returns((object?)null);

            // Act
            _subsystem.TryStartDevourHand(card);

            // Assert
            _actionSystem.Received(1).StartTargeting(ActionState.TargetingDevourHand, card);
        }

        [TestMethod]
        public void HandleDevourSelection_ReturnsCommand()
        {
            // Arrange
            var target = CreateTestCard("Target");

            // Act
            var cmd = _subsystem.HandleDevourSelection(target);

            // Assert
            Assert.IsNotNull(cmd);
            Assert.AreEqual(target.Id, cmd.CardToDevour.Id);
            // We no longer trigger execution or completion directly in the subsystem for manual selection
            // _matchManager.Received(1).DevourCard(target); 
            // _actionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void HandleDevourSelection_Ignored_IfNull()
        {
            _subsystem.HandleDevourSelection(null);
            _matchManager.DidNotReceive().DevourCard(Arg.Any<Card>());
        }
    }
}
