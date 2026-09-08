using NSubstitute;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Integration.Factories
{
    [TestClass]

    [TestCategory("Integration")]
    public class CardFactoryTests
    {
        private static readonly ILocalizationService _localization = new TestLocalizationService(new()
        {
            ["priestess_of_lolth_name"] = "Priestess of Lolth",
            ["priestess_of_lolth_description"] = "A test card",
            ["test_card_name"] = "Test Card",
            ["test_card_description"] = "Test effects",
            ["old_card_name"] = "Old Card",
            ["old_card_description"] = "Old description",
            ["inf_test_name"] = "Influence Test",
            ["inf_test_description"] = "Influence test",
            ["choose_count_card_name"] = "Choose Count Card",
            ["choose_count_card_description"] = "Test card for ChooseCount",
            ["chained_repeat_card_name"] = "Chained Repeat Card",
            ["chained_repeat_card_description"] = "Test card for ChainedRepeatCount",
            ["gain_resource_per_repeat_card_name"] = "Gain Resource Per Repeat Card",
            ["gain_resource_per_repeat_card_description"] = "Test card for GainResourcePerRepeat",
            ["promotion_completion_effect_card_name"] = "Promotion Completion Effect Card",
            ["promotion_completion_effect_card_description"] = "Test card for PromotionCompletionEffect",
        });

        [TestMethod]
        public void CreateFromData_ParsesPropertiesCorrectly()
        {
            // ARRANGE
            var cardData = new CardData
            {
                Id = "priestess_of_lolth",
                Cost = 2,
                Aspect = "Sorcery",
                DeckVP = 1,
                InnerCircleVP = 0,
                Effects = new List<CardEffectData>()
            };

            // ACT
            var card = CardFactory.CreateFromData(cardData, _localization);

            // ASSERT
            Assert.IsNotNull(card);
            StringAssert.StartsWith(card.Id, "priestess_of_lolth");
            Assert.AreEqual("Priestess of Lolth", card.Name);
            Assert.AreEqual(2, card.Cost);
            Assert.AreEqual(CardAspect.Sorcery, card.Aspect);

            Assert.AreEqual(1, card.DeckVP);
            Assert.AreEqual("A test card", card.Description);
        }

        [TestMethod]
        public void CreateFromData_ParsesEffectsCorrectly()
        {
            // ARRANGE
            var cardData = new CardData
            {
                Id = "test_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData { Type = "Promote", Amount = 1, TargetResource = "None" },
                    new CardEffectData { Type = "GainResource", Amount = 2, TargetResource = "Power" }
                }
            };

            // ACT
            var card = CardFactory.CreateFromData(cardData, _localization);

            // ASSERT
            Assert.HasCount(2, card.Effects);

            var effect1 = card.Effects[0];
            Assert.AreEqual(EffectType.Promote, effect1.Type);
            Assert.AreEqual(1, effect1.Amount);
            Assert.AreEqual(ResourceType.None, effect1.TargetResource);

            var effect2 = card.Effects[1];
            Assert.AreEqual(EffectType.GainResource, effect2.Type);
            Assert.AreEqual(2, effect2.Amount);
            Assert.AreEqual(ResourceType.Power, effect2.TargetResource);
        }

        [TestMethod]
        public void CreateSoldier_CreatesCorrectCard()
        {
            var card = CardFactory.CreateSoldier();
            StringAssert.StartsWith(card.Id, "soldier");
            Assert.AreEqual(EffectType.GainResource, card.Effects[0].Type);
            Assert.AreEqual(ResourceType.Power, card.Effects[0].TargetResource);
        }

        [TestMethod]
        public void CreateNoble_CreatesCorrectCard()
        {
            var card = CardFactory.CreateNoble();
            StringAssert.StartsWith(card.Id, "noble");
            Assert.AreEqual(EffectType.GainResource, card.Effects[0].Type);
            Assert.AreEqual(ResourceType.Influence, card.Effects[0].TargetResource);
        }

        [TestMethod]
        public void CreateFromData_DefaultsMissingVPsToZero()
        {
            // Scenario: Loading old JSON data where DeckVP/InnerCircleVP properties don't exist
            // C# object initializer leaves them as default (0)
            var cardData = new CardData
            {
                Id = "old_card",
                Cost = 1,
                Aspect = "Neutral",
                Effects = new List<CardEffectData>()
            };

            var card = CardFactory.CreateFromData(cardData, _localization);

            Assert.AreEqual(0, card.DeckVP, "Missing DeckVP should default to 0");
            Assert.AreEqual(0, card.InnerCircleVP, "Missing InnerCircleVP should default to 0");
        }

        [TestMethod]
        public void CreateFromData_SetsDefaultInfluenceToZero()
        {
            // Current Factory implementation hardcodes Influence to 0
            // This test ensures that stays true until we explicitly update CardData
            var cardData = new CardData
            {
                Id = "inf_test",
                Cost = 1,
                Aspect = "Neutral",
                Effects = new List<CardEffectData>()
            };

            var card = CardFactory.CreateFromData(cardData, _localization);

            Assert.AreEqual(0, card.InfluenceValue);
        }

        // --- CardEffect.ChooseCount (Weaponmaster's "choose N times" primitive) load-time
        // validation - see CardFactory.WarnIfChooseCountShapeIsUnsupported ---

        [TestMethod]
        public void CreateFromData_ChooseCountParsesOntoTheEffect()
        {
            var cardData = new CardData
            {
                Id = "choose_count_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Type = "GainResource", Amount = 1, TargetResource = "Troops", IsOptional = true, ChooseCount = 3,
                        Alternative = new CardEffectData { Type = "Assassinate", Amount = 1 }
                    }
                }
            };

            var card = CardFactory.CreateFromData(cardData, _localization);

            Assert.AreEqual(3, card.Effects[0].ChooseCount);
        }

        [TestMethod]
        public void CreateFromData_ChooseCountUnusuallyLarge_LogsAWarning()
        {
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "choose_count_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Type = "GainResource", Amount = 1, TargetResource = "Troops", IsOptional = true, ChooseCount = 300,
                        Alternative = new CardEffectData { Type = "Assassinate", Amount = 1, TargetNeutralTroopOnly = true }
                    }
                }
            };

            var card = CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.Received(1).Log(Arg.Is<string>(s => s.Contains("unusually large")), LogChannel.Warning);
            Assert.AreEqual(300, card.Effects[0].ChooseCount, "The warning is advisory only - the authored value is not clamped.");
        }

        [TestMethod]
        public void CreateFromData_ChooseCountWithoutAlternative_LogsAWarning()
        {
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "choose_count_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData { Type = "GainResource", Amount = 1, TargetResource = "Troops", IsOptional = true, ChooseCount = 3 }
                }
            };

            CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.Received(1).Log(Arg.Is<string>(s => s.Contains("no Alternative")), LogChannel.Warning);
        }

        [TestMethod]
        public void CreateFromData_ChooseCountWithAChainThatWouldBeOverridden_LogsAWarning()
        {
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "choose_count_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Type = "GainResource", Amount = 1, TargetResource = "Troops", IsOptional = true, ChooseCount = 3,
                        Alternative = new CardEffectData
                        {
                            Type = "Assassinate",
                            Amount = 1,
                            OnSuccess = new CardEffectData { Type = "GainResource", Amount = 1, TargetResource = "Power" }
                        }
                    }
                }
            };

            CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.Received(1).Log(Arg.Is<string>(s => s.Contains("only honored on the LAST round")), LogChannel.Warning);
        }

        [TestMethod]
        public void CreateFromData_ChooseCountWithAWellFormedChoicePair_LogsNoWarning()
        {
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "choose_count_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Type = "GainResource", Amount = 1, TargetResource = "Troops", IsOptional = true, ChooseCount = 3,
                        Alternative = new CardEffectData { Type = "Assassinate", Amount = 1, TargetNeutralTroopOnly = true }
                    }
                }
            };

            CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.DidNotReceiveWithAnyArgs().Log(default(string)!, default);
        }

        // --- CardEffect.ChainedRepeatCount (Graz'zt's "return any number, Supplant at each
        // site" primitive) load-time validation - see
        // CardFactory.WarnIfChainedRepeatCountShapeIsUnsupported ---

        [TestMethod]
        public void CreateFromData_ChainedRepeatCountParsesOntoTheEffect()
        {
            var cardData = new CardData
            {
                Id = "chained_repeat_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Type = "ReturnOwnSpy", Amount = 1, IsOptional = true, ChainedRepeatCount = 5,
                        OnSuccess = new CardEffectData { Type = "Supplant", Amount = 1 }
                    }
                }
            };

            var card = CardFactory.CreateFromData(cardData, _localization);

            Assert.AreEqual(5, card.Effects[0].ChainedRepeatCount);
        }

        [TestMethod]
        public void CreateFromData_ChainedRepeatCountUnusuallyLarge_LogsAWarning()
        {
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "chained_repeat_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Type = "ReturnOwnSpy", Amount = 1, IsOptional = true, ChainedRepeatCount = 300,
                        OnSuccess = new CardEffectData { Type = "Supplant", Amount = 1 }
                    }
                }
            };

            var card = CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.Received(1).Log(Arg.Is<string>(s => s.Contains("unusually large")), LogChannel.Warning);
            Assert.AreEqual(300, card.Effects[0].ChainedRepeatCount, "The warning is advisory only - the authored value is not clamped.");
        }

        [TestMethod]
        public void CreateFromData_ChainedRepeatCountWithoutOnSuccess_LogsAWarning()
        {
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "chained_repeat_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData { Type = "ReturnOwnSpy", Amount = 1, IsOptional = true, ChainedRepeatCount = 5 }
                }
            };

            CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.Received(1).Log(Arg.Is<string>(s => s.Contains("no OnSuccess")), LogChannel.Warning);
        }

        [TestMethod]
        public void CreateFromData_ChainedRepeatCountWithoutIsOptional_LogsAWarning()
        {
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "chained_repeat_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Type = "ReturnOwnSpy", Amount = 1, ChainedRepeatCount = 5,
                        OnSuccess = new CardEffectData { Type = "Supplant", Amount = 1 }
                    }
                }
            };

            CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.Received(1).Log(Arg.Is<string>(s => s.Contains("not IsOptional")), LogChannel.Warning);
        }

        [TestMethod]
        public void CreateFromData_ChainedRepeatCountWithAWellFormedPair_LogsNoWarning()
        {
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "chained_repeat_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Type = "ReturnOwnSpy", Amount = 1, IsOptional = true, ChainedRepeatCount = 5,
                        OnSuccess = new CardEffectData { Type = "Supplant", Amount = 1 }
                    }
                }
            };

            CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.DidNotReceiveWithAnyArgs().Log(default(string)!, default);
        }

        // --- CardEffect.GainResourcePerRepeat (Death Tyrant's "for each troop removed, gain
        // Influence" primitive) load-time validation - see
        // CardFactory.WarnIfGainResourcePerRepeatShapeIsUnsupported ---

        [TestMethod]
        public void CreateFromData_GainResourcePerRepeatParsesOntoTheEffect()
        {
            var cardData = new CardData
            {
                Id = "gain_resource_per_repeat_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData { Type = "Assassinate", Amount = 3, GainResourcePerRepeat = "Influence" }
                }
            };

            var card = CardFactory.CreateFromData(cardData, _localization);

            Assert.AreEqual(ResourceType.Influence, card.Effects[0].GainResourcePerRepeat);
        }

        [TestMethod]
        public void CreateFromData_GainResourcePerRepeatWithUnparseableValue_LogsAWarning()
        {
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "gain_resource_per_repeat_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData { Type = "Assassinate", Amount = 3, GainResourcePerRepeat = "NotARealResource" }
                }
            };

            var card = CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.Received(1).Log(Arg.Is<string>(s => s.Contains("FAILED to parse GainResourcePerRepeat")), LogChannel.Warning);
            Assert.AreEqual(ResourceType.None, card.Effects[0].GainResourcePerRepeat);
        }

        [TestMethod]
        public void CreateFromData_GainResourcePerRepeatOnAssassinate_LogsNoWarning()
        {
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "gain_resource_per_repeat_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData { Type = "Assassinate", Amount = 3, GainResourcePerRepeat = "Influence" }
                }
            };

            CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.DidNotReceiveWithAnyArgs().Log(default(string)!, default);
        }

        [TestMethod]
        public void CreateFromData_GainResourcePerRepeatOnANonAssassinateEffect_LogsAWarning()
        {
            // GainResourcePerRepeat is only ever read by ActionSystem.PerformAssassinate -
            // authoring it on any other effect type would parse and clone fine but silently
            // never fire.
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "gain_resource_per_repeat_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData { Type = "Supplant", Amount = 1, GainResourcePerRepeat = "Influence" }
                }
            };

            CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.Received(1).Log(Arg.Is<string>(s => s.Contains("only wired for EffectType.Assassinate")), LogChannel.Warning);
        }

        // --- CardEffect.PromotionCompletionEffect (Blue Dragon's "...then gain 1 VP for every
        // 3 cards in your inner circle" primitive) load-time parsing/validation - see
        // CardFactory.ParsePromotionCompletionEffect/WarnIfPromotionCompletionEffectShapeIsUnsupported ---

        [TestMethod]
        public void CreateFromData_PromotionCompletionEffectParsesOntoTheEffect()
        {
            var cardData = new CardData
            {
                Id = "promotion_completion_effect_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Type = "Promote",
                        Amount = 2,
                        PromotionCreditIsOptional = true,
                        PromotionCompletionEffect = new CardEffectData
                        {
                            Type = "GainResource",
                            TargetResource = "VictoryPoints",
                            DynamicAmountSource = "InnerCircleCount",
                            DynamicAmountDivisor = 3
                        }
                    }
                }
            };

            var card = CardFactory.CreateFromData(cardData, _localization);

            var completionEffect = card.Effects[0].PromotionCompletionEffect;
            Assert.IsNotNull(completionEffect);
            Assert.AreEqual(EffectType.GainResource, completionEffect!.Type);
            Assert.AreEqual(ResourceType.VictoryPoints, completionEffect.TargetResource);
            Assert.AreEqual(DynamicAmountSource.InnerCircleCount, completionEffect.DynamicAmountSource);
            Assert.AreEqual(3, completionEffect.DynamicAmountDivisor);
        }

        [TestMethod]
        public void CreateFromData_NoPromotionCompletionEffectAuthored_LeavesItNull()
        {
            // core_noble/Cultist of Myrkul/Zuggtmoy - every existing Promote card is unaffected.
            var cardData = new CardData
            {
                Id = "promotion_completion_effect_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData { Type = "Promote", Amount = 1 }
                }
            };

            var card = CardFactory.CreateFromData(cardData, _localization);

            Assert.IsNull(card.Effects[0].PromotionCompletionEffect);
        }

        [TestMethod]
        public void CreateFromData_PromotionCompletionEffectOnPromote_LogsNoWarning()
        {
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "promotion_completion_effect_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Type = "Promote",
                        Amount = 2,
                        PromotionCompletionEffect = new CardEffectData { Type = "GainResource", TargetResource = "VictoryPoints" }
                    }
                }
            };

            CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.DidNotReceiveWithAnyArgs().Log(default(string)!, default);
        }

        [TestMethod]
        public void CreateFromData_PromotionCompletionEffectOnANonPromoteEffect_LogsAWarning()
        {
            // PromotionCompletionEffect is only ever read by ApplyPromote (registered into
            // TurnContext, applied later by MatchManager.EndTurn) - authoring it on any other
            // effect type would parse and clone fine but silently never fire.
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "promotion_completion_effect_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Type = "Supplant",
                        Amount = 1,
                        PromotionCompletionEffect = new CardEffectData { Type = "GainResource", TargetResource = "VictoryPoints" }
                    }
                }
            };

            CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.Received(1).Log(Arg.Is<string>(s => s.Contains("only wired for EffectType.Promote")), LogChannel.Warning);
        }

        [TestMethod]
        public void CreateFromData_PromotionCompletionEffectWithItsOwnOnSuccessChain_LogsAWarning()
        {
            // MatchManager.EndTurn applies the completion effect via a direct
            // CardEffectProcessor.ApplyEffect call with no EffectContext ever built for it - a
            // chain authored on the completion effect itself would be silently dropped.
            var logger = Substitute.For<IGameLogger>();
            var cardData = new CardData
            {
                Id = "promotion_completion_effect_card",
                Aspect = "Neutral",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Type = "Promote",
                        Amount = 2,
                        PromotionCompletionEffect = new CardEffectData
                        {
                            Type = "GainResource",
                            TargetResource = "VictoryPoints",
                            OnSuccess = new CardEffectData { Type = "GainResource", TargetResource = "Influence" }
                        }
                    }
                }
            };

            CardFactory.CreateFromData(cardData, _localization, logger: logger);

            logger.Received(1).Log(Arg.Is<string>(s => s.Contains("PromotionCompletionEffect has an OnSuccess/Alternative chain")), LogChannel.Warning);
        }

    }
}

