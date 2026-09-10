using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using NSubstitute;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using System;
using System.Collections.Generic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;

namespace ChaosWarlords.Tests.Source.Systems
{
    // Covers CardEffectApplier - ApplyEffect and its EffectType-keyed handler dictionary, plus
    // DynamicAmountResolver (exercised through ApplyEffect's GainResource/DrawCard handlers).
    // Mirrors the production CardEffectProcessor/CardEffectApplier split - pipeline tests
    // (ResolveEffects and everything it calls) live in CardEffectProcessorTests.cs instead.
    [TestClass]

    [TestCategory("Unit")]
    public class CardEffectApplierTests
    {
        private MatchContext _context = null!;
        private Player _player = null!;

        [TestInitialize]
        public void Setup()
        {
            Tests.Utilities.TestLogger.Initialize();
            _player = TestData.Players.PoorPlayer();

            var turnSub = Substitute.For<ITurnManager>();
            turnSub.ActivePlayer.Returns(_player);
            // We need a real TurnContext for promotions
            turnSub.CurrentTurnContext.Returns(new TurnContext(_player, Tests.Utilities.TestLogger.Instance));

            _context = new MatchContext(
                turnSub,
                Substitute.For<IMapManager>(),
                Substitute.For<IMarketManager>(),
                Substitute.For<IActionSystem>(),
                Substitute.For<ICardDatabase>(),
                new PlayerStateManager(Tests.Utilities.TestLogger.Instance), // <--- Use real StateManager for logic testing
                Tests.Utilities.TestLogger.Instance
            );

            // Inject Partial Mock (Spy) for CardRuleEngine to allow mocking virtual methods if needed
            // But verify behavior primarily through Logic and ActionSystem mocks
            var ruleEngineSpy = Substitute.ForPartsOf<CardRuleEngine>(_context, Tests.Utilities.TestLogger.Instance);

            // Inject via Reflection (Private Setter)
            typeof(MatchContext).GetProperty("CardRuleEngine")!.SetValue(_context, ruleEngineSpy);
        }

        private static Site MakeSite(string name) => new NonCitySite(name, ResourceType.Power, 1, ResourceType.Power, 1);

        #region ShouldSkipDevourChain Tests (tested via ApplyEffect)

        [TestMethod]
        public void ApplyEffect_DevourChain_Proceeds_When_NoChainOrNonTargetingChain()
        {
            // Arrange: Devour effect without targeting chain (OnSuccess is null or non-targeting)
            var card = TestData.Cards.DevourCard();
            var devourEffect = card.Effects.Find(e => e.Type == EffectType.Devour);
            Assert.IsNotNull(devourEffect);
            devourEffect!.OnSuccess = new CardEffect(EffectType.GainResource, 2, ResourceType.Power); // Non-targeting

            _player.AddToHand(TestData.Cards.CheapCard());
            _player.AddToHand(TestData.Cards.CheapCard());
            // No manual mocks needed on CardRuleEngine - Logic + MapManager/ActionSystem Mocks handle it.
            // Since we use ForPartsOf, HasValidTargets executes real logic -> Checks Hand (Populated) -> Returns True.
            // GetStrategy returns Real DevourStrategy -> Executes ActionSystem.TryStartDevourHand.

            // Act
            CardEffectApplier.ApplyEffect(devourEffect, card, _context, Tests.Utilities.TestLogger.Instance);

            // Assert: Should proceed -> ActionSystem.TryStartDevourHand called (default implementation)
            _context.ActionSystem.Received(1).TryStartDevourHand(card, Arg.Any<Action>(), Arg.Any<bool>());
        }

        [TestMethod]
        public void ApplyEffect_DevourChain_SkipsWhen_DependentEffectHasNoValidTargets()
        {
            // Arrange: Devour with Supplant as OnSuccess, but no valid targets for Supplant
            var card = TestData.Cards.DevourCard();
            var devourEffect = card.Effects.Find(e => e.Type == EffectType.Devour);
            Assert.IsNotNull(devourEffect);
            devourEffect!.OnSuccess = new CardEffect(EffectType.Supplant, 0);

            _player.AddToHand(TestData.Cards.CheapCard());

            _player.AddToHand(TestData.Cards.CheapCard());

            // Mock: Supplant relies on MapManager/Game State.
            // SupplantStrategy checks player.TroopsInBarracks > 0 && MapManager.HasValidAssassinationTarget.
            // Setup gives player troops. We mock MapManager to return false.

            _context.MapManager.HasValidAssassinationTarget(_player).Returns(false);

            // Act
            CardEffectApplier.ApplyEffect(devourEffect, card, _context, Tests.Utilities.TestLogger.Instance);

            // Assert: Should SKIP -> ActionSystem.TryStartDevourHand NOT called
            _context.ActionSystem.DidNotReceive().TryStartDevourHand(Arg.Any<Card>(), Arg.Any<Action>(), Arg.Any<bool>());
        }

