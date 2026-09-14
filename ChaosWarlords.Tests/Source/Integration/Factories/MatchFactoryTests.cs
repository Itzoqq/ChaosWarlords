using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Factories;
using NSubstitute;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Core.Contexts;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Commands;

namespace ChaosWarlords.Tests.Integration.Factories
{
    [TestClass]

    [TestCategory("Integration")]
    public class MatchFactoryTests
    {
        [TestMethod]
        public void Build_WithMarketDeckSelection_PassesItToTheMarketDatabase()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            var selection = new MarketDeckSelection(MarketHalfDeck.Aberrations, MarketHalfDeck.Undead);
            mockDb.GetMarketCards(selection, Arg.Any<IGameRandom>()).Returns(new List<Card>());

            var world = new MatchFactory(mockDb, Utilities.TestLogger.Instance)
                .Build(Substitute.For<IReplayManager>(), seed: 555, marketDeckSelection: selection);

            Assert.IsNotNull(world.MarketManager);
            mockDb.Received(1).GetMarketCards(selection, Arg.Any<IGameRandom>());
        }
        [TestMethod]
        public void Build_WithDemonsInSelection_InitializesFullInsaneOutcastSupply()
        {
            // Rulebook p.4 setup step 4: the Insane Outcast pile is only put into play "if
            // you're playing with the Demons half-deck" - planning.txt TIER 1 item 11.
            var mockDb = Substitute.For<ICardDatabase>();
            var selection = new MarketDeckSelection(MarketHalfDeck.Demons, MarketHalfDeck.Drow);
            mockDb.GetMarketCards(selection, Arg.Any<IGameRandom>()).Returns(new List<Card>());

            var world = new MatchFactory(mockDb, Utilities.TestLogger.Instance)
                .Build(Substitute.For<IReplayManager>(), seed: 555, marketDeckSelection: selection);

            Assert.AreEqual(GameConstants.InsaneOutcastSupplyCount, world.PlayerStateManager.InsaneOutcastSupplyRemaining);
        }

        [TestMethod]
        public void Build_WithoutDemonsInSelection_InitializesZeroInsaneOutcastSupply()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            var selection = new MarketDeckSelection(MarketHalfDeck.Drow, MarketHalfDeck.Dragons);
            mockDb.GetMarketCards(selection, Arg.Any<IGameRandom>()).Returns(new List<Card>());

            var world = new MatchFactory(mockDb, Utilities.TestLogger.Instance)
                .Build(Substitute.For<IReplayManager>(), seed: 555, marketDeckSelection: selection);

