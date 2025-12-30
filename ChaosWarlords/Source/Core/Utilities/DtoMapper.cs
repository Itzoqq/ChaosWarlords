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
using System.Linq;

namespace ChaosWarlords.Source.Core.Utilities
{
    /// <summary>
    /// Static utility for converting Game Entities to Data Transfer Objects.
    /// Used for Saving, Replay Recording, and Networking.
    /// </summary>
    public static class DtoMapper
    {
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

            int seat = actor?.SeatIndex ?? -1;

            GameCommandDto dto = command switch
            {
                PlayCardCommand playCmd => new PlayCardCommandDto
                {
                    CardId = playCmd.Card.Id,
                    HandIdx = actor?.Hand.IndexOf(playCmd.Card) ?? -1
                },
                BuyCardCommand buyCmd => new BuyCardCommandDto
                {
                    CardId = buyCmd.Card.Id
                },
                DevourCardCommand devourCmd => new DevourCardCommandDto
                {
                    CardId = devourCmd.CardToDevour.Id,
                    HandIdx = actor?.Hand.IndexOf(devourCmd.CardToDevour) ?? -1
                },
                DeployTroopCommand deployCmd => new DeployTroopCommandDto
                {
                    NodeId = deployCmd.Node.Id
                },
                EndTurnCommand => new EndTurnCommandDto(),
                CancelActionCommand => new CancelActionCommandDto(),
                ToggleMarketCommand => new ToggleMarketCommandDto(),
                SwitchToNormalModeCommand => new SwitchModeCommandDto(),
                StartAssassinateCommand => new StartAssassinateCommandDto(),
                StartReturnSpyCommand => new StartReturnSpyCommandDto(),
                ResolveSpyCommand spyCmd => new ResolveSpyCommandDto
                {
                    SiteId = spyCmd.SiteId,
                    Color = spyCmd.SpyColor.ToString(),
                    CardId = spyCmd.CardId
                },
                AssassinateCommand ashCmd => new AssassinateCommandDto
                {
                    NodeId = ashCmd.TargetNodeId,
                    CardId = ashCmd.CardId
                },
                ReturnTroopCommand retCmd => new ReturnTroopCommandDto
                {
                    NodeId = retCmd.TargetNodeId,
                    CardId = retCmd.CardId
                },
                SupplantCommand supCmd => new SupplantCommandDto
                {
                    NodeId = supCmd.TargetNodeId,
                    CardId = supCmd.CardId
                },
                PlaceSpyCommand spyCmd => new PlaceSpyCommandDto
                {
                    SiteId = spyCmd.TargetSiteId,
                    CardId = spyCmd.CardId
                },
                MoveTroopCommand moveCmd => new MoveTroopCommandDto
                {
                    SrcId = moveCmd.SourceNodeId,
                    DestId = moveCmd.DestinationNodeId,
                    CardId = moveCmd.CardId
                },
                ActionCompletedCommand => new ActionCompletedCommandDto(),
                _ => throw new NotSupportedException($"Command type {command.GetType().Name} not supported in DTO mapping.")
            };

            dto.Seq = sequenceNumber;
            dto.Seat = seat;
            return dto;
        }

        // --- Hydration (DTO -> Command) ---

        public static IGameCommand? HydrateCommand(GameCommandDto dto, IGameplayState state)
        {
            if (dto == null) return null;

            var player = state.TurnManager?.Players.FirstOrDefault(p => p.SeatIndex == dto.Seat);
            
            return dto switch
            {
                PlayCardCommandDto playDto => HydratePlayCard(playDto, player),
                BuyCardCommandDto buyDto => HydrateBuyCard(buyDto, state),
                DeployTroopCommandDto deployDto => HydrateDeploy(deployDto, state, player),
                DevourCardCommandDto devourDto => HydrateDevour(devourDto, player),
                EndTurnCommandDto => new EndTurnCommand(),
                CancelActionCommandDto => new CancelActionCommand(),
                ToggleMarketCommandDto => new ToggleMarketCommand(),
                SwitchModeCommandDto => new SwitchToNormalModeCommand(),
                StartAssassinateCommandDto => new StartAssassinateCommand(),
                StartReturnSpyCommandDto => new StartReturnSpyCommand(),
                ResolveSpyCommandDto spyDto => Enum.TryParse<PlayerColor>(spyDto.Color, out var c) ? new ResolveSpyCommand(spyDto.SiteId, c, spyDto.CardId) : null,
                AssassinateCommandDto ashDto => new AssassinateCommand(ashDto.NodeId, ashDto.CardId),
                ReturnTroopCommandDto retDto => new ReturnTroopCommand(retDto.NodeId, retDto.CardId),
                SupplantCommandDto supDto => new SupplantCommand(supDto.NodeId, supDto.CardId),
                PlaceSpyCommandDto spyDto => new PlaceSpyCommand(spyDto.SiteId, spyDto.CardId),
                MoveTroopCommandDto moveDto => new MoveTroopCommand(moveDto.SrcId, moveDto.DestId, moveDto.CardId),
                ActionCompletedCommandDto => new ActionCompletedCommand(),
                _ => null
            };
        }

        private static PlayCardCommand? HydratePlayCard(PlayCardCommandDto dto, Player? player)
        {
            if (player == null) return null;
            
            // Prefer CardId for robustness against hand order changes
            Card? card = null;
            if (dto.CardId != null)
                card = player.Hand.FirstOrDefault(c => c.Id == dto.CardId);
                
            // Fallback to index if ID not found or not provided
            if (card == null)
                card = player.Hand.ElementAtOrDefault(dto.HandIdx);
            
            return card != null ? new PlayCardCommand(card) : null;
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
    }
}