        [TestMethod]
        public void ApplyEffect_DevourChain_Proceeds_When_DependentEffectHasValidTargets()
        {
            // Arrange: Devour with Supplant as OnSuccess, WITH valid targets
            var card = TestData.Cards.DevourCard();
            var devourEffect = card.Effects.Find(e => e.Type == EffectType.Devour);
            Assert.IsNotNull(devourEffect);
            devourEffect!.OnSuccess = new CardEffect(EffectType.Supplant, 0);

            _player.AddToHand(TestData.Cards.CheapCard());

            _player.AddToHand(TestData.Cards.CheapCard());

            // Fix: SupplantStrategy checks TroopsInBarracks > 0.
            // PoorPlayer has 0. We need to inject troops.
            // Since setter is internal, use reflection or assuming internals visible.
            // Safest is reflection if we aren't sure about IVT.
            typeof(Player).GetProperty("TroopsInBarracks")!.SetValue(_player, 5);

            // Mock: Supplant HAS valid targets (MapManager returns true).
            _context.MapManager.HasValidAssassinationTarget(_player).Returns(true);

            // Act
            CardEffectApplier.ApplyEffect(devourEffect, card, _context, Tests.Utilities.TestLogger.Instance);

            // Assert: Should proceed -> ActionSystem.TryStartDevourHand called
            _context.ActionSystem.Received(1).TryStartDevourHand(card, Arg.Any<Action>(), Arg.Any<bool>());
        }


        #endregion

        #region ApplyPlaceSpy Tests (tested via ApplyEffect)

        [TestMethod]
        public void ApplyEffect_PlaceSpy_StartsTargeting_WhenValidSiteAndSpiesAvailable()
        {
            // Arrange
            var card = TestData.Cards.PlaceSpyCard();
            var placeSpyEffect = card.Effects.Find(e => e.Type == EffectType.PlaceSpy);
            Assert.IsNotNull(placeSpyEffect);

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(true);
            _player.SpiesInBarracks = 1;

            // Act
            CardEffectApplier.ApplyEffect(placeSpyEffect!, card, _context, Tests.Utilities.TestLogger.Instance);

            // Assert
            _context.ActionSystem.Received(1).StartTargeting(ActionState.TargetingPlaceSpy, card);
        }

