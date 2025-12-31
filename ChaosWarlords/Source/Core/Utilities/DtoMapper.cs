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
        private static readonly Dictionary<Type, Func<IGameCommand, int, Player?, GameCommandDto>> _commandToDtoMap;
        private static readonly Dictionary<Type, Func<GameCommandDto, IGameplayState, IGameCommand?>> _dtoToCommandMap;

        static DtoMapper()
        {
            _commandToDtoMap = new Dictionary<Type, Func<IGameCommand, int, Player?, GameCommandDto>>
            {
                { typeof(PlayCardCommand), (cmd, seq, p) => 
                    {
                        var c = (PlayCardCommand)cmd;
                        return new PlayCardCommandDto 
                        { 
                            CardId = c.Card.Id, 
                            HandIdx = p?.Hand.IndexOf(c.Card) ?? -1 
                        };
                    } 
                },
                { typeof(BuyCardCommand), (cmd, seq, p) => new BuyCardCommandDto { CardId = ((BuyCardCommand)cmd).Card.Id } },
                { typeof(DevourCardCommand), (cmd, seq, p) => 
                    {
                        var c = (DevourCardCommand)cmd;
                        return new DevourCardCommandDto 
                        { 
                            CardId = c.CardToDevour.Id, 
                            HandIdx = p?.Hand.IndexOf(c.CardToDevour) ?? -1 
                        };
                    } 
                },
                { typeof(DeployTroopCommand), (cmd, seq, p) => new DeployTroopCommandDto { NodeId = ((DeployTroopCommand)cmd).Node.Id } },
                { typeof(EndTurnCommand), (cmd, seq, p) => new EndTurnCommandDto() },
                { typeof(CancelActionCommand), (cmd, seq, p) => new CancelActionCommandDto() },
                { typeof(ToggleMarketCommand), (cmd, seq, p) => new ToggleMarketCommandDto() },
                { typeof(SwitchToNormalModeCommand), (cmd, seq, p) => new SwitchModeCommandDto() },
                { typeof(StartAssassinateCommand), (cmd, seq, p) => new StartAssassinateCommandDto() },
                { typeof(StartReturnSpyCommand), (cmd, seq, p) => new StartReturnSpyCommandDto() },
                { typeof(ResolveSpyCommand), (cmd, seq, p) => 
                    {
                        var c = (ResolveSpyCommand)cmd;
                        return new ResolveSpyCommandDto { SiteId = c.SiteId, Color = c.SpyColor.ToString(), CardId = c.CardId }; 
                    } 
                },
                { typeof(AssassinateCommand), (cmd, seq, p) => 
                    {
                        var c = (AssassinateCommand)cmd;
                        return new AssassinateCommandDto { NodeId = c.TargetNodeId, CardId = c.CardId };
                    } 
                },
                { typeof(ReturnTroopCommand), (cmd, seq, p) => 
                    {
                        var c = (ReturnTroopCommand)cmd;
                        return new ReturnTroopCommandDto { NodeId = c.TargetNodeId, CardId = c.CardId };
                    } 
                },
                { typeof(SupplantCommand), (cmd, seq, p) => 
                    {
                        var c = (SupplantCommand)cmd;
                        return new SupplantCommandDto { NodeId = c.TargetNodeId, CardId = c.CardId };
                    } 
                },
                { typeof(PlaceSpyCommand), (cmd, seq, p) => 
                    {
                        var c = (PlaceSpyCommand)cmd;
                        return new PlaceSpyCommandDto { SiteId = c.TargetSiteId, CardId = c.CardId }; 
                    } 
                },
                { typeof(MoveTroopCommand), (cmd, seq, p) => 
                    {
                        var c = (MoveTroopCommand)cmd;
                        return new MoveTroopCommandDto { SrcId = c.SourceNodeId, DestId = c.DestinationNodeId, CardId = c.CardId }; 
                    } 
                },
                { typeof(ActionCompletedCommand), (cmd, seq, p) => new ActionCompletedCommandDto() }
            };

            _dtoToCommandMap = new Dictionary<Type, Func<GameCommandDto, IGameplayState, IGameCommand?>>
            {
                { typeof(PlayCardCommandDto), (d, s) => HydratePlayCard((PlayCardCommandDto)d, GetSeatPlayer(d, s), s.Logger) },
                { typeof(BuyCardCommandDto), (d, s) => HydrateBuyCard((BuyCardCommandDto)d, s) },
                { typeof(DeployTroopCommandDto), (d, s) => HydrateDeploy((DeployTroopCommandDto)d, s, GetSeatPlayer(d, s)) },
                { typeof(DevourCardCommandDto), (d, s) => HydrateDevour((DevourCardCommandDto)d, GetSeatPlayer(d, s)) },
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
                { typeof(AssassinateCommandDto), (d, s) => new AssassinateCommand(((AssassinateCommandDto)d).NodeId, ((AssassinateCommandDto)d).CardId) },
                { typeof(ReturnTroopCommandDto), (d, s) => new ReturnTroopCommand(((ReturnTroopCommandDto)d).NodeId, ((ReturnTroopCommandDto)d).CardId) },
                { typeof(SupplantCommandDto), (d, s) => new SupplantCommand(((SupplantCommandDto)d).NodeId, ((SupplantCommandDto)d).CardId) },
                { typeof(PlaceSpyCommandDto), (d, s) => new PlaceSpyCommand(((PlaceSpyCommandDto)d).SiteId, ((PlaceSpyCommandDto)d).CardId) },
                { typeof(MoveTroopCommandDto), (d, s) => new MoveTroopCommand(((MoveTroopCommandDto)d).SrcId, ((MoveTroopCommandDto)d).DestId, ((MoveTroopCommandDto)d).CardId) },
                { typeof(ActionCompletedCommandDto), (d, s) => new ActionCompletedCommand() }
            };
        }

        private static Player? GetSeatPlayer(GameCommandDto dto, IGameplayState state)
        {
             return state.TurnManager?.Players.FirstOrDefault(p => p.SeatIndex == dto.Seat);
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

        // --- Command Mapping ---

        public static GameCommandDto? ToDto(IGameCommand? command, int sequenceNumber, Player? actor)
        {
            if (command == null) return null;
            
            if (_commandToDtoMap.TryGetValue(command.GetType(), out var factory))
            {
                var dto = factory(command, sequenceNumber, actor);
                dto.Seq = sequenceNumber;
                dto.Seat = actor?.SeatIndex ?? -1;
                return dto;
            }

            throw new NotSupportedException($"Command type {command.GetType().Name} not supported in DTO mapping.");
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
            var card = state.MarketManager.MarketRow.FirstOrDefault(c => c.Id == dto.CardId);
            return card != null ? new BuyCardCommand(card) : null;
        }

        private static DeployTroopCommand? HydrateDeploy(DeployTroopCommandDto dto, IGameplayState state, Player? player)
        {
            var node = state.MapManager.Nodes.FirstOrDefault(n => n.Id == dto.NodeId);
            if (node != null && player != null)
                return new DeployTroopCommand(node, player);
            return null;
        }

        private static DevourCardCommand? HydrateDevour(DevourCardCommandDto dto, Player? player)
        {
            if (player == null) return null;
            
            // Prefer CardId for robustness
            Card? card = null;
            if (dto.CardId != null)
                card = player.Hand.FirstOrDefault(c => c.Id == dto.CardId);

            if (card == null)
                card = player.Hand.ElementAtOrDefault(dto.HandIdx);
            
            return card != null ? new DevourCardCommand(card) : null;
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
                int score = victoryManager.CalculateFinalScore(player, context);
                dto.FinalScores[player.SeatIndex] = score;
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
