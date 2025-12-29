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

        public static CommandDto? ToDto(IGameCommand? command, int sequenceNumber, Player? actor)
        {
            if (command == null) return null;

            var dto = new CommandDto
            {
                CommandType = command.GetType().Name, 
                SequenceNumber = sequenceNumber,
                PlayerId = actor?.PlayerId ?? Guid.Empty,
            };

            switch (command)
            {
                case PlayCardCommand playCmd:
                    dto.CardDefinitionId = playCmd.Card.Id;
                    dto.CardHandIndex = FindCardIndex(actor?.Hand, playCmd.Card);
                    break;

                case BuyCardCommand buyCmd:
                    dto.CardDefinitionId = buyCmd.Card.Id;
                    break;
                
                case DevourCardCommand devourCmd:
                    dto.CardDefinitionId = devourCmd.CardToDevour.Id;
                    dto.CardHandIndex = FindCardIndex(actor?.Hand, devourCmd.CardToDevour);
                    break;

                case DeployTroopCommand deployCmd:
                    dto.TargetNodeId = deployCmd.Node.Id;
                    break;
                
                case ResolveSpyCommand spyCmd:
                   dto.Context = spyCmd.SpyColor.ToString();
                   break;

                default:
                    // Signal commands have no payload
                    break;
            }

            return dto;
        }

        private static int? FindCardIndex(List<Card>? list, Card card)
        {
            if (list == null || card == null) return null;
            int idx = list.IndexOf(card);
            return idx >= 0 ? idx : null;
        }

        // --- Hydration (DTO -> Command) ---

        /// <summary>
        /// Reconstructs a GameCommand from a DTO.
        /// Requires access to game state to resolve references (IDs to Objects).
        /// </summary>
        public static IGameCommand? HydrateCommand(CommandDto dto, IGameplayState state)
        {
            if (dto == null) return null;

            // 1. Resolve Actor
            // We use TurnManager to find the player by ID.
            var player = state.TurnManager?.Players.FirstOrDefault(p => p.PlayerId == dto.PlayerId);
            
            // If player not found (and required), we might fail.
            // Some commands (like EndTurn) might imply ActivePlayer if PlayerId is missing/empty, 
            // but strict replay should use ID.

            switch (dto.CommandType)
            {
                case nameof(PlayCardCommand):
                    if (player != null && dto.CardHandIndex.HasValue)
                    {
                        var card = player.Hand.ElementAtOrDefault(dto.CardHandIndex.Value);
                        if (card != null) return new PlayCardCommand(card);
                    }
                    break;

                case nameof(BuyCardCommand):
                     // Requires Market lookup or Definition lookup if buying by ID
                     // Market logic typically uses DefinitionID for "Buy from Market"
                     // We need a way to find a card by DefinitionID in the Market or Factory.
                     // IMPORTANT: BuyCard takes a SPECIFIC Card instance from the Market row.
                     if (dto.CardDefinitionId != null)
                     {
                         // Simplified: Find first matching card in Market
                         var matchingCard = state.MarketManager.MarketRow.FirstOrDefault(c => c.Id == dto.CardDefinitionId);
                         if (matchingCard != null) return new BuyCardCommand(matchingCard);
                     }
                     break;

                case nameof(DeployTroopCommand):
                    if (dto.TargetNodeId.HasValue)
                    {
                        var node = state.MapManager.Nodes.FirstOrDefault(n => n.Id == dto.TargetNodeId.Value);
                        if (node != null) return new DeployTroopCommand(node);
                    }
                    break;
                
                case nameof(DevourCardCommand):
                    if (player != null && dto.CardHandIndex.HasValue)
                    {
                        var card = player.Hand.ElementAtOrDefault(dto.CardHandIndex.Value);
                        if (card != null) return new DevourCardCommand(card);
                    }
                    else if (player != null && !string.IsNullOrEmpty(dto.CardDefinitionId))
                    {
                        // Fallback: search by ID if index invalid (less deterministic but robust)
                        var card = player.Hand.FirstOrDefault(c => c.Id == dto.CardDefinitionId);
                        if (card != null) return new DevourCardCommand(card);
                    }
                    break;

                case nameof(EndTurnCommand):
                    return new EndTurnCommand();

                case nameof(CancelActionCommand):
                    return new CancelActionCommand();

                case nameof(ToggleMarketCommand):
                    return new ToggleMarketCommand();

                 case nameof(SwitchToNormalModeCommand):
                    return new SwitchToNormalModeCommand();
                
                 case nameof(StartAssassinateCommand):
                    return new StartAssassinateCommand();

                 case nameof(StartReturnSpyCommand):
                    return new StartReturnSpyCommand();
                    
                 case nameof(ResolveSpyCommand):
                    if (Enum.TryParse<PlayerColor>(dto.Context, out var color))
                    {
                        return new ResolveSpyCommand(color);
                    }
                    break;

                 case nameof(ActionCompletedCommand):
                    return new ActionCompletedCommand();
            }

            return null; // Could not hydrate
        }
    }
}