            Assert.AreEqual(0, world.PlayerStateManager.InsaneOutcastSupplyRemaining, "Insane Outcast never exists at all in a game that didn't select the Demons half-deck.");
        }

        [TestMethod]
        public void Build_CreatesValidWorldState_Headless()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());
            mockDb.GetMarketCards(Arg.Any<MarketDeckSelection>(), Arg.Any<IGameRandom>()).Returns(new List<Card>());
            mockDb.GetAllMarketCards().Returns(new List<Card>());

            var builder = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManager = Substitute.For<IReplayManager>();
            var result = builder.Build(replayManager);

            Assert.IsNotNull(result.TurnManager.ActivePlayer);
            Assert.IsNotNull(result.MapManager);
        }

        [TestMethod]
        public void Build_DealsEachPlayerAFiveCardOpeningHand()
        {
            // Rulebook p.4 setup step 10: shuffle, draw 5, then deploy. Dealt directly inside
            // Build (planning.txt TIER 1 item 12) so this holds headlessly - no GameplayState,
            // no MonoGame LoadContent, no UI event wiring involved at all.
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());
            mockDb.GetMarketCards(Arg.Any<MarketDeckSelection>(), Arg.Any<IGameRandom>()).Returns(new List<Card>());

            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManager = Substitute.For<IReplayManager>();

            var world = factory.Build(replayManager, seed: 555);

            foreach (var player in world.TurnManager.Players)
            {
                Assert.HasCount(5, player.Hand, $"{player.Color} should have a 5-card opening hand immediately after Build().");
                Assert.HasCount(5, player.DeckManager.DrawPile, $"{player.Color}'s 10-card starting deck should have exactly 5 cards left after dealing the opening hand.");
                Assert.IsEmpty(player.DiscardPile, $"{player.Color}'s discard pile should still be empty - dealing the opening hand isn't a discard.");
            }
        }

        [TestMethod]
        public void Build_OpeningHandSurvivesSetupPhaseDeployment_IntoRealPlay()
        {
            // The exact regression this item closed: TurnLifecycleSubsystem.EndTurn used to run
            // its full Cleanup+Draw turn-cycle even during Setup, silently discarding the
            // opening hand Build() just dealt and replacing it with a fresh, different one the
            // moment each player's setup-deployment auto-EndTurn fired (see
            // GameplayState.HandleSetupDeploymentComplete for the real UI wiring this mimics).
            // Drives both players' setup deployment through the REAL CommandDispatcher, exactly
            // as production does, and asserts each player's hand is IDENTICAL (same cards, not
            // just the same count) before and after - proving the dealt hand actually survives
            // into real Round 1 rather than being silently swapped for a different random one.
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());
            mockDb.GetMarketCards(Arg.Any<MarketDeckSelection>(), Arg.Any<IGameRandom>()).Returns(new List<Card>());

            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManager = new ChaosWarlords.Source.Managers.ReplayManager(Utilities.TestLogger.Instance);

            var world = factory.Build(replayManager, seed: 555);
            var context = new MatchContext(
                world.TurnManager, world.MapManager, world.MarketManager, world.ActionSystem,
                mockDb, world.PlayerStateManager, Utilities.TestLogger.Instance, world.Seed);
            world.ActionSystem.SetMatchContext(context);
            var matchManager = new MatchManager(context, Utilities.TestLogger.Instance, new VictoryManager(Utilities.TestLogger.Instance));
            world.ActionSystem.SetMatchManager(matchManager);

            var dispatcher = new CommandDispatcher(replayManager, Utilities.TestLogger.Instance);

            var redHandBeforeDeployment = world.TurnManager.Players.First(p => p.Color == PlayerColor.Red).Hand.ToList();
            var blueHandBeforeDeployment = world.TurnManager.Players.First(p => p.Color == PlayerColor.Blue).Hand.ToList();

            // Deploy + auto-end-turn for each player, same sequence GameplayState drives via
            // MapManager.OnSetupDeploymentComplete.
            for (int i = 0; i < world.TurnManager.Players.Count; i++)
            {
                var activePlayer = context.ActivePlayer;
                var node = context.MapManager.Nodes.First(n => context.MapManager.CanDeployAt(n, activePlayer.Color));
                dispatcher.Dispatch(new DeployTroopCommand(node.Id), context);
                dispatcher.Dispatch(new EndTurnCommand(), context);
            }

            Assert.AreEqual(MatchPhase.Playing, context.CurrentPhase, "Both players deploying their single setup troop should transition to Playing.");

            var redAfter = world.TurnManager.Players.First(p => p.Color == PlayerColor.Red);
            var blueAfter = world.TurnManager.Players.First(p => p.Color == PlayerColor.Blue);
            CollectionAssert.AreEqual(redHandBeforeDeployment, redAfter.Hand.ToList(), "Red's real opening hand must survive Setup-phase deployment unchanged.");
            CollectionAssert.AreEqual(blueHandBeforeDeployment, blueAfter.Hand.ToList(), "Blue's real opening hand must survive Setup-phase deployment unchanged.");
            Assert.IsEmpty(redAfter.DiscardPile, "No card should ever have touched Red's discard pile during Setup.");
            Assert.IsEmpty(blueAfter.DiscardPile, "No card should ever have touched Blue's discard pile during Setup.");
        }

        [TestMethod]
        public void Verify_SeatIndex_IsStable_Deterministic()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());
            mockDb.GetMarketCards(Arg.Any<MarketDeckSelection>(), Arg.Any<IGameRandom>()).Returns(new List<Card>());
            mockDb.GetAllMarketCards(null).Returns(new List<Card>()); // Handle optional argument

            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManagerMock = Substitute.For<IReplayManager>();

            // Run 1
            var world1 = factory.Build(replayManagerMock, 555);
            var p1_red = world1.TurnManager.Players.First(p => p.Color == PlayerColor.Red);
            var p1_blue = world1.TurnManager.Players.First(p => p.Color == PlayerColor.Blue);

            // Run 2
            var world2 = factory.Build(replayManagerMock, 555);
            var p2_red = world2.TurnManager.Players.First(p => p.Color == PlayerColor.Red);
            var p2_blue = world2.TurnManager.Players.First(p => p.Color == PlayerColor.Blue);

            Assert.AreEqual(p1_red.SeatIndex, p2_red.SeatIndex);
            Assert.AreEqual(p1_blue.SeatIndex, p2_blue.SeatIndex);

            Assert.AreNotEqual(p1_red.SeatIndex, p1_blue.SeatIndex);
        }
        [TestMethod]
        public void Build_WithFourPlayerColors_CreatesAllFourPlayers()
        {
            // Regression test: MatchFactory used to hardcode exactly Red/Blue - no path in the
            // codebase had ever built a 3-4 player match despite PlayerColor supporting 4 seats
            // (rulebook p.4: 2-4 players). See planning.txt.
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());
            mockDb.GetMarketCards(Arg.Any<MarketDeckSelection>(), Arg.Any<IGameRandom>()).Returns(new List<Card>());

            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManagerMock = Substitute.For<IReplayManager>();
            var colors = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Black, PlayerColor.Orange };

            var world = factory.Build(replayManagerMock, seed: 555, playerColors: colors);

            Assert.HasCount(4, world.TurnManager.Players);
            for (int i = 0; i < colors.Length; i++)
            {
                var player = world.TurnManager.Players.Single(p => p.Color == colors[i]);
                Assert.AreEqual(i, player.SeatIndex);
            }
        }

        [TestMethod]
        public void Build_WithThreePlayerColors_CreatesAllThreePlayers()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());
            mockDb.GetMarketCards(Arg.Any<MarketDeckSelection>(), Arg.Any<IGameRandom>()).Returns(new List<Card>());

            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManagerMock = Substitute.For<IReplayManager>();
            var colors = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Black };

            var world = factory.Build(replayManagerMock, seed: 555, playerColors: colors);

            Assert.HasCount(3, world.TurnManager.Players);
        }

        [TestMethod]
        public void Build_WithoutPlayerColors_DefaultsToRedBlue_UnchangedFromBefore()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());
            mockDb.GetMarketCards(Arg.Any<MarketDeckSelection>(), Arg.Any<IGameRandom>()).Returns(new List<Card>());

            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManagerMock = Substitute.For<IReplayManager>();

            var world = factory.Build(replayManagerMock, seed: 555);

            Assert.HasCount(2, world.TurnManager.Players);
            Assert.IsTrue(world.TurnManager.Players.Any(p => p.Color == PlayerColor.Red));
            Assert.IsTrue(world.TurnManager.Players.Any(p => p.Color == PlayerColor.Blue));
        }

        [TestMethod]
        public void Build_WithOnePlayerColor_ThrowsArgumentException()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManagerMock = Substitute.For<IReplayManager>();

            Assert.ThrowsExactly<ArgumentException>(() =>
                factory.Build(replayManagerMock, playerColors: new[] { PlayerColor.Red }));
        }

        [TestMethod]
        public void Build_WithFivePlayerColors_ThrowsArgumentException()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManagerMock = Substitute.For<IReplayManager>();
            var colors = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Black, PlayerColor.Orange, PlayerColor.Red };

            Assert.ThrowsExactly<ArgumentException>(() =>
                factory.Build(replayManagerMock, playerColors: colors));
        }

        [TestMethod]
        public void Build_SeedsNeutralTroopsOnTheRealMap_SoWhiteTroopTargetingCardsHaveAValidTarget()
        {
            // Rulebook p.4 setup step 6: "Put white (unaligned) troop pieces in all troop spaces
            // marked..." - MapFactory.CreateScenarioMap pre-seeds Neutral troops via
            // SiteConfig.NeutralTroopSpaceCount so "Assassinate/Supplant a white troop" cards
            // (Wight, Ogre Zombie, Black Dragon, Mummy Lord, ...) have a real target the moment a
            // match starts, not just when some other effect happens to create one first.
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());
            mockDb.GetMarketCards(Arg.Any<MarketDeckSelection>(), Arg.Any<IGameRandom>()).Returns(new List<Card>());

            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManagerMock = Substitute.For<IReplayManager>();

            var world = factory.Build(replayManagerMock, seed: 555);
            var activePlayer = world.TurnManager.ActivePlayer;

            Assert.IsTrue(world.MapManager.HasValidAssassinationTarget(activePlayer, requireNeutralTroop: true, ignoresPresence: true));
        }

        [TestMethod]
        public void ApplyScenarioRules_AddsSpies_ToCityOfGold()
        {
            // Arrange
            var nodes = new List<MapNode>();
            var sites = new List<Site>();
            // Use concrete CitySite
            var cityOfGold = new CitySite("The City of Gold", ResourceType.Power, 1, ResourceType.Influence, 1);
            cityOfGold.Id = 1;
            sites.Add(cityOfGold);

            var mapManager = new MapManager(nodes, sites, Substitute.For<ITurnManager>(), Utilities.TestLogger.Instance, Substitute.For<IPlayerStateManager>());

            // Act
            MatchFactory.ApplyScenarioRules(mapManager);

            // Assert
            CollectionAssert.Contains(cityOfGold.Spies, PlayerColor.Blue);
            CollectionAssert.Contains(cityOfGold.Spies, PlayerColor.Red);
            CollectionAssert.Contains(cityOfGold.Spies, PlayerColor.Neutral);
        }

        [TestMethod]
        public void ApplyScenarioRules_DoesNothing_ForNormalSites()
        {
            // Arrange
            var nodes = new List<MapNode>();
            var sites = new List<Site>();
            var normalSite = new CitySite("Normal Forest", ResourceType.Power, 1, ResourceType.Influence, 1);
            normalSite.Id = 2;
            sites.Add(normalSite);

            var mapManager = new MapManager(nodes, sites, Substitute.For<ITurnManager>(), Utilities.TestLogger.Instance, Substitute.For<IPlayerStateManager>());

            // Act
            MatchFactory.ApplyScenarioRules(mapManager);

            // Assert
            CollectionAssert.AreEqual(new List<PlayerColor>(), normalSite.Spies);
        }

        [TestMethod]
        public void ApplyScenarioRules_HandlesNullSites_Gracefully()
        {
            // Arrange
            // We need a map manager with null sites. 
            // MapManager constructor assigns directly.
            var mapManager = new MapManager(new List<MapNode>(), null!, Substitute.For<ITurnManager>(), Utilities.TestLogger.Instance, Substitute.For<IPlayerStateManager>());

            // Act
            MatchFactory.ApplyScenarioRules(mapManager);

            // Assert
            // Should not throw exception
        }
    }
}



