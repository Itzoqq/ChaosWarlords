using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Services;
using System.Linq;

namespace ChaosWarlords.Source.Core.Utilities
{
    /// <summary>
    /// Static utility for reconstructing a live IGameCommand from a recorded GameCommandDto -
    /// replay playback's deserialization direction. The reverse direction (live state to DTO)
    /// lives in DtoMapper instead - the two never call each other; this class only ever consumes
    /// a DTO, never produces one.
    /// </summary>
    public static class CommandHydrator
    {
        private static readonly Dictionary<Type, Func<GameCommandDto, MatchContext, IGameCommand?>> _dtoToCommandMap;

        static CommandHydrator()
        {
            _dtoToCommandMap = new Dictionary<Type, Func<GameCommandDto, MatchContext, IGameCommand?>>
            {
                { typeof(PlayCardCommandDto), (d, c) => HydratePlayCard((PlayCardCommandDto)d, GetSeatPlayer(d, c), c.Logger) },
                { typeof(BuyCardCommandDto), (d, c) => HydrateBuyCard((BuyCardCommandDto)d, c) },
                { typeof(DeployTroopCommandDto), (d, c) => HydrateDeploy((DeployTroopCommandDto)d, c) },
                { typeof(DevourCardCommandDto), (d, c) => HydrateDevour((DevourCardCommandDto)d, GetSeatPlayer(d, c), c) },
                { typeof(EndTurnCommandDto), (d, s) => new EndTurnCommand() },
                { typeof(CancelActionCommandDto), (d, s) => new CancelActionCommand() },
                { typeof(ToggleMarketCommandDto), (d, s) => new ToggleMarketCommand() },
                { typeof(SwitchModeCommandDto), (d, s) => new SwitchToNormalModeCommand() },
                { typeof(StartAssassinateCommandDto), (d, s) => new StartAssassinateCommand() },
                { typeof(StartReturnSpyCommandDto), (d, s) => new StartReturnSpyCommand() },
                { typeof(ResolveSpyCommandDto), (d, s) =>
                    {
                        var dto = (ResolveSpyCommandDto)d;
                        return Enum.TryParse<PlayerColor>(dto.Color, out var c) ? new ResolveSpyCommand(dto.SiteId, c, dto.CardId) : null;
                    }
                },
                { typeof(AssassinateCommandDto), (d, s) => new AssassinateCommand(((AssassinateCommandDto)d).NodeId, ((AssassinateCommandDto)d).CardId, ((AssassinateCommandDto)d).DevourCardId) },
                { typeof(ReturnTroopCommandDto), (d, s) => new ReturnTroopCommand(((ReturnTroopCommandDto)d).NodeId, ((ReturnTroopCommandDto)d).CardId) },
                { typeof(SupplantCommandDto), (d, s) => new SupplantCommand(((SupplantCommandDto)d).NodeId, ((SupplantCommandDto)d).CardId, ((SupplantCommandDto)d).DevourCardId) },
                { typeof(PlaceSpyCommandDto), (d, s) => new PlaceSpyCommand(((PlaceSpyCommandDto)d).SiteId, ((PlaceSpyCommandDto)d).CardId) },
                { typeof(MoveTroopCommandDto), (d, s) => new MoveTroopCommand(((MoveTroopCommandDto)d).SrcId, ((MoveTroopCommandDto)d).DestId, ((MoveTroopCommandDto)d).CardId) },
                { typeof(ActionCompletedCommandDto), (d, s) => new ActionCompletedCommand() },
                { typeof(PromoteCommandDto), (d, s) =>
                    {
                        var dto = (PromoteCommandDto)d;
                        return new PromoteCommand(dto.CardId, dto.IsChainedEffect, dto.CardRuntimeId);
                    }
                },
                { typeof(DiscardCardCommandDto), (d, s) =>
                    {
                        var dto = (DiscardCardCommandDto)d;
                        return Enum.TryParse<PlayerColor>(dto.PlayerColor, out var color)
                            ? new DiscardCardCommand(color, dto.CardId, dto.PromoteInsteadOfDiscard)
                            : null;
                    }
                },
                { typeof(ReturnOwnSpyCommandDto), (d, s) => new ReturnOwnSpyCommand(((ReturnOwnSpyCommandDto)d).SiteId, ((ReturnOwnSpyCommandDto)d).CardId) },
                { typeof(ReturnAnySpyCommandDto), (d, s) =>
                    {
                        var dto = (ReturnAnySpyCommandDto)d;
                        return Enum.TryParse<PlayerColor>(dto.Color, out var c) ? new ReturnAnySpyCommand(dto.SiteId, c, dto.CardId) : null;
                    }
                },
                { typeof(PlayFromMarketCommandDto), (d, s) => new PlayFromMarketCommand(((PlayFromMarketCommandDto)d).MarketCardRuntimeId, ((PlayFromMarketCommandDto)d).MarketCardId!) },
                { typeof(SelectOpponentCommandDto), (d, s) =>
                    {
                        var dto = (SelectOpponentCommandDto)d;
                        return Enum.TryParse<PlayerColor>(dto.TargetPlayerColor, out var color)
                            ? new SelectOpponentCommand(color)
                            : null;
                    }
                },
                { typeof(DeclineRepeatCommandDto), (d, s) => new DeclineRepeatCommand(((DeclineRepeatCommandDto)d).CardId) },
                { typeof(DeployFromTrophyHallCommandDto), (d, s) =>
                    {
                        var dto = (DeployFromTrophyHallCommandDto)d;
                        return Enum.TryParse<PlayerColor>(dto.SourcePlayerColor, out var sourceColor) && Enum.TryParse<PlayerColor>(dto.TroopColor, out var troopColor)
                            ? new DeployFromTrophyHallCommand(dto.NodeId, sourceColor, troopColor, dto.CardId)
                            : null;
                    }
                }
            };
        }

