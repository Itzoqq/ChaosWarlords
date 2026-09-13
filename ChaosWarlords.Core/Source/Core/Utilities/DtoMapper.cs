using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Services;
using System.Linq; // Required for serialization

namespace ChaosWarlords.Source.Core.Utilities
{
    /// <summary>
    /// Static utility for converting live game Entities/state to Data Transfer Objects - Saving,
    /// Replay Recording, and Networking's serialization direction. The reverse direction (DTO
    /// back to a live IGameCommand, for replay playback) lives in CommandHydrator instead - the
    /// two never call each other; this class only ever produces a DTO, never consumes one.
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

        public static MapDto ToDto(IMapManager mapManager)
        {
            var dto = new MapDto();
            dto.Nodes = ConvertNodesToDto(mapManager);
            dto.Sites = ConvertSitesToDto(mapManager);
            return dto;
        }

        private static List<MapNodeDto> ConvertNodesToDto(IMapManager? mapManager)
        {
            var list = new List<MapNodeDto>();
            if (mapManager?.Nodes != null)
            {
                foreach (var node in mapManager.Nodes)
                {
                    var nodeDto = ToDto(node);
                    if (nodeDto != null)
                    {
                        list.Add(nodeDto);
                    }
                }
            }
            return list;
        }

        private static List<SiteDto> ConvertSitesToDto(IMapManager? mapManager)
        {
            var list = new List<SiteDto>();
            if (mapManager?.Sites != null)
            {
                foreach (var site in mapManager.Sites)
                {
                    list.Add(new SiteDto(site));
                }
            }
            return list;
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
                EnrichCommandDtoWithHandIndex(dto, command, actor);
            }

            return dto;
        }

        private static void EnrichCommandDtoWithHandIndex(GameCommandDto dto, IGameCommand command, Player actor)
        {
            if (dto is PlayCardCommandDto playDto && command is PlayCardCommand playCmd && playDto.HandIdx == -1)
            {
                playDto.HandIdx = GetCardIndex(actor.Hand, playCmd.CardRuntimeId);
            }
            else if (dto is DevourCardCommandDto devourDto && command is DevourCardCommand devourCmd && devourDto.HandIdx == -1)
            {
                devourDto.HandIdx = GetCardIndex(actor.Hand, devourCmd.CardRuntimeId);
            }
        }

        private static int GetCardIndex(IReadOnlyList<Card> list, Guid runtimeId)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].RuntimeId == runtimeId) return i;
            }
            return -1;
        }

        // --- Victory Mapping ---

        public static VictoryDto ToVictoryDto(Source.Contexts.MatchContext context, IVictoryManager victoryManager)
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
        public static GameStateDto ToGameStateDto(Source.Contexts.MatchContext context)
        {
            var dto = new GameStateDto();
            dto.Seed = context.Seed;
            dto.TurnNumber = context.CurrentTurnNumber;
            dto.Phase = context.CurrentPhase;
            dto.SequenceNumber = context.SequenceNumber;

            // Transient - definitional ids (Card.DefinitionId, NOT the CardFactory.
            // GenerateUniqueId-suffixed Card.Id ICardDatabase.GetCardById can't resolve).
            dto.MarkedForTurnEndDevourCardIds = context.CardsMarkedForTurnEndDevour.Select(c => c.DefinitionId).ToList();
            dto.MarkedForTurnEndPromoteCardIds = context.CardsMarkedForTurnEndPromote.Select(c => c.DefinitionId).ToList();
            dto.PendingOpponentDiscardTriggerCardIds = context.PendingOpponentDiscardTriggers.Select(c => c.DefinitionId).ToList();

            // Entities
            dto.Players = context.TurnManager.Players.Select(p => ToDto(p)).Where(d => d != null).ToList()!;
            dto.Map = ToDto(context.MapManager);
            dto.Market = ToDtoList(context.MarketManager.MarketRow);
            dto.MarketDeck = ToDtoList(context.MarketManager.MarketDeck);
            dto.FixedRecruitPiles = context.MarketManager.FixedRecruitPiles?
                .Select(pile => new FixedRecruitPileDto
                {
                    DefinitionId = pile.DefinitionId,
                    Cards = ToDtoList(pile.Cards)
                })
                .ToList() ?? [];
            dto.VoidPile = ToDtoList(context.VoidPile);

            // Stack Serialization
            dto.EffectStack = SerializeEffectStack(context.ActionSystem.ExecutionStack);

            // ActionSystem's targeting state machine - see GameStateDto.ActionSystemState's
            // doc comment for why this travels alongside EffectStack.
            dto.ActionSystemState = context.ActionSystem.CurrentState;
            dto.PendingCardId = context.ActionSystem.PendingCard?.DefinitionId;
            dto.PendingSiteId = context.ActionSystem.PendingSite?.Id;
            dto.PendingMoveSourceNodeId = context.ActionSystem.PendingMoveSource?.Id;
            dto.PendingDevourCardId = context.ActionSystem.PendingDevourCard?.DefinitionId;
            dto.PendingAffectedPlayerColor = context.ActionSystem.PendingAffectedPlayerColor;
            dto.PendingTrophyHallSourceColor = context.ActionSystem.PendingTrophyHallSourceColor;
            dto.PendingDeployedNodeIds = context.ActionSystem.PendingDeployedNodes.Select(n => n.Id).ToList();

            // Computed from the live context, not recomputed independently on the DTO later -
            // see GameStateDto.StateHash's doc comment for why.
            dto.StateHash = context.GetStateHash();

            return dto;
        }

        private static List<EffectContextDto> SerializeEffectStack(Stack<Core.Contexts.EffectContext> executionStack)
        {
            var effectStack = new List<EffectContextDto>();
            if (executionStack.Count > 0)
            {
                var stackList = executionStack.Reverse().ToList();
                foreach (var effect in stackList)
                {
                    effectStack.Add(new EffectContextDto
                    {
                        State = effect.EffectType,
                        SourceCardId = effect.SourceCard?.DefinitionId,
                        RequiresInput = effect.RequiresInput,
                        Description = effect.Description,
                        EffectType = effect.SourceEffect?.Type ?? EffectType.None,
                        RemainingRepeats = effect.RemainingRepeats
                    });
                }
            }
            return effectStack;
        }
    }
}