        [TestMethod]
        public void ApplyEffect_PlaceSpy_LogsWarning_WhenNoSpiesInBarracks()
        {
            // Arrange
            var card = TestData.Cards.PlaceSpyCard();
            var placeSpyEffect = card.Effects.Find(e => e.Type == EffectType.PlaceSpy);
            Assert.IsNotNull(placeSpyEffect);

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(true);
            _player.SpiesInBarracks = 0; // No spies available

            var mockLogger = Substitute.For<IGameLogger>();

            // Act
            CardEffectApplier.ApplyEffect(placeSpyEffect!, card, _context, mockLogger);

            // Assert
            mockLogger.Received().Log(Arg.Is<string>(s => s.Contains("No Spies in Barracks")), LogChannel.Warning);
            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ApplyEffect_PlaceSpy_LogsWarning_WhenNoValidSites()
        {
            // Arrange
            var card = TestData.Cards.PlaceSpyCard();
            var placeSpyEffect = card.Effects.Find(e => e.Type == EffectType.PlaceSpy);
            Assert.IsNotNull(placeSpyEffect);

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(false); // No valid sites
            _player.SpiesInBarracks = 1;

            var mockLogger = Substitute.For<IGameLogger>();

            // Act
            CardEffectApplier.ApplyEffect(placeSpyEffect!, card, _context, mockLogger);

            // Assert
            mockLogger.Received().Log(Arg.Is<string>(s => s.Contains("No valid sites")), LogChannel.Warning);
            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ApplyEffect_PlaceSpy_DoesNotStartTargeting_WhenBothConstraintsFail()
        {
            // Arrange
            var card = TestData.Cards.PlaceSpyCard();
            var placeSpyEffect = card.Effects.Find(e => e.Type == EffectType.PlaceSpy);
            Assert.IsNotNull(placeSpyEffect);

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(false);
            _player.SpiesInBarracks = 0;

            // Act
            CardEffectApplier.ApplyEffect(placeSpyEffect!, card, _context, Tests.Utilities.TestLogger.Instance);

            // Assert
            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ApplyEffect_GainResource_UnwiredDynamicAmountSource_LogsWarningAndResolvesToZero()
        {
            // A DynamicAmountSource value with no matching case in
            // DynamicAmountResolver.ResolveAmount (simulated here via an out-of-range cast,
            // standing in for "added to GameEnums.cs before its ResolveAmount arm exists yet")
            // must not silently resolve to a permanent, unexplained 0 - it should log loudly so
            // this is easy to spot instead of looking like a card that "does nothing".
            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 0, ResourceType.VictoryPoints)
            {
                DynamicAmountSource = (DynamicAmountSource)999
            };
            card.AddEffect(effect);

            var mockLogger = Substitute.For<IGameLogger>();

            CardEffectApplier.ApplyEffect(effect, card, _context, mockLogger);

            mockLogger.Received().Log(Arg.Is<string>(s => s.Contains("no case wired for DynamicAmountSource")), LogChannel.Warning);
            Assert.AreEqual(0, _player.VictoryPoints, "An unwired dynamic amount source must resolve to 0, not throw or grant an arbitrary amount.");
        }

        [TestMethod]
        public void ApplyEffect_GainResource_TrophyHallCountSource_FloorsDivisionAgainstTotalTrophyHall()
        {
            // Beholder: "Gain Influence for every 3 troops in your trophy hall" - 7 troops / 3
            // must floor to 2 Influence, not round to 3, and must count ALL colors (not just
            // one), matching Player.TrophyHall's own definition (sum across TrophyHallByColor).
            _player.SetTrophyHall(new Dictionary<PlayerColor, int> { [PlayerColor.Neutral] = 4, [PlayerColor.Blue] = 3 });
            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 0, ResourceType.Influence)
            {
                DynamicAmountSource = DynamicAmountSource.TrophyHallCount,
                DynamicAmountDivisor = 3
            };
            card.AddEffect(effect);
            int influenceBefore = _player.Influence;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(influenceBefore + 2, _player.Influence, "7 total troops / 3 must floor to 2 Influence.");
        }

        [TestMethod]
        public void ApplyEffect_GainResource_TrophyHallCountSource_EmptyTrophyHallGrantsZero()
        {
            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 0, ResourceType.Influence)
            {
                DynamicAmountSource = DynamicAmountSource.TrophyHallCount,
                DynamicAmountDivisor = 3
            };
            card.AddEffect(effect);
            int influenceBefore = _player.Influence;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(influenceBefore, _player.Influence, "An empty trophy hall must resolve to 0 Influence, not throw or fall back to a default amount.");
        }

        [TestMethod]
        public void ApplyEffect_GainResource_NonPositiveDynamicAmountDivisor_LogsWarningAndClampsToOne()
        {
            // A "0" (or negative) DynamicAmountDivisor - e.g. a cards.json typo for the intended
            // divisor - must not silently grant the full un-divided count with no trace in the
            // log, the same reasoning as the unwired-DynamicAmountSource warning above.
            _player.SetTrophyHall(6, PlayerColor.Neutral);
            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 0, ResourceType.Influence)
            {
                DynamicAmountSource = DynamicAmountSource.TrophyHallCount,
                DynamicAmountDivisor = 0
            };
            card.AddEffect(effect);
            int influenceBefore = _player.Influence;
            var mockLogger = Substitute.For<IGameLogger>();

            CardEffectApplier.ApplyEffect(effect, card, _context, mockLogger);

            mockLogger.Received().Log(Arg.Is<string>(s => s.Contains("DynamicAmountDivisor") && s.Contains("not positive")), LogChannel.Warning);
            Assert.AreEqual(influenceBefore + 6, _player.Influence, "A non-positive divisor must clamp to 1, not divide by zero or silently no-op.");
        }

