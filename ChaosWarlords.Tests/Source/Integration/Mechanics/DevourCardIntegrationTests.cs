using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Integration.Mechanics
{
    [TestClass]
    [TestCategory("Integration")]
    public class DevourCardIntegrationTests
    {
        private MatchContext _context = null!;
        private Player _player = null!;
        private ActionSystem _actionSystem = null!;
        private IMarketManager _marketManager = null!;
        private IMapManager _mapManager = null!;
        private IMatchManager _matchManager = null!;
        private IUIEventMediator _uiMediator = null!;
        private IGameLogger _logger = null!;
        private IPlayerStateManager _playerStateManager = null!;

        [TestInitialize]
        public void Setup()
        {
            ChaosWarlords.Tests.Utilities.TestLogger.Initialize();
            _logger = ChaosWarlords.Tests.Utilities.TestLogger.Instance;

            _player = new Player(PlayerColor.Red);

            var turnManager = Substitute.For<ITurnManager>();
            turnManager.ActivePlayer.Returns(_player);

            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _matchManager = Substitute.For<IMatchManager>();

            // Use real or mock StateManager?
            // Since we test side effects (TroopsInBarracks), and PlayerStateManager calls player setters...
            // We can use a Real PlayerStateManager if available, OR mock it and verify calls.
            // But CardEffectProcessor calls context.PlayerStateManager.AddTroops...
            // If we mock PlayerStateManager, we won't see Player.Troops update unless we setup the mock to do so.
            // Let's mock it and setup behavior manually for the test critical path.
            _playerStateManager = Substitute.For<IPlayerStateManager>();
            
            // Forward calls to simple player logic for "AddPower", "AddTroops" etc if we want integration,
            // OR just Assert.Received.
            // The tests check `_player.Power` so we should wire them up.
            
            _playerStateManager.When(x => x.AddPower(Arg.Any<Player>(), Arg.Any<int>()))
                .Do(info => info.Arg<Player>().Power += info.Arg<int>());
            
            _playerStateManager.When(x => x.AddTroops(Arg.Any<Player>(), Arg.Any<int>()))
                .Do(info => info.Arg<Player>().TroopsInBarracks += info.Arg<int>());

             _playerStateManager.When(x => x.AddInfluence(Arg.Any<Player>(), Arg.Any<int>()))
                .Do(info => info.Arg<Player>().Influence += info.Arg<int>());

            // Setup ActionSystem
            _actionSystem = new ActionSystem(turnManager, _mapManager, _logger);
            _actionSystem.SetMatchManager(_matchManager);
            _actionSystem.SetMarketManager(_marketManager);

            // Mock UI Mediator to handle optional popups
            _uiMediator = Substitute.For<IUIEventMediator>();

            _context = new MatchContext(
                turnManager,
                _mapManager,
                _marketManager,
                _actionSystem,
                Substitute.For<ICardDatabase>(),
                _playerStateManager,
                _uiMediator,
                _logger,
                12345
            );
        }

        [TestMethod]
        public void MarketCorruptor_Play_PromptsOptionalMarketDevour()
        {
             var card = new Card("market_corruptor", "Market Corruptor", 3, CardAspect.Sorcery, 1, 2, 0);
             var devourEffect = new CardEffect(EffectType.Devour, 1)
             {
                 TargetLocation = CardLocation.Market,
                 IsOptional = true,
                 OnSuccess = new CardEffect(EffectType.GainResource, 3, ResourceType.Influence)
             };
             card.Effects.Add(devourEffect);

            // Setup Market
            var marketCard = new Card("m1", "MarketCard", 0, CardAspect.Neutral, 0, 0, 0) { Location = CardLocation.Market };
            _marketManager.MarketRow.Returns(new List<Card> { marketCard });

             // Act
            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, _logger);

            // Assert
            _uiMediator.Received(1).RequestOptionalEffect(card, devourEffect, Arg.Any<System.Action>(), Arg.Any<System.Action>());
        }

        [TestMethod]
        public void MarketCorruptor_AcceptDevour_EntersTargetingMode()
        {
            var card = new Card("market_corruptor", "Market Corruptor", 3, CardAspect.Sorcery, 1, 2, 0);
             var devourEffect = new CardEffect(EffectType.Devour, 1)
             {
                 TargetLocation = CardLocation.Market,
                 IsOptional = true,
                 OnSuccess = new CardEffect(EffectType.GainResource, 3, ResourceType.Influence)
             };
             card.Effects.Add(devourEffect);

            var marketCard = new Card("m1", "MarketCard", 0, CardAspect.Neutral, 0, 0, 0) { Location = CardLocation.Market };
            _marketManager.MarketRow.Returns(new List<Card> { marketCard });

            System.Action? onAccept = null;
            _uiMediator.RequestOptionalEffect(Arg.Any<Card>(), Arg.Any<CardEffect>(), Arg.Do<System.Action>(a => onAccept = a), Arg.Any<System.Action>());

            // Act 1: Resolve
            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, _logger);

            // Act 2: Accept
            onAccept?.Invoke();

            // Assert: Should now be in Targeting Mode via ActionSystem
            Assert.AreEqual(ActionState.TargetingDevourMarket, _actionSystem.CurrentState, "Should enter Market Devour Targeting");
        }
    }
}
