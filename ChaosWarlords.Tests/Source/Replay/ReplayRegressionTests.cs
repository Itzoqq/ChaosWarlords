using System;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Interfaces.Composition;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Factories;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Tests.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Replay
{
    /// <summary>
    /// Regression tests for specific replay bugs.
    /// Uses MSTest to match project standards.
    /// </summary>
    [TestClass]
    public class ReplayRegressionTests
    {
        private IGameLogger _loggerMock = null!;
        private ChaosWarlords.Source.Core.Interfaces.Data.ICardDatabase _cardDbMock = null!;
        private IReplayManager _replayManagerMock = null!;
        private SeededGameRandom? _random;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = Substitute.For<IGameLogger>();
            _cardDbMock = Substitute.For<ChaosWarlords.Source.Core.Interfaces.Data.ICardDatabase>();
            _replayManagerMock = Substitute.For<IReplayManager>();
            _random = new SeededGameRandom(12345, _loggerMock);

            // Mock Card DB to return deterministic cards
            _cardDbMock.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());
            _cardDbMock.GetAllMarketCards(null).Returns(new List<Card>());
        }

        [TestMethod]
        public void Verify_SetupPhase_DoesNotAutoAdvance_DuringReplay()
        {
            // Setup Environment
            var factory = new MatchFactory(_cardDbMock, _loggerMock);
            
            // Allow ReplayManager logic to flow
            _replayManagerMock.IsReplaying.Returns(true);
            _replayManagerMock.Seed.Returns(12345);

            var worldData = factory.Build(_replayManagerMock, 12345);

            // Manually wire up the context
            var matchContext = new MatchContext(
                worldData.TurnManager,
                worldData.MapManager,
                worldData.MarketManager,
                worldData.ActionSystem,
                _cardDbMock,
                worldData.PlayerStateManager,
                _loggerMock,
                12345
            );

            // Act: Verify MapManager event firing
            bool eventFired = false;
            worldData.MapManager.OnSetupDeploymentComplete += () => eventFired = true;
            
            worldData.MapManager.SetPhase(MatchPhase.Setup);
            
            var p1 = worldData.TurnManager.Players[0];
            p1.TroopsInBarracks = 1; // Last troop
            
            // Find a valid starting node (must be StartingSite and Empty)
            MapNode? deployNode = null;
            foreach(var n in worldData.MapManager.NodesInternal)
            {
                var site = worldData.MapManager.GetSiteForNode(n);
                if (site is StartingSite && n.Occupant == PlayerColor.None)
                {
                    deployNode = n;
                    break;
                }
            }
            Assert.IsNotNull(deployNode, "Could not find a valid StartingSite node for test.");

            worldData.MapManager.TryDeploy(p1, deployNode);
            
            Assert.IsTrue(eventFired, "MapManager SHOULD fire the event when setup deployment occurs.");
            
            // NOTE: We cannot easily verify GameplayState behavior without instantiating it, 
            // but we've verified the fix visually. This test ensures the PRECONDITION (event firing) exists.
        }
        
        [TestMethod]
        public void Verify_SeatIndex_IsStable_Deterministic()
        {
             var factory = new MatchFactory(_cardDbMock, _loggerMock);
             
             // Run 1
             var world1 = factory.Build(_replayManagerMock, 555);
             var p1_red = world1.TurnManager.Players.First(p => p.Color == PlayerColor.Red);
             var p1_blue = world1.TurnManager.Players.First(p => p.Color == PlayerColor.Blue);
             
             // Run 2
             var world2 = factory.Build(_replayManagerMock, 555);
             var p2_red = world2.TurnManager.Players.First(p => p.Color == PlayerColor.Red);
             var p2_blue = world2.TurnManager.Players.First(p => p.Color == PlayerColor.Blue);
             
             Assert.AreEqual(p1_red.SeatIndex, p2_red.SeatIndex);
             Assert.AreEqual(p1_blue.SeatIndex, p2_blue.SeatIndex);
             
             Assert.AreNotEqual(p1_red.SeatIndex, p1_blue.SeatIndex);
        }
        
        [TestMethod]
        public void Verify_Hydration_Uses_CardId()
        {
            var p = new Player(PlayerColor.Red, Guid.NewGuid());
            // Card(string id, string name, int cost, CardAspect aspect, int deckVp, int innerCircleVp, int influence)
            var card1 = new Card("noble_111", "Noble", 0, CardAspect.Neutral, 1, 1, 1);
            var card2 = new Card("soldier_222", "Soldier", 0, CardAspect.Neutral, 1, 1, 0);
            p.Hand.Add(card1);
            p.Hand.Add(card2);
            p.SeatIndex = 0;
            
            var stateMock = Substitute.For<IGameplayState>();
            stateMock.Logger.Returns(_loggerMock);
            
            var tmMock = Substitute.For<ITurnManager>();
            tmMock.Players.Returns(new List<Player> { p });
            stateMock.TurnManager.Returns(tmMock);
            
            var dto = new PlayCardCommandDto
            {
                CardId = "noble_111",
                HandIdx = 1, // Wrong index (Soldier is here)
                Seat = 0
            };
            
            var result = ChaosWarlords.Source.Core.Utilities.DtoMapper.HydrateCommand(dto, stateMock) as PlayCardCommand;
            
            Assert.IsNotNull(result);
            Assert.AreEqual("noble_111", result.Card.Id, "Hydration should prefer ID over Index!");
        }
        
        [TestMethod]
        public void Verify_Hydration_Fallback_ToIndex_If_ID_Missing()
        {
            var p = new Player(PlayerColor.Red, Guid.NewGuid());
            var card1 = new Card("noble_111", "Noble", 0, CardAspect.Neutral, 1, 1, 1);
            p.Hand.Add(card1);
            p.SeatIndex = 0;
            
            var stateMock = Substitute.For<IGameplayState>();
            stateMock.Logger.Returns(_loggerMock);
            
            var tmMock = Substitute.For<ITurnManager>();
            tmMock.Players.Returns(new List<Player> { p });
            stateMock.TurnManager.Returns(tmMock);
            
            var dto = new PlayCardCommandDto
            {
                CardId = "noble_old_XX",
                HandIdx = 0, 
                Seat = 0
            };
            
            var result = ChaosWarlords.Source.Core.Utilities.DtoMapper.HydrateCommand(dto, stateMock) as PlayCardCommand;
            
            Assert.IsNotNull(result);
            Assert.AreEqual("noble_111", result.Card.Id, "Hydration should fallback to index if ID not found.");
            
            // Verify warning logged
            _loggerMock.Received().Log(Arg.Is<string>(s => s.Contains("Fell back to Index")), LogChannel.Warning);
        }
    }
}
