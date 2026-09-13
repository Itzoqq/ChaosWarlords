namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// The 5 single-primitive-shape test cards (test_assassin/test_guard/test_infiltrator/
    /// test_blade_dancer/test_displacer) used across the scenario-harness suite to exercise one
    /// stock EffectType (Assassinate/ReturnUnit/Supplant/MoveUnit, +GainResource for the Focus
    /// case) in isolation, with no card-specific mechanic muddying the assertion, plus 2 cards
    /// that don't correspond to any real scanned card at all and predate this project's
    /// scan-based transcription discipline: core_noble (a plain, mandatory "promote a card
    /// played this turn" fixture used across ~35 call sites as generic Devour/discard/inner-
    /// circle fodder - the actual "Noble" starting-deck card is already correctly implemented
    /// separately by CardFactory.CreateNoble) and market_corruptor (a "devour a market card to
    /// gain Influence" card with its own dedicated MarketCorruptorScenarioTests.cs, but no match
    /// anywhere across all 5 organized half-deck scan folders - Carrion Crawler's real,
    /// scan-verified "devour a market card and replace it with this card" is the closest genuine
    /// card with a similar shape). All 7 were moved out of production `cards.json`/`en_US.json`
    /// (planning.txt TIER 1 items 3 and 5) into this test-owned fixture, merged into
    /// MatchScenario's CardDatabase/LocalizationManager via LoadAdditionalFromJson rather than
    /// the normal production Load path. None of these need a HalfDeck tag - they never belong
    /// to any of the 6 selectable market half-decks (MarketHalfDeck), by construction.
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
          },
          {
            "Id": "market_corruptor",
            "Cost": 3,
            "Aspect": "Sorcery",
            "DeckVP": 1,
            "InnerCircleVP": 2,
            "Effects": [
              {
                "Type": "Devour",
                "Amount": 1,
                "TargetLocation": "Market",
                "IsOptional": true,
                "OnSuccess": { "Type": "GainResource", "Amount": 3, "TargetResource": "Influence" }
              }
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
          "test_displacer_description": "Move an enemy troop.",
          "market_corruptor_name": "Market Corruptor",
          "market_corruptor_description": "You may devour a card from the Market to gain 3 Influence."
        }
        """;
    }
}