        private static Player? GetSeatPlayer(GameCommandDto dto, MatchContext context)
        {
            return context.TurnManager?.Players.FirstOrDefault(p => p.SeatIndex == dto.Seat);
        }

        public static IGameCommand? HydrateCommand(GameCommandDto dto, MatchContext context)
        {
            if (dto == null) return null;

            if (_dtoToCommandMap.TryGetValue(dto.GetType(), out var factory))
            {
                return factory(dto, context);
            }

            return null;
        }

        private static PlayCardCommand? HydratePlayCard(PlayCardCommandDto dto, Player? player, IGameLogger? logger = null)
        {
            if (player == null) return null;

            var card = FindCardForPlayCommand(dto, player, logger);
            return card != null ? new PlayCardCommand(card) : null;
        }

        private static Card? FindCardForPlayCommand(PlayCardCommandDto dto, Player player, IGameLogger? logger)
        {
            // Try RuntimeId first - unambiguous even when the player holds two copies of the
            // same card definition. Falls back to CardId-then-index for replay data recorded
            // before CardRuntimeId existed.
            if (dto.CardRuntimeId is Guid runtimeId)
            {
                var byRuntimeId = player.Hand.FirstOrDefault(c => c.RuntimeId == runtimeId);
                if (byRuntimeId != null) return byRuntimeId;
            }

            var card = TryFindCardById(dto.CardId, player, logger);

            // Fallback to index if ID lookup failed
            if (card == null)
            {
                card = TryFindCardByIndex(dto.HandIdx, player, logger);
            }

            return card;
        }

        private static Card? TryFindCardById(string? cardId, Player player, IGameLogger? logger)
        {
            if (cardId == null) return null;

            var card = player.Hand.FirstOrDefault(c => c.Id == cardId);

            if (card == null)
            {
                logger?.Log($"[Hydrate Error] Could not find CardId '{cardId}' in Hand of {player.DisplayName}.", LogChannel.Error);
                logger?.Log($"Hand IDs: {string.Join(", ", player.Hand.Select(c => c.Id))}", LogChannel.Error);
            }

            return card;
        }

        private static Card? TryFindCardByIndex(int handIdx, Player player, IGameLogger? logger)
        {
            var card = player.Hand.ElementAtOrDefault(handIdx);

            if (card != null)
            {
                logger?.Log($"[Hydrate Warning] Fell back to Index {handIdx} -> Found {card.Name} ({card.Id})", LogChannel.Warning);
            }

            return card;
        }


