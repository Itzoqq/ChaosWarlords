using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Data.Enums;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using System;

namespace ChaosWarlords.Tests.Source.Mechanics.Commands
{
    /// <summary>
    /// Serialization round-trip tests for the 3 commands added this session
    /// (DiscardCardCommand/ReturnOwnSpyCommand/PlayFromMarketCommand) - all 3 DTOs sat at 0%
    /// coverage per the 2026-09-01 coverage run (see planning.txt TIER 1 item 3): no test
    /// exercised either ToDto() or DtoMapper.HydrateCommand for any of them, unlike every
    /// other command (see CommandSerializationTests.cs's existing per-command pattern, which
    /// this mirrors).
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class NewCommandDtoRoundTripTests
    {
        private static MatchContext CreateMinimalContext()
        {
            var logger = ChaosWarlords.Tests.Utilities.TestLogger.Instance;
            var turnManager = Substitute.For<ITurnManager>();
            var mapManager = Substitute.For<IMapManager>();
            var marketManager = Substitute.For<IMarketManager>();
            var actionSystem = Substitute.For<IActionSystem>();
            var cardDb = Substitute.For<ICardDatabase>();
            var playerState = new PlayerStateManager(logger);
            return new MatchContext(turnManager, mapManager, marketManager, actionSystem, cardDb, playerState, logger);
        }

        // --- DiscardCardCommand / DiscardCardCommandDto ---

        [TestMethod]
        public void DiscardCardCommand_ToDto_CarriesTargetPlayerColorAndCardId()
        {
            var command = new DiscardCardCommand(PlayerColor.Blue, "wight");

            var dto = (DiscardCardCommandDto)command.ToDto();

            Assert.AreEqual("Blue", dto.PlayerColor);
            Assert.AreEqual("wight", dto.CardId);
            Assert.AreEqual(CommandType.DiscardCard, command.Type);
        }

        [TestMethod]
        public void DiscardCardCommandDto_HydrateCommand_RoundTripsToAnEquivalentCommand()
        {
            var original = new DiscardCardCommand(PlayerColor.Blue, "wight");
            var dto = original.ToDto();
            var context = CreateMinimalContext();

            var hydrated = DtoMapper.HydrateCommand(dto, context) as DiscardCardCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(original.TargetPlayerColor, hydrated!.TargetPlayerColor);
            Assert.AreEqual(original.CardId, hydrated.CardId);
        }

        [TestMethod]
        public void DiscardCardCommandDto_HydrateCommand_WithUnparsablePlayerColor_ReturnsNull()
        {
            // Defensive coverage for the Enum.TryParse guard in DtoMapper's rehydration -
            // a corrupted/forward-incompatible replay/network payload shouldn't throw or
            // silently default to some player.
            var dto = new DiscardCardCommandDto { PlayerColor = "NotARealColor", CardId = "wight" };
            var context = CreateMinimalContext();

            var hydrated = DtoMapper.HydrateCommand(dto, context);

            Assert.IsNull(hydrated);
        }

        // --- ReturnOwnSpyCommand / ReturnOwnSpyCommandDto ---

        [TestMethod]
        public void ReturnOwnSpyCommand_ToDto_CarriesSiteIdAndCardId()
        {
            var command = new ReturnOwnSpyCommand(42, "cloaker");

            var dto = (ReturnOwnSpyCommandDto)command.ToDto();

            Assert.AreEqual(42, dto.SiteId);
            Assert.AreEqual("cloaker", dto.CardId);
            Assert.AreEqual(CommandType.ReturnOwnSpy, command.Type);
        }

        [TestMethod]
        public void ReturnOwnSpyCommandDto_HydrateCommand_RoundTripsToAnEquivalentCommand()
        {
            var original = new ReturnOwnSpyCommand(42, "cloaker");
            var dto = original.ToDto();
            var context = CreateMinimalContext();

            var hydrated = DtoMapper.HydrateCommand(dto, context) as ReturnOwnSpyCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(original.TargetSiteId, hydrated!.TargetSiteId);
            Assert.AreEqual(original.CardId, hydrated.CardId);
        }

        // --- PlayFromMarketCommand / PlayFromMarketCommandDto ---

        [TestMethod]
        public void PlayFromMarketCommand_ToDto_CarriesMarketCardRuntimeIdAndId()
        {
            var marketCard = new Card("core_house_guard", "House Guard", 3, CardAspect.Warlord, 1, 2, 0);
            var sourceCard = new Card("ulitharid", "Ulitharid", 6, CardAspect.Oblivion, 3, 6, 0);
            var command = new PlayFromMarketCommand(marketCard, sourceCard);

            var dto = (PlayFromMarketCommandDto)command.ToDto();

            Assert.AreEqual(marketCard.RuntimeId, dto.MarketCardRuntimeId);
            Assert.AreEqual("core_house_guard", dto.MarketCardId);
            Assert.AreEqual(CommandType.PlayFromMarket, command.Type);
        }

        [TestMethod]
        public void PlayFromMarketCommandDto_HydrateCommand_RoundTripsToAnEquivalentCommand()
        {
            var marketCard = new Card("core_house_guard", "House Guard", 3, CardAspect.Warlord, 1, 2, 0);
            var sourceCard = new Card("ulitharid", "Ulitharid", 6, CardAspect.Oblivion, 3, 6, 0);
            var original = new PlayFromMarketCommand(marketCard, sourceCard);
            var dto = original.ToDto();
            var context = CreateMinimalContext();

            var hydrated = DtoMapper.HydrateCommand(dto, context) as PlayFromMarketCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(original.MarketCardRuntimeId, hydrated!.MarketCardRuntimeId);
            Assert.AreEqual(original.MarketCardId, hydrated.MarketCardId);
        }

        // --- SelectOpponentCommand / SelectOpponentCommandDto ---
        // (Cranium Rats / planning.txt TIER 2 #6 - the "target a player" primitive.)

        [TestMethod]
        public void SelectOpponentCommand_ToDto_CarriesTargetPlayerColor()
        {
            var command = new SelectOpponentCommand(PlayerColor.Blue);

            var dto = (SelectOpponentCommandDto)command.ToDto();

            Assert.AreEqual("Blue", dto.TargetPlayerColor);
            Assert.AreEqual(CommandType.SelectOpponent, command.Type);
        }

        [TestMethod]
        public void SelectOpponentCommandDto_HydrateCommand_RoundTripsToAnEquivalentCommand()
        {
            var original = new SelectOpponentCommand(PlayerColor.Blue);
            var dto = original.ToDto();
            var context = CreateMinimalContext();

            var hydrated = DtoMapper.HydrateCommand(dto, context) as SelectOpponentCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(original.TargetPlayerColor, hydrated!.TargetPlayerColor);
        }

        [TestMethod]
        public void SelectOpponentCommandDto_HydrateCommand_WithUnparsablePlayerColor_ReturnsNull()
        {
            // Same defensive Enum.TryParse guard as DiscardCardCommandDto's rehydration - a
            // corrupted/forward-incompatible replay/network payload shouldn't throw or
            // silently default to some player.
            var dto = new SelectOpponentCommandDto { TargetPlayerColor = "NotARealColor" };
            var context = CreateMinimalContext();

            var hydrated = DtoMapper.HydrateCommand(dto, context);

            Assert.IsNull(hydrated);
        }
    }
}
