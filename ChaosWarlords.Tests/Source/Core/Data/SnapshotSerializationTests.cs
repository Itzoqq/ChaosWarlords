using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Tests.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Contexts;
using System.Collections.Generic;
using ChaosWarlords.Source.Core.Interfaces.Logic;

namespace ChaosWarlords.Tests.Source.Core.Data
{
    [TestClass]
    [TestCategory("Unit")]
    public class SnapshotSerializationTests
    {
        private ITurnManager _turnManager = null!;
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private IActionSystem _actionSystem = null!;
        private ICardDatabase _cardDatabase = null!;
        private IPlayerStateManager _playerStateManager = null!;

        [TestInitialize]
        public void Setup()
        {
            _turnManager = Substitute.For<ITurnManager>();
            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _cardDatabase = Substitute.For<ICardDatabase>();
            _playerStateManager = Substitute.For<IPlayerStateManager>();
            
            // Setup empty collections for all manager mocks
            _turnManager.Players.Returns(_ => new List<Player>());
            _mapManager.Nodes.Returns(_ => new List<MapNode>());
            _marketManager.MarketRow.Returns(_ => new List<Card>());
        }

        [TestMethod]
        public void ToGameStateDto_SerializesEffectStack()
        {
            // Arrange
            // Create a fake stack
            var stack = new Stack<EffectContext>();
            var testCard = new Card("test-1", "Test Card", 3, CardAspect.Neutral, 0, 0, 0);
            var effect = new EffectContext(
                ActionState.TargetingAssassinate,
                testCard,
                true,
                "Assassinate Logic",
                (s) => { },
                null
            );
            stack.Push(effect);

            _actionSystem.ExecutionStack.Returns(stack);

            // Create context with this action system
            var context = new MatchContext(
                _turnManager,
                _mapManager,
                _marketManager,
                _actionSystem,
                _cardDatabase,
                _playerStateManager,
                TestLogger.Instance,
                999
            );
            
            // Allow mapper to see ExecutionStack
            // DtoMapper reads .ExecutionStack. The mock is set up to return our list.

            // Act
            var dto = DtoMapper.ToGameStateDto(context);

            // Assert
            Assert.IsNotNull(dto.EffectStack, "EffectStack should be initialized.");
            Assert.HasCount(1, dto.EffectStack, "Should have 1 item.");
            Assert.AreEqual(ActionState.TargetingAssassinate, dto.EffectStack[0].State);
        }

        [TestMethod]
        public void ToGameStateDto_SerializesEffectStack_PreservesRemainingRepeats()
        {
            // Companion to ToGameStateDto_SerializesEffectStack above - RemainingRepeats
            // (Deathblade's "Assassinate 2 troops" mid-sequence counter) must actually make it
            // into the DTO, not just default silently.
            var stack = new Stack<EffectContext>();
            var testCard = new Card("test-1", "Test Card", 3, CardAspect.Neutral, 0, 0, 0);
            var effect = new EffectContext(
                ActionState.TargetingAssassinate,
                testCard,
                true,
                "Assassinate Logic",
                (s) => { },
                null
            )
            {
                RemainingRepeats = 2
            };
            stack.Push(effect);

            _actionSystem.ExecutionStack.Returns(stack);

            var context = new MatchContext(
                _turnManager,
                _mapManager,
                _marketManager,
                _actionSystem,
                _cardDatabase,
                _playerStateManager,
                TestLogger.Instance,
                999
            );

            var dto = DtoMapper.ToGameStateDto(context);

            Assert.AreEqual(2, dto.EffectStack[0].RemainingRepeats, "RemainingRepeats must be carried into the DTO, not defaulted.");
        }
    }
}
