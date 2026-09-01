using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Core.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Managers
{
    /// <summary>
    /// Restores a <see cref="MatchContext"/> in-place from a <see cref="GameStateDto"/> snapshot.
    /// Used for transactional rollback: if a command fails partway through execution,
    /// the context is reverted to a snapshot taken before the command ran.
    /// </summary>
    public static class StateRestorer
    {
        public static void RestoreState(MatchContext context, GameStateDto dto)
        {
            // 1. Meta State
            context.CurrentTurnNumber = dto.TurnNumber;
            context.CurrentPhase = dto.Phase;
            context.SequenceNumber = dto.SequenceNumber;
            
            // 2. Map State
            RestoreMap(context.MapManager, dto.Map);

            // 3. Player State
            RestorePlayers(context, dto.Players);

            // 4. Market State
            RestoreMarket(context, dto.Market);
            
            // 5. Void / Transient State
            context.VoidPile.Clear();
            if (dto.VoidPile != null)
            {
                foreach (var cardDto in dto.VoidPile)
                {
                    var card = context.CardDatabase.GetCardById(cardDto.DefinitionId);
                    if (card != null) context.VoidPile.Add(card);
                }
            }
            
            context.CardsMarkedForTurnEndDevour.Clear();
            if (dto.MarkedForTurnEndDevourCardIds != null)
            {
                foreach (var id in dto.MarkedForTurnEndDevourCardIds)
                {
                     // Card equality is ID-based, so a database lookup is sufficient here.
                     var card = context.CardDatabase.GetCardById(id);
                     if (card != null) context.CardsMarkedForTurnEndDevour.Add(card);
                }
            }

            context.PendingOpponentDiscardTriggers.Clear();
            if (dto.PendingOpponentDiscardTriggerCardIds != null)
            {
                foreach (var id in dto.PendingOpponentDiscardTriggerCardIds)
                {
                    var card = context.CardDatabase.GetCardById(id);
                    if (card != null) context.PendingOpponentDiscardTriggers.Add(card);
                }
            }

            // 6. Action Stack
            // EffectContext carries runtime delegates (OnResolved/OnCancelled) that can't be
            // serialized, so restored effects get no-op callbacks - sufficient for a rollback,
            // where the stack is discarded rather than resumed.
            // DtoMapper.SerializeEffectStack reverses the stack into a list (List[0] = stack bottom),
            // so restoring in list order rebuilds the stack correctly.
            context.ActionSystem.ExecutionStack.Clear();
            if (dto.EffectStack != null)
            {
                foreach (var effectDto in dto.EffectStack)
                {
                     RestoreEffect(context, effectDto);
                }
            }

            // 7. ActionSystem's targeting state machine (CurrentState + Pending* fields) - see
            // GameStateDto.ActionSystemState's doc comment for why this travels separately
            // from EffectStack and how Card/Site/MapNode are re-resolved here.
            var pendingCard = dto.PendingCardId != null ? context.CardDatabase.GetCardById(dto.PendingCardId) : null;
            var pendingSite = dto.PendingSiteId is int siteId ? context.MapManager.Sites.FirstOrDefault(s => s.Id == siteId) : null;
            var pendingMoveSource = dto.PendingMoveSourceNodeId is int nodeId ? context.MapManager.Nodes.FirstOrDefault(n => n.Id == nodeId) : null;
            var pendingDevourCard = dto.PendingDevourCardId != null ? context.CardDatabase.GetCardById(dto.PendingDevourCardId) : null;
            context.ActionSystem.RestorePendingState(dto.ActionSystemState, pendingCard, pendingSite, pendingMoveSource, pendingDevourCard);
        }

        private static void RestoreMap(IMapManager mapManager, MapDto mapDto)
        {
            if (mapDto.Nodes != null)
            {
                foreach (var nodeDto in mapDto.Nodes)
                {
                    var node = mapManager.Nodes.FirstOrDefault(n => n.Id == nodeDto.Id);
                    if (node != null)
                    {
                        node.Occupant = nodeDto.Occupant;
                        // Determine presence/visibility if cached?
                    }
                }
            }

            if (mapDto.Sites != null)
            {
                foreach (var siteDto in mapDto.Sites)
                {
                    var site = mapManager.Sites.FirstOrDefault(s => s.Id == siteDto.Id);
                    if (site != null)
                    {
                        site.Owner = siteDto.Owner;
                        site.Spies.Clear();
                        if (siteDto.Spies != null)
                        {
                            foreach (var s in siteDto.Spies)
                            {
                                if (Enum.TryParse<PlayerColor>(s, out var color))
                                {
                                    site.Spies.Add(color);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void RestorePlayers(MatchContext context, List<PlayerDto> playerDtos)
        {
            foreach (var pDto in playerDtos)
            {
                var player = context.TurnManager.Players.FirstOrDefault(p => p.SeatIndex == pDto.SeatIndex);
                if (player != null)
                {
                    player.SetPower(pDto.Power);
                    player.SetInfluence(pDto.Influence);
                    player.VictoryPoints = pDto.VictoryPoints;
                    player.TroopsInBarracks = pDto.Troops;
                    player.SpiesInBarracks = pDto.Spies;
                    
                    // Card Lists
                    RestorePlayerCollection(player, pDto.Hand, context.CardDatabase, (p, c) => p.AddToHand(c), p => p.ClearHand());
                    RestorePlayerCollection(player, pDto.Deck, context.CardDatabase, (p, c) => p.DeckManager.ForceAdd(c), p => p.DeckManager.Clear());
                    RestorePlayerCollection(player, pDto.DiscardPile, context.CardDatabase, (p, c) => p.DeckManager.AddToDiscard(c), p => p.DeckManager.ClearDiscard());
                    RestorePlayerCollection(player, pDto.PlayedCards, context.CardDatabase, (p, c) => p.AddToPlayed(c), p => p.ClearPlayed());
                    RestorePlayerCollection(player, pDto.InnerCircle, context.CardDatabase, (p, c) => p.AddToInnerCircle(c), p => p.ClearInnerCircle());
                }
            }
        }

        private static void RestorePlayerCollection(Player player, List<CardDto> dtos, ICardDatabase db, Action<Player, Card> addAction, Action<Player> clearAction)
        {
             clearAction(player);
             if (dtos == null) return;
             foreach (var d in dtos)
             {
                 var card = db.GetCardById(d.DefinitionId);
                 if (card != null) addAction(player, card);
             }
        }



        private static void RestoreMarket(MatchContext context, List<CardDto> marketDtos)
        {
            var mgr = context.MarketManager;
            mgr.MarketRow.Clear();
            if (marketDtos != null)
            {
                 foreach (var d in marketDtos)
                 {
                     var card = context.CardDatabase.GetCardById(d.DefinitionId);
                     if (card != null) mgr.MarketRow.Add(card);
                 }
            }
        }

        /// <summary>
        /// Reconstructs a single EffectContext from its DTO and pushes it back onto the stack.
        /// Note: <see cref="EffectContextDto.State"/> carries the ActionState (targeting phase), while
        /// <see cref="EffectContextDto.EffectType"/> carries the card's EffectType, used to look up the
        /// matching CardEffect definition on the source card.
        /// </summary>
        private static void RestoreEffect(MatchContext context, EffectContextDto effectDto)
        {
             // Reconstruct EffectContext. This is tricky because we need the original reference
             // to the card's Effect object, not just a serialized copy.
             if (string.IsNullOrEmpty(effectDto.SourceCardId)) return;

             var sourceCard = context.CardDatabase.GetCardById(effectDto.SourceCardId);
             if (sourceCard == null) return;

             CardEffect? sourceEffect = null;
             if (effectDto.State != ActionState.Normal)
             {
                 sourceEffect = sourceCard.Effects.FirstOrDefault(e => e.Type == effectDto.EffectType);
             }

             var ctx = new EffectContext(
                 effectDto.State,
                 sourceCard,
                 effectDto.RequiresInput,
                 effectDto.Description,
                 _ => { }, // Dummy callback, state restore cannot recover runtime delegates yet
                 sourceEffect
             );

             // Push to stack
             context.ActionSystem.PushEffect(ctx);
        }
    }
}
