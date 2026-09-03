using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Ogre Zombie ("Supplant a white troop anywhere on the
    /// board.") - the simplest vehicle for CardEffect.IgnoresPresenceRequirement, the new
    /// sibling primitive to TargetNeutralTroopOnly (see planning.txt section 2 / RESOLVED.txt).
    /// Loads the REAL "ogre_zombie" entry out of the REAL cards.json and dispatches every
    /// command through a REAL CommandDispatcher.
    ///
    /// HISTORY (see RESOLVED.txt/planning.txt) - a real production bug was found while writing
    /// this file, now FIXED: CardEffectData (the JSON DTO in CardDatabase.cs) had no
    /// IgnoresPresenceRequirement property at all, and CardFactory.ParseOptionalFlags never
    /// copied it onto the runtime CardEffect even where TargetNeutralTroopOnly WAS copied. The
    /// result was that cards.json said "IgnoresPresenceRequirement": true for ogre_zombie, but
    /// any CardEffect loaded through the REAL CardDatabase/CardFactory pipeline ended up with
    /// IgnoresPresenceRequirement == false regardless. CardEffectData now has the property and
    /// CardFactory.ParseOptionalFlags copies it (mirroring the existing TargetNeutralTroopOnly
    /// line). The underlying primitive itself
    /// (MapRuleEngine.CanAssassinate/HasValidAssassinationTarget, MapManager,
    /// SupplantStrategy.HasValidTargets, SupplantCommand.Validate,
    /// ActionInputController.HandleSupplant) was always correctly wired and is covered
    /// directly at the unit level (see MapRuleEngineTests/SupplantCommandTests/
    /// ActionInputControllerTests) - only the JSON->CardEffect plumbing was broken.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class OgreZombieScenarioTests
    {
        private static (Player red, MapNode targetNode) SetupRedWithAdjacentTroop(MatchScenario scenario, PlayerColor occupant)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var targetNode = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            targetNode.Occupant = occupant; // Setup only - not going through a command.

            return (red, targetNode);
        }

        // --- Regression: pins the JSON-wiring fix described in the class doc comment ---

        [TestMethod]
        public void OgreZombie_LoadedFromRealCardsJson_IgnoresPresenceRequirementIsTrue()
        {
            // FIXED (see RESOLVED.txt/planning.txt): cards.json says
            // "IgnoresPresenceRequirement": true for ogre_zombie's Supplant effect, and
            // CardEffectData/CardFactory now correctly carries that flag from JSON onto the
            // runtime CardEffect (CardFactory.ParseOptionalFlags copies it, mirroring the
            // existing TargetNeutralTroopOnly line). This test guards against a regression
            // silently losing that flag again.
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "ogre_zombie");

            var effect = card.Effects.First(e => e.Type == EffectType.Supplant);

            Assert.IsTrue(effect.IgnoresPresenceRequirement, "cards.json says IgnoresPresenceRequirement: true for ogre_zombie - the runtime CardEffect must carry that flag through from JSON.");
        }

        // --- Row 1: the headline behavior - previously blocked by the now-FIXED JSON-wiring bug above ---

        [TestMethod]
        public void PlayOgreZombie_NeutralTroopWithNoPresenceAnywhereNearby_StillSupplantsIt()
        {
            // The core new behavior: Red has NO troop and NO spy anywhere near the target -
            // an ordinary Supplant would have no valid target at all here.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var neutralTarget = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None);
            neutralTarget.Occupant = PlayerColor.Neutral; // Setup only - far from any Red presence.

            var card = scenario.GiveCard(PlayerColor.Red, "ogre_zombie");
            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "IgnoresPresenceRequirement should let a zero-Presence Neutral troop still count as a valid target.");

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(red.Color, neutralTarget.Occupant, "Red's troop should have Supplanted the Neutral one despite having no Presence there.");
            Assert.AreEqual(1, red.TrophyHall);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void SupplantCommand_TargetingNeutralTroopWithNoPresence_IsAcceptedWhileOgreZombieEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var neutralTarget = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None);
            neutralTarget.Occupant = PlayerColor.Neutral;

            var card = scenario.GiveCard(PlayerColor.Red, "ogre_zombie");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            long sequenceBefore = scenario.Context.SequenceNumber;
            scenario.Dispatch(new SupplantCommand(neutralTarget.Id, card.Id));

            Assert.AreNotEqual(sequenceBefore, scenario.Context.SequenceNumber, "A forged SupplantCommand against a zero-Presence Neutral troop should be ACCEPTED (this card overrides Presence) and mutate state.");
            Assert.AreEqual(red.Color, neutralTarget.Occupant);
        }

        // --- Row 1b: the mundane case (WITH Presence) - proves the card otherwise works ---

        [TestMethod]
        public void PlayOgreZombie_WithNeutralTroopAndNormalPresence_SupplantsTheNeutralTroop()
        {
            // Not exercising IgnoresPresenceRequirement at all (Presence is available anyway)
            // - proves Ogre Zombie's TargetNeutralTroopOnly filter and Supplant plumbing work
            // end to end via real dispatch, independent of the presence-override.
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "ogre_zombie");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(red.Color, neutralTarget.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 3: no valid target anywhere at all (Presence-override doesn't matter if there's no target) ---

        [TestMethod]
        public void PlayOgreZombie_NoNeutralTroopAnywhereOnTheBoard_SkipsSupplantEntirely()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "ogre_zombie");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(0, red.TrophyHall);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayOgreZombieCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "ogre_zombie");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        // --- Row 5: illegal targets rejected server-side (using a WITH-presence setup) ---

        [TestMethod]
        public void SupplantCommand_TargetingAnActualPlayersTroop_IsRejectedWhileOgreZombieEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 2);
            var neutralNode = site.NodesInternal[0];
            var blueTarget = site.NodesInternal[1];
            neutralNode.Occupant = PlayerColor.Neutral;
            blueTarget.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color); // Setup only - grants Presence at every node of this site.

            var card = scenario.GiveCard(PlayerColor.Red, "ogre_zombie");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new SupplantCommand(blueTarget.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A non-Neutral troop must be rejected while TargetNeutralTroopOnly is in effect - IgnoresPresenceRequirement never overrides the neutral-only filter.");

            Assert.AreEqual(PlayerColor.Blue, blueTarget.Occupant);
        }

        [TestMethod]
        public void SupplantCommand_TargetingANonexistentNode_IsRejectedWhileOgreZombieEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "ogre_zombie");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new SupplantCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand);
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void SupplantCommand_DispatchedTwiceAgainstTheNeutralTroop_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "ogre_zombie");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new SupplantCommand(neutralTarget.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(red.Color, neutralTarget.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }
    }
}
