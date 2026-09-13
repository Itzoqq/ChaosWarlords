namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// The 5 single-primitive-shape test cards (test_assassin/test_guard/test_infiltrator/
    /// test_blade_dancer/test_displacer) used across the scenario-harness suite to exercise one
    /// stock EffectType (Assassinate/ReturnUnit/Supplant/MoveUnit, +GainResource for the Focus
    /// case) in isolation, with no card-specific mechanic muddying the assertion, plus
    /// core_noble (a plain, mandatory "promote a card played this turn" fixture used across ~35
    /// call sites as generic Devour/discard/inner-circle fodder). All 6 were moved out of
    /// production `cards.json`/`en_US.json` (planning.txt TIER 1 items 3 and 5 - they shipped
    /// in the real market data with no fixture flag; core_noble specifically doesn't correspond
    /// to any real scanned card at all - it predates this project's scan-based transcription
    /// discipline and was never a real card, unlike the actual "Noble" starting-deck card
    /// CardFactory.CreateNoble already implements separately) into this test-owned fixture,
    /// merged into MatchScenario's CardDatabase/LocalizationManager via LoadAdditionalFromJson
    /// rather than the normal production Load path.
    /// </summary>
    internal static class TestFixtureCards
    {
        public const string CardsJson = """
        [
          {
            "Id": "test_assassin",
            "Cost": 3,
            "Aspect": "Shadow",
            "DeckVP": 1,
            "InnerCircleVP": 3,
            "Effects": [
              { "Type": "Assassinate", "Amount": 1 }
            ]
          },
          {
            "Id": "core_noble",
            "Cost": 3,
            "Aspect": "Blasphemy",
            "DeckVP": 1,
            "InnerCircleVP": 3,
            "Effects": [
              { "Type": "Promote", "Amount": 1 }
            ]
          },
          {
            "Id": "test_guard",
            "Cost": 2,
            "Aspect": "Order",
            "DeckVP": 1,
            "InnerCircleVP": 2,
            "Effects": [
              { "Type": "ReturnUnit", "Amount": 1 }
            ]
          },
          {
            "Id": "test_infiltrator",
            "Cost": 5,
            "Aspect": "Shadow",
            "DeckVP": 2,
            "InnerCircleVP": 4,
            "Effects": [
              { "Type": "Supplant", "Amount": 1 }
            ]
          },
          {
            "Id": "test_blade_dancer",
            "Cost": 4,
            "Aspect": "Shadow",
            "DeckVP": 1,
            "InnerCircleVP": 3,
            "Effects": [
              { "Type": "Assassinate", "Amount": 1 },
              { "Type": "GainResource", "Amount": 3, "TargetResource": "Power", "RequiresFocus": true }
            ]
          },
          {
            "Id": "test_displacer",
            "Cost": 3,
            "Aspect": "Order",
            "DeckVP": 1,
            "InnerCircleVP": 3,
            "Effects": [
              { "Type": "MoveUnit", "Amount": 1 }
            ]
          }
        ]
        """;

        public const string LocalizationJson = """
        {
          "test_assassin_name": "Drow Assassin",
          "test_assassin_description": "Assassinate a troop",
          "core_noble_name": "Drow Noble",
          "core_noble_description": "Promote a card from your hand.",
          "test_guard_name": "City Guard",
          "test_guard_description": "Return a troop",
          "test_infiltrator_name": "Elite Infiltrator",
          "test_infiltrator_description": "Supplant a troop",
          "test_blade_dancer_name": "Shadow Blade Dancer",
          "test_blade_dancer_description": "Assassinate a troop. Focus - Gain 3 Power.",
          "test_displacer_name": "Displacer Beast",
          "test_displacer_description": "Move an enemy troop."
        }
        """;
    }
}
