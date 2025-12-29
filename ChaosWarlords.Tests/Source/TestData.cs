using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Tests
{
    /// <summary>
    /// Centralized test data for common test scenarios.
    /// All methods return NEW instances to prevent state pollution between tests.
    /// </summary>
    public static class TestData
    {
        /// <summary>
        /// Common card configurations for testing.
        /// Each method returns a NEW card instance.
        /// </summary>
        public static class Cards
        {
            public static Card CheapCard() => new CardBuilder()
                .WithName("cheap")
                .WithCost(2)
                .WithAspect(CardAspect.Neutral)
                .Build();

            public static Card ExpensiveCard() => new CardBuilder()
                .WithName("expensive")
                .WithCost(10)
                .WithAspect(CardAspect.Neutral)
                .Build();

            public static Card FreeCard() => new CardBuilder()
                .WithName("free")
                .WithCost(0)
                .WithAspect(CardAspect.Neutral)
                .Build();

            public static Card AssassinCard() => new CardBuilder()
                .WithName("assassin")
                .WithCost(3)
                .WithAspect(CardAspect.Shadow)
                .WithEffect(EffectType.Assassinate, 1)
                .Build();

            public static Card PowerCard() => new CardBuilder()
                .WithName("power_generator")
                .WithCost(2)
                .WithAspect(CardAspect.Warlord)
                .WithEffect(EffectType.GainResource, 3, ResourceType.Power)
                .Build();

            public static Card InfluenceCard() => new CardBuilder()
                .WithName("influence_generator")
                .WithCost(2)
                .WithAspect(CardAspect.Neutral)
                .WithEffect(EffectType.GainResource, 2, ResourceType.Influence)
                .Build();

            public static Card DrawCard() => new CardBuilder()
                .WithName("draw_card")
                .WithCost(1)
                .WithAspect(CardAspect.Sorcery)
                .WithEffect(EffectType.DrawCard, 2)
                .Build();

            public static Card MoveUnitCard() => new CardBuilder()
                .WithName("move_unit")
                .WithCost(2)
                .WithAspect(CardAspect.Warlord)
                .WithEffect(EffectType.MoveUnit, 1)
                .Build();

            public static Card SupplantCard() => new CardBuilder()
                .WithName("supplant")
                .WithCost(4)
                .WithAspect(CardAspect.Shadow)
                .WithEffect(EffectType.Supplant, 1)
                .Build();
        }

        /// <summary>
        /// Common player configurations for testing.
        /// Each method returns a NEW player instance.
        /// </summary>
        public static class Players
        {
            public static Player RedPlayer() => new PlayerBuilder()
                .WithColor(PlayerColor.Red)
                .WithPower(10)
                .WithInfluence(10)
                .WithTroops(10)
                .WithSpies(5)
                .Build();

            public static Player BluePlayer() => new PlayerBuilder()
                .WithColor(PlayerColor.Blue)
                .WithPower(10)
                .WithInfluence(10)
                .WithTroops(10)
                .WithSpies(5)
                .Build();

            public static Player PoorPlayer() => new PlayerBuilder()
                .WithColor(PlayerColor.Red)
                .WithPower(0)
                .WithInfluence(0)
                .WithTroops(0)
                .WithSpies(0)
                .Build();

            public static Player RichPlayer() => new PlayerBuilder()
                .WithColor(PlayerColor.Red)
                .WithPower(100)
                .WithInfluence(100)
                .WithTroops(50)
                .WithSpies(20)
                .Build();
        }

        /// <summary>
        /// Common map node configurations for testing.
        /// Each method returns a NEW node instance.
        /// </summary>
        public static class MapNodes
        {
            public static MapNode Node1() => new MapNodeBuilder()
                .WithId(1)
                .At(10, 10)
                .Build();

            public static MapNode Node2() => new MapNodeBuilder()
                .WithId(2)
                .At(20, 10)
                .Build();

            public static MapNode Node3() => new MapNodeBuilder()
                .WithId(3)
                .At(30, 10)
                .Build();

            public static MapNode RedNode() => new MapNodeBuilder()
                .WithId(10)
                .At(100, 100)
                .OccupiedBy(PlayerColor.Red)
                .Build();

            public static MapNode BlueNode() => new MapNodeBuilder()
                .WithId(11)
                .At(110, 100)
                .OccupiedBy(PlayerColor.Blue)
                .Build();

            public static MapNode EmptyNode() => new MapNodeBuilder()
                .WithId(99)
                .At(200, 200)
                .Build();
        }

        /// <summary>
        /// Common site configurations for testing.
        /// Each method returns a NEW site instance.
        /// </summary>
        public static class Sites
        {
            public static CitySite PowerCity() => new CitySite(
                "Power City",
                ResourceType.Power,
                2,
                ResourceType.VictoryPoints,
                2
            );

            public static NonCitySite InfluenceSite() => new NonCitySite(
                "Influence Site",
                ResourceType.Influence,
                1,
                ResourceType.VictoryPoints,
                1
            );

            public static NonCitySite NeutralSite() => new NonCitySite(
                "Neutral Site",
                ResourceType.Power,
                1,
                ResourceType.Power,
                1
            );
        }
    }
}
