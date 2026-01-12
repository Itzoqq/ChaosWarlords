using System;
using System.Collections.Generic;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Services;
using System.Linq;

namespace ChaosWarlords.Source.Core.Utilities
{
    /// <summary>
    /// Static utility for converting Game Entities to Data Transfer Objects.
    /// Used for Saving, Replay Recording, and Networking.
    /// </summary>
    public static class DtoMapper
    {
        private static readonly Dictionary<Type, Func<GameCommandDto, IGameplayState, IGameCommand?>> _dtoToCommandMap;

        static DtoMapper()
        {
            _dtoToCommandMap = new Dictionary<Type, Func<GameCommandDto, IGameplayState, IGameCommand?>>
            {
                { typeof(PlayCardCommandDto), (d, s) => HydratePlayCard((PlayCardCommandDto)d, GetSeatPlayer(d, s), s.Logger) },
                { typeof(BuyCardCommandDto), (d, s) => HydrateBuyCard((BuyCardCommandDto)d, s) },
                { typeof(DeployTroopCommandDto), (d, s) => HydrateDeploy((DeployTroopCommandDto)d, s, GetSeatPlayer(d, s)) },
                { typeof(DevourCardCommandDto), (d, s) => HydrateDevour((DevourCardCommandDto)d, GetSeatPlayer(d, s), s) },
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
                { typeof(PromoteCommandDto), (d, s) => new PromoteCommand(((PromoteCommandDto)d).CardId) }
            };
        }

        private static Player? GetSeatPlayer(GameCommandDto dto, IGameplayState state)
        {
             return state.MatchContext.TurnManager?.Players.FirstOrDefault(p => p.SeatIndex == dto.Seat);
        }

        // --- Card Mapping ---

        public static CardDto? ToDto(Card? card, int collectionIndex = -1)
        {
            if (card == null) return null;
            return new CardDto(card, collectionIndex);
        }

        public static List<CardDto> ToDtoList(IEnumerable<Card> cards)
        {
            var list = new List<CardDto>();
            if (cards == null) return list;
            int index = 0;
            foreach (var card in cards)
            {
                list.Add(new CardDto(card, index++));
            }
            return list;
        }

        // --- Player Mapping ---

        public static PlayerDto? ToDto(Player? player)
        {
            if (player == null) return null;
            return PlayerDto.FromEntity(player);
        }

        // --- Map Mapping ---

        public static MapNodeDto? ToDto(MapNode? node)
        {
            if (node == null) return null;
            return new MapNodeDto(node);
        }

        public static MapDto ToDto(IMapManager mapManager)
        {
            var dto = new MapDto();
            if (mapManager?.Nodes != null)
            {
               dto.Nodes = mapManager.Nodes.Select(n => ToDto(n)).Where(n => n != null).ToList()!;
            }
            if (mapManager?.Sites != null)
            {
                dto.Sites = mapManager.Sites.Select(s => new SiteDto(s)).ToList();
            }
            return dto;
        }

        // --- Command Mapping ---

        public static GameCommandDto? ToDto(IGameCommand? command, int sequenceNumber, Player? actor)
        {
            if (command == null) return null;
            
            var dto = command.ToDto();
            dto.Seq = sequenceNumber;
            dto.Seat = actor?.SeatIndex ?? -1;

            // Enrichment for Hand Index (Legacy support until commands carry index)
            if (actor != null)
            {
                if (dto is PlayCardCommandDto playDto && command is PlayCardCommand playCmd && playDto.HandIdx == -1)
                {
                    playDto.HandIdx = actor.Hand.IndexOf(playCmd.Card);
                }
                else if (dto is DevourCardCommandDto devourDto && command is DevourCardCommand devourCmd && devourDto.HandIdx == -1)
                {
                    devourDto.HandIdx = actor.Hand.IndexOf(devourCmd.CardToDevour);
                }
            }

            return dto;
        }

        // --- Hydration (DTO -> Command) ---

        public static IGameCommand? HydrateCommand(GameCommandDto dto, IGameplayState state)
        {
            if (dto == null) return null;
            
            if (_dtoToCommandMap.TryGetValue(dto.GetType(), out var factory))
            {
                return factory(dto, state);
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
            // Try to find by CardId first
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


        private static BuyCardCommand? HydrateBuyCard(BuyCardCommandDto dto, IGameplayState state)
        {
            var card = state.MatchContext.MarketManager.MarketRow.FirstOrDefault(c => c.Id == dto.CardId);
            return card != null ? new BuyCardCommand(card) : null;
        }

        private static DeployTroopCommand? HydrateDeploy(DeployTroopCommandDto dto, IGameplayState state, Player? player)
        {
            var node = state.MatchContext.MapManager.Nodes.FirstOrDefault(n => n.Id == dto.NodeId);
            if (node != null && player != null)
                return new DeployTroopCommand(node, player);
            return null;
        }

        private static DevourCardCommand? HydrateDevour(DevourCardCommandDto dto, Player? player, IGameplayState? state = null)
        {
            if (player == null) return null;

            var card = FindDevourTargetCard(dto, player, state);
            var sourceCard = FindDevourSourceCard(dto, player);

            var cmd = card != null ? new DevourCardCommand(card) : null;
            if (cmd != null) cmd.SourceCard = sourceCard;
            return cmd;
        }

        private static Card? FindDevourTargetCard(DevourCardCommandDto dto, Player player, IGameplayState? state)
        {
            if (string.Equals(dto.Location, "Market", StringComparison.OrdinalIgnoreCase) && state != null)
            {
                return state.MatchContext.MarketManager.MarketRow.FirstOrDefault(c => c.Id == dto.CardId);
            }
            
            if (string.Equals(dto.Location, "InnerCircle", StringComparison.OrdinalIgnoreCase))
            {
                return player.InnerCircle.FirstOrDefault(c => c.Id == dto.CardId);
            }

            return FindCardInHand(dto, player);
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
            if (dto.SourceCardId == null) return null;
            return player.Hand.FirstOrDefault(c => c.Id == dto.SourceCardId);
        }

        // --- Victory Mapping ---

        public static VictoryDto ToVictoryDto(ChaosWarlords.Source.Contexts.MatchContext context, IVictoryManager victoryManager)
        {
            var dto = new VictoryDto();
            
            // Check current status
            dto.IsGameOver = victoryManager.CheckEndGameConditions(context, out var reason);
            dto.VictoryReason = reason;

            // Calculate scores regardless of game over (for scoreboard)
            foreach (var player in context.TurnManager.Players)
            {
                var breakdown = victoryManager.GetScoreBreakdown(player, context);
                dto.FinalScores[player.SeatIndex] = breakdown.TotalScore;
                dto.ScoreBreakdowns[player.SeatIndex] = breakdown;
                dto.PlayerColors[player.SeatIndex] = player.Color.ToString(); // Assuming PlayerColor is an enum or has valid ToString
            }

            if (dto.IsGameOver)
            {
                var winner = victoryManager.DetermineWinner(context.TurnManager.Players, context);
                dto.WinnerSeat = winner.SeatIndex;
                dto.WinnerName = winner.DisplayName;
            }

            return dto;
        }
    }
}