        private static BuyCardCommand? HydrateBuyCard(BuyCardCommandDto dto, MatchContext context)
        {
            if (dto.CardRuntimeId is Guid runtimeId)
            {
                var byRuntimeId = context.MarketManager.MarketRow?.FirstOrDefault(c => c.RuntimeId == runtimeId);
                if (byRuntimeId != null) return new BuyCardCommand(byRuntimeId);
            }

            var card = context.MarketManager.MarketRow?.FirstOrDefault(c => c.Id == dto.CardId);
            return card != null ? new BuyCardCommand(card) : null;
        }

        private static DeployTroopCommand? HydrateDeploy(DeployTroopCommandDto dto, MatchContext context)
        {
            var node = context.MapManager.GetNodeById(dto.NodeId);
            return node != null ? new DeployTroopCommand(dto.NodeId, dto.CardId) : null;
        }

        private static DevourCardCommand? HydrateDevour(DevourCardCommandDto dto, Player? player, MatchContext? context = null)
        {
            if (player == null) return null;

            var card = FindDevourTargetCard(dto, player, context);
            var sourceCard = FindDevourSourceCard(dto, player);

            var cmd = card != null ? new DevourCardCommand(card) : null;
            if (cmd != null) cmd.SourceCard = sourceCard;
            return cmd;
        }

        private static Card? FindDevourTargetCard(DevourCardCommandDto dto, Player player, MatchContext? context)
        {
            // Try RuntimeId first, searching everywhere a devour target can live - unambiguous
            // even with duplicate-copy cards. Falls back to the Location-directed CardId/index
            // lookup below for replay data recorded before CardRuntimeId existed.
            if (dto.CardRuntimeId is Guid runtimeId)
            {
                var byRuntimeId = FindByRuntimeId(runtimeId, player.Hand, player.InnerCircle, player.PlayedCards, context?.MarketManager.MarketRow);
                if (byRuntimeId != null) return byRuntimeId;
            }

            return FindByLocationOrHand(dto, player, context);
        }

        /// <summary>
        /// Location-directed CardId/index lookup - the replay-compatible fallback for data
        /// recorded before CardRuntimeId existed (see FindDevourTargetCard). Case-insensitive
        /// on Location to match the original string.Equals(..., OrdinalIgnoreCase) checks this
        /// replaced (both "Market"/"market"/"MARKET" etc. must still match).
        /// </summary>
        private static Card? FindByLocationOrHand(DevourCardCommandDto dto, Player player, MatchContext? context)
        {
            return dto.Location?.ToUpperInvariant() switch
            {
                "MARKET" when context != null => context.MarketManager.MarketRow?.FirstOrDefault(c => c.Id == dto.CardId),
                "INNERCIRCLE" => player.InnerCircle.FirstOrDefault(c => c.Id == dto.CardId),
                _ => FindCardInHand(dto, player),
            };
        }

        /// <summary>
        /// Searches each given zone, in order, for a card with the given RuntimeId - the first
        /// match wins. Used to resolve a devour target unambiguously across every zone it could
        /// live in (Hand/InnerCircle/PlayedCards/Market), even with duplicate-copy cards where
        /// matching by shared definition Id alone would be ambiguous.
        /// </summary>
        private static Card? FindByRuntimeId(Guid runtimeId, params IEnumerable<Card>?[] zones)
        {
            foreach (var zone in zones)
            {
                var match = zone?.FirstOrDefault(c => c.RuntimeId == runtimeId);
                if (match != null) return match;
            }
            return null;
        }

        private static Card? FindCardInHand(DevourCardCommandDto dto, Player player)
        {
            // Prefer CardId for robustness
            if (dto.CardId != null)
            {
                var card = player.Hand.FirstOrDefault(c => c.Id == dto.CardId);
                if (card != null) return card;
            }

            return player.Hand.ElementAtOrDefault(dto.HandIdx);
        }

        private static Card? FindDevourSourceCard(DevourCardCommandDto dto, Player player)
        {
            if (dto.SourceCardRuntimeId is Guid sourceRuntimeId)
            {
                var byRuntimeId = player.Hand.FirstOrDefault(c => c.RuntimeId == sourceRuntimeId);
                if (byRuntimeId != null) return byRuntimeId;
            }

            if (dto.SourceCardId == null) return null;
            return player.Hand.FirstOrDefault(c => c.Id == dto.SourceCardId);
        }
    }
}
