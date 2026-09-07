using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Engine-level coverage for CardEffectProcessor.PushSuccessorEffect's chain propagation -
    /// not a specific shipped card (no cards.json entry currently chains a targeting effect
    /// beneath a non-targeting effect's OnSuccess). Confirms that shape still correctly opens
    /// real targeting and completes through the real click-dispatch path, using the exact same
    /// harness/setup pattern DeathbladeScenarioTests.cs uses for Assassinate.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class CardEffectProcessorChainTests
    {
        [TestMethod]
        public void GainResourceChainedIntoATargetingEffect_OpensTargetingAndCompletesThroughARealClick()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var redNode = scenario.Context.MapManager.Nodes.First(n =>
                scenario.Context.MapManager.CanDeployAt(n, red.Color) &&
                n.Neighbors.Any(neighbor => neighbor.Occupant == PlayerColor.None));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var target = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            target.Occupant = PlayerColor.Blue; // Setup only - not going through a command.

            var card = new Card("chain_test", "Chain Test", 0, CardAspect.Neutral, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Power)
            {
                OnSuccess = new CardEffect(EffectType.Assassinate, 1)
            });
            red.AddToHand(card);

            scenario.PlayCard(card);

            Assert.AreEqual(1, red.Power, "The GainResource half of the chain should have applied exactly once.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState,
                "The chained Assassinate effect must have opened real targeting, not been silently dropped.");

            scenario.ClickTarget(target, null);

            Assert.AreEqual(PlayerColor.None, target.Occupant, "The clicked target should actually have been assassinated.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }
    }
}
