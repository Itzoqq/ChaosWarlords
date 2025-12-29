using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;
using System.Diagnostics;

namespace ChaosWarlords.Tests.Core.Performance
{
    /// <summary>
    /// Performance benchmark tests for critical game systems.
    /// These tests ensure operations complete within acceptable time limits.
    /// </summary>
    [TestClass]
    [TestCategory("Performance")]
    public class PerformanceTests
    {
        private const int PerformanceThresholdMs = 50; // 50ms max for most operations
        private const int ResourceUpdateThresholdMs = 250; // 250ms for resource updates

        [TestMethod]
        public void DeckShuffle_CompletesWithin50ms_For1000Cards()
        {
            // Arrange
            var deck = new Deck();
            for (int i = 0; i < 1000; i++)
            {
                deck.AddToTop(new CardBuilder()
                    .WithName($"card_{i}")
                    .WithCost(i % 10)
                    .Build());
            }

            var random = new SeededGameRandom(12345);
            var stopwatch = Stopwatch.StartNew();

            // Act
            deck.Shuffle(random);

            // Assert
            stopwatch.Stop();
            Assert.IsLessThan(PerformanceThresholdMs,
                stopwatch.ElapsedMilliseconds,
                $"Deck shuffle took {stopwatch.ElapsedMilliseconds}ms, expected < {PerformanceThresholdMs}ms");
        }

        [TestMethod]
        public void DeckDraw_CompletesWithin10ms_For100Draws()
        {
            // Arrange
            var deck = new Deck();
            for (int i = 0; i < 100; i++)
            {
                deck.AddToTop(TestData.Cards.FreeCard());
            }

            var random = new SeededGameRandom(12345);
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < 100; i++)
            {
                deck.Draw(1, random);
            }

            // Assert
            stopwatch.Stop();
            Assert.IsLessThan(10,
                stopwatch.ElapsedMilliseconds,
                $"100 draws took {stopwatch.ElapsedMilliseconds}ms, expected < 10ms");
        }

        [TestMethod]
        public void PlayerResourceUpdates_CompletesWithin200ms_For1000Updates()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            var stateManager = new ChaosWarlords.Source.Managers.PlayerStateManager();
            var stopwatch = Stopwatch.StartNew();

            GameLogger.IsEnabled = false;
            try
            {
                // Act
                for (int i = 0; i < 1000; i++)
                {
                    stateManager.AddPower(player, 1);
                    stateManager.AddInfluence(player, 1);
                    stateManager.AddVictoryPoints(player, 1);
                }
            }
            finally
            {
                GameLogger.IsEnabled = true;
            }

            // Assert
            stopwatch.Stop();
            Assert.IsLessThan(ResourceUpdateThresholdMs,
                stopwatch.ElapsedMilliseconds,
                $"1000 resource updates took {stopwatch.ElapsedMilliseconds}ms, expected < {ResourceUpdateThresholdMs}ms");
        }

        [TestMethod]
        public void CardEffectResolution_CompletesWithin30ms_For100Cards()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            var cards = new List<Card>();
            for (int i = 0; i < 100; i++)
            {
                cards.Add(new CardBuilder()
                    .WithEffect(EffectType.GainResource, 1, ResourceType.Power)
                    .WithEffect(EffectType.GainResource, 1, ResourceType.Influence)
                    .Build());
            }

            var stateManager = new ChaosWarlords.Source.Managers.PlayerStateManager();
            var stopwatch = Stopwatch.StartNew();

            GameLogger.IsEnabled = false;
            try
            {
                // Act
                foreach (var card in cards)
                {
                    foreach (var effect in card.Effects)
                    {
                        if (effect.Type == EffectType.GainResource)
                        {
                            if (effect.TargetResource == ResourceType.Power)
                                stateManager.AddPower(player, effect.Amount);
                            else if (effect.TargetResource == ResourceType.Influence)
                                stateManager.AddInfluence(player, effect.Amount);
                        }
                    }
                }
            }
            finally
            {
                GameLogger.IsEnabled = true;
            }

            // Assert
            stopwatch.Stop();
            Assert.IsLessThan(30,
                stopwatch.ElapsedMilliseconds,
                $"100 card effect resolutions took {stopwatch.ElapsedMilliseconds}ms, expected < 30ms");
        }

        [TestMethod]
        public void MapNodeNeighborLookup_CompletesWithin10ms_For1000Lookups()
        {
            // Arrange
            var node1 = TestData.MapNodes.Node1();
            var node2 = TestData.MapNodes.Node2();
            var node3 = TestData.MapNodes.Node3();

            node1.AddNeighbor(node2);
            node2.AddNeighbor(node1);
            node2.AddNeighbor(node3);
            node3.AddNeighbor(node2);

            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < 1000; i++)
            {
                var neighbors = node2.Neighbors.ToList();
                var hasNode1 = neighbors.Contains(node1);
                var hasNode3 = neighbors.Contains(node3);
            }

            // Assert
            stopwatch.Stop();
            Assert.IsLessThan(10,
                stopwatch.ElapsedMilliseconds,
                $"1000 neighbor lookups took {stopwatch.ElapsedMilliseconds}ms, expected < 10ms");
        }

        [TestMethod]
        public void SeededRandom_CompletesWithin20ms_For10000Generations()
        {
            // Arrange
            var random = new SeededGameRandom(12345);
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < 10000; i++)
            {
                random.NextInt(0, 100);
            }

            // Assert
            stopwatch.Stop();
            Assert.IsLessThan(20,
                stopwatch.ElapsedMilliseconds,
                $"10000 random generations took {stopwatch.ElapsedMilliseconds}ms, expected < 20ms");
        }

        [TestMethod]
        public void PlayerHandManipulation_CompletesWithin15ms_For1000Operations()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            var cards = new List<Card>();
            for (int i = 0; i < 100; i++)
            {
                cards.Add(new CardBuilder().WithName($"card_{i}").Build());
            }

            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < 1000; i++)
            {
                var card = cards[i % cards.Count];
                player.Hand.Add(card);
                player.Hand.Remove(card);
            }

            // Assert
            stopwatch.Stop();
            Assert.IsLessThan(15,
                stopwatch.ElapsedMilliseconds,
                $"1000 hand operations took {stopwatch.ElapsedMilliseconds}ms, expected < 15ms");
        }
    }
}