        [TestMethod]
        public void ApplyEffect_GainResource_PlayerTrophyHallCountSource_ExcludesNeutralTroopsAndFloors()
        {
            // Death Knight: "Gain 1 VP for every 5 PLAYER troops in your trophy hall" - the
            // printed wording explicitly excludes captured white/unaligned troops, unlike
            // Beholder's TrophyHallCount (every troop, any color). 11 Blue + 7 Neutral must
            // count only the 11 Blue ones: 11 / 5 = 2, not 18 / 5 = 3.
            _player.SetTrophyHall(new Dictionary<PlayerColor, int> { [PlayerColor.Neutral] = 7, [PlayerColor.Blue] = 11 });
            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 0, ResourceType.VictoryPoints)
            {
                DynamicAmountSource = DynamicAmountSource.PlayerTrophyHallCount,
                DynamicAmountDivisor = 5
            };
            card.AddEffect(effect);
            int vpBefore = _player.VictoryPoints;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(vpBefore + 2, _player.VictoryPoints, "Only the 11 Blue (non-Neutral) troops should count: 11 / 5 = 2 VP.");
        }

        [TestMethod]
        public void ApplyEffect_GainResource_PlayerTrophyHallCountSource_OnlyNeutralTroopsGrantsZero()
        {
            _player.SetTrophyHall(20, PlayerColor.Neutral); // Plenty of trophies, but none from a player.
            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 0, ResourceType.VictoryPoints)
            {
                DynamicAmountSource = DynamicAmountSource.PlayerTrophyHallCount,
                DynamicAmountDivisor = 5
            };
            card.AddEffect(effect);
            int vpBefore = _player.VictoryPoints;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(vpBefore, _player.VictoryPoints, "A trophy hall made up entirely of Neutral troops must grant 0 VP for PlayerTrophyHallCount.");
        }

        [TestMethod]
        public void ApplyEffect_GainResource_InnerCircleCountSource_FloorsDivisionAgainstCurrentInnerCircle()
        {
            // Vampire: "...promote a card, then gain 1 VP for every 3 cards in your inner
            // circle" - 7 cards / 3 must floor to 2 VP, not round to 3.
            for (int i = 0; i < 7; i++)
            {
                _player.AddToInnerCircle(new Card($"test-inner-{i}", $"Test Inner {i}", 1, CardAspect.Neutral, 0, 0, 0));
            }
            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 0, ResourceType.VictoryPoints)
            {
                DynamicAmountSource = DynamicAmountSource.InnerCircleCount,
                DynamicAmountDivisor = 3
            };
            card.AddEffect(effect);
            int vpBefore = _player.VictoryPoints;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(vpBefore + 2, _player.VictoryPoints, "7 inner circle cards / 3 must floor to 2 VP.");
        }

        [TestMethod]
        public void ApplyEffect_GainResource_InnerCircleCountSource_EmptyInnerCircleGrantsZero()
        {
            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 0, ResourceType.VictoryPoints)
            {
                DynamicAmountSource = DynamicAmountSource.InnerCircleCount,
                DynamicAmountDivisor = 3
            };
            card.AddEffect(effect);
            int vpBefore = _player.VictoryPoints;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(vpBefore, _player.VictoryPoints, "An empty inner circle must resolve to 0 VP, not throw or fall back to a default amount.");
        }

        [TestMethod]
        public void ApplyEffect_DrawCard_SpiesOnBoardSource_DrawsOneCardPerSiteWithActivePlayersSpy()
        {
            // Aboleth: "Draw a card for each spy you have on the board" - a 1:1 count, not a
            // divided one (DynamicAmountDivisor defaults to 1). Only sites carrying the ACTIVE
            // player's own spy should count - a site with no spy, or only an opponent's, must not.
            var siteWithSpy1 = MakeSite("A");
            siteWithSpy1.AddSpy(_player.Color);
            var siteWithSpy2 = MakeSite("B");
            siteWithSpy2.AddSpy(_player.Color);
            var siteWithOpponentSpy = MakeSite("C");
            siteWithOpponentSpy.AddSpy(PlayerColor.Blue);
            var siteWithNoSpy = MakeSite("D");
            _context.MapManager.Sites.Returns(new List<Site> { siteWithSpy1, siteWithSpy2, siteWithOpponentSpy, siteWithNoSpy });

            for (int i = 0; i < 5; i++)
            {
                _player.DeckManager.AddToTop(new Card($"deck-{i}", $"Deck {i}", 1, CardAspect.Neutral, 0, 0, 0));
            }
            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.DrawCard, 0)
            {
                DynamicAmountSource = DynamicAmountSource.SpiesOnBoard
            };
            card.AddEffect(effect);
            int handSizeBefore = _player.Hand.Count;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.HasCount(handSizeBefore + 2, _player.Hand, "Only the 2 sites carrying the active player's own spy should count - the opponent's spy and the empty site must not.");
        }

        [TestMethod]
        public void ApplyEffect_DrawCard_SpiesOnBoardSource_NoSpiesAnywhereDrawsZero()
        {
            _context.MapManager.Sites.Returns(new List<Site> { MakeSite("A"), MakeSite("B") });
            _player.DeckManager.AddToTop(new Card("deck-0", "Deck 0", 1, CardAspect.Neutral, 0, 0, 0));
            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.DrawCard, 0)
            {
                DynamicAmountSource = DynamicAmountSource.SpiesOnBoard
            };
            card.AddEffect(effect);
            int handSizeBefore = _player.Hand.Count;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.HasCount(handSizeBefore, _player.Hand, "No spies on the board anywhere must draw 0 cards, not throw or fall back to a default amount.");
        }

        [TestMethod]
        public void ApplyEffect_DrawCard_WithoutDynamicAmountSource_StillUsesFixedAmount()
        {
            // Regression guard: generalizing ApplyDrawCard to route through ResolveAmount must
            // not change behavior for a plain, non-dynamic DrawCard effect (DynamicAmountSource
            // defaults to None, which ResolveAmount short-circuits back to effect.Amount as-is).
            for (int i = 0; i < 3; i++)
            {
                _player.DeckManager.AddToTop(new Card($"deck-{i}", $"Deck {i}", 1, CardAspect.Neutral, 0, 0, 0));
            }
            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.DrawCard, 2);
            card.AddEffect(effect);
            int handSizeBefore = _player.Hand.Count;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.HasCount(handSizeBefore + 2, _player.Hand, "A fixed Amount (no DynamicAmountSource) must still draw exactly that many cards.");
        }

        [TestMethod]
        public void ApplyEffect_GainResource_NeutralTrophyHallCountSource_CountsOnlyNeutralTroopsAndFloors()
        {
            // Black Dragon: "Gain 1 VP for every 3 WHITE troops in your trophy hall" - the
            // mirror image of PlayerTrophyHallCount (Death Knight): only Neutral troops count,
            // not any actual player's. 10 Neutral + 11 Blue must count only the 10 Neutral: 10 /
            // 3 = 3, not 21 / 3 = 7.
            _player.SetTrophyHall(new Dictionary<PlayerColor, int> { [PlayerColor.Neutral] = 10, [PlayerColor.Blue] = 11 });
            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 0, ResourceType.VictoryPoints)
            {
                DynamicAmountSource = DynamicAmountSource.NeutralTrophyHallCount,
                DynamicAmountDivisor = 3
            };
            card.AddEffect(effect);
            int vpBefore = _player.VictoryPoints;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(vpBefore + 3, _player.VictoryPoints, "Only the 10 Neutral troops should count: 10 / 3 = 3 VP.");
        }

        [TestMethod]
        public void ApplyEffect_GainResource_NeutralTrophyHallCountSource_OnlyPlayerTroopsGrantsZero()
        {
            _player.SetTrophyHall(new Dictionary<PlayerColor, int> { [PlayerColor.Blue] = 20 }); // Plenty of trophies, but none Neutral.
            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 0, ResourceType.VictoryPoints)
            {
                DynamicAmountSource = DynamicAmountSource.NeutralTrophyHallCount,
                DynamicAmountDivisor = 3
            };
            card.AddEffect(effect);
            int vpBefore = _player.VictoryPoints;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(vpBefore, _player.VictoryPoints, "A trophy hall with no Neutral troops at all must grant 0 VP for NeutralTrophyHallCount.");
        }

        #endregion

        #region ApplyPromote PromotionCompletionEffect Tests (Blue Dragon)

        [TestMethod]
        public void ApplyEffect_Promote_WithPromotionCompletionEffect_RegistersItOnTurnContextWithoutApplyingItImmediately()
        {
            // Blue Dragon: "...then gain 1 VP for every 3 cards in your inner circle" - must be
            // registered for MatchManager.EndTurn to apply LATER (after the deferred redemption
            // actually happens), not applied the moment this Promote node itself resolves
            // (which happens at PLAY time - see CardEffect.PromotionCompletionEffect's own doc
            // comment for why).
            var card = new Card("test-blue-dragon", "Test Blue Dragon", 1, CardAspect.Neutral, 0, 0, 0);
            var completionEffect = new CardEffect(EffectType.GainResource, 0, ResourceType.VictoryPoints)
            {
                DynamicAmountSource = DynamicAmountSource.InnerCircleCount,
                DynamicAmountDivisor = 3
            };
            var effect = new CardEffect(EffectType.Promote, 2, ResourceType.None)
            {
                PromotionCreditIsOptional = true,
                PromotionCompletionEffect = completionEffect
            };
            card.AddEffect(effect);
            for (int i = 0; i < 6; i++)
            {
                _player.AddToInnerCircle(new Card($"test-inner-{i}", $"Test Inner {i}", 1, CardAspect.Neutral, 0, 0, 0));
            }
            int vpBefore = _player.VictoryPoints;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(vpBefore, _player.VictoryPoints, "The completion effect must NOT fire immediately when Promote itself resolves.");
            var drained = _context.TurnManager.CurrentTurnContext.DrainPromotionCompletionEffects();
            Assert.HasCount(1, drained, "The completion effect must have been registered exactly once.");
            Assert.AreSame(card, drained[0].Source);
            Assert.AreSame(completionEffect, drained[0].Effect);
        }

        [TestMethod]
        public void ApplyEffect_Promote_WithoutPromotionCompletionEffect_RegistersNothing()
        {
            // core_noble/Cultist of Myrkul/Zuggtmoy - a plain Promote effect (no
            // PromotionCompletionEffect authored) must not register anything for
            // MatchManager.EndTurn to apply - every existing Promote card is unaffected by this
            // primitive.
            var card = TestData.Cards.NobleCard();
            var effect = card.Effects[0];

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.IsEmpty(_context.TurnManager.CurrentTurnContext.DrainPromotionCompletionEffects());
        }

        #endregion

        #region ApplyGainResource SitesUnderTotalControl Tests (Red Dragon)

        [TestMethod]
        public void ApplyEffect_GainResource_SitesUnderTotalControlSource_OnlyCountsSitesWithBothControlAndTotalControl()
        {
            // Red Dragon: "Gain 1 VP for each site under your total control" - a stricter subset
            // of plain SitesControlled (mere majority-troop control).
            var controlledAndTotal = MakeSite("A");
            controlledAndTotal.Owner = _player.Color;
            controlledAndTotal.HasTotalControl = true;
            var controlledOnly = MakeSite("B"); // Controlled, but NOT total control - must not count.
            controlledOnly.Owner = _player.Color;
            controlledOnly.HasTotalControl = false;
            var neitherA = MakeSite("C"); // Not even controlled - must not count.
            _context.MapManager.Sites.Returns(new List<Site> { controlledAndTotal, controlledOnly, neitherA });

            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 0, ResourceType.VictoryPoints)
            {
                DynamicAmountSource = DynamicAmountSource.SitesUnderTotalControl
            };
            card.AddEffect(effect);
            int vpBefore = _player.VictoryPoints;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(vpBefore + 1, _player.VictoryPoints, "Only the 1 site with BOTH Owner == player AND HasTotalControl should count.");
        }

        [TestMethod]
        public void ApplyEffect_GainResource_SitesUnderTotalControlSource_NoTotalControlAnywhereGrantsZero()
        {
            var controlledOnly = MakeSite("A");
            controlledOnly.Owner = _player.Color;
            controlledOnly.HasTotalControl = false;
            _context.MapManager.Sites.Returns(new List<Site> { controlledOnly });

            var card = new Card("test-dynamic", "Test Dynamic", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 0, ResourceType.VictoryPoints)
            {
                DynamicAmountSource = DynamicAmountSource.SitesUnderTotalControl
            };
            card.AddEffect(effect);
            int vpBefore = _player.VictoryPoints;

            CardEffectApplier.ApplyEffect(effect, card, _context, Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(vpBefore, _player.VictoryPoints, "Plain control without total control must grant 0 VP, not throw or fall back to a default amount.");
        }

        #endregion
    }
}
