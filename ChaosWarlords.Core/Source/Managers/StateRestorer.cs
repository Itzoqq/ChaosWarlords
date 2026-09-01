using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
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
            // VoidPile carries full CardDtos (Location/RuntimeId matter - see RestoreCardDtoList).
            // CardsMarkedForTurnEndDevour/PendingOpponentDiscardTriggers are plain definitional-
            // id lists (MatchManager.EndTurn sets their Location itself when it processes them,
            // never reads it beforehand, so a bare re-resolved Card is sufficient there) - see
            // RestoreCardIdList's own doc comment for the resulting known limitation.
            RestoreCardDtoList(context.VoidPile, dto.VoidPile, context.CardDatabase);
            RestoreCardIdList(context.CardsMarkedForTurnEndDevour, dto.MarkedForTurnEndDevourCardIds, context.CardDatabase);
            RestoreCardIdList(context.PendingOpponentDiscardTriggers, dto.PendingOpponentDiscardTriggerCardIds, context.CardDatabase);

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

        /// <summary>
        /// Clears <paramref name="target"/> and repopulates it by looking up each definitional
        /// id in <paramref name="definitionIds"/> against the card database, skipping any that
        /// no longer resolve. Used for CardsMarkedForTurnEndDevour/PendingOpponentDiscardTriggers
        /// - both are transient, single-turn markers whose only consumer (MatchManager.EndTurn)
        /// sets each card's Location itself when processing it rather than reading a
        /// pre-existing value, so a freshly-resolved Card (not necessarily the same reference
        /// sitting in the restored Hand/PlayedCards) is sufficient. KNOWN LIMITATION: because
        /// these are freshly-resolved rather than looked up in Hand/PlayedCards by RuntimeId,
        /// MatchManager.EndTurn's List.Remove(card) (reference equality) would silently fail to
        /// find/remove them if a rollback happens between one of these being marked and
        /// EndTurn actually processing it - a narrow window, not fixed by this pass.
        /// </summary>
        private static void RestoreCardIdList(List<Card> target, IEnumerable<string>? definitionIds, ICardDatabase db)
        {
            target.Clear();
            if (definitionIds == null) return;

            foreach (var definitionId in definitionIds)
            {
                var card = db.GetCardById(definitionId);
                if (card != null) target.Add(card);
            }
        }

        /// <summary>
        /// Clears <paramref name="target"/> and repopulates it from full CardDtos, restoring
        /// each resolved Card's Location and RuntimeId from the snapshot (not just its
        /// definitional identity) - see ResolveCard. Used for VoidPile, where downstream code
        /// can care about both (e.g. a card's Location should read Void, and a UI/command
        /// already holding that card's RuntimeId should still find it after a restore).
        /// </summary>
        private static void RestoreCardDtoList(List<Card> target, List<CardDto>? dtos, ICardDatabase db)
        {
            target.Clear();
            if (dtos == null) return;

            foreach (var d in dtos)
            {
                var card = ResolveCard(d, db);
                if (card != null) target.Add(card);
            }
        }

        /// <summary>
        /// Resolves a CardDto back to a live Card via its DefinitionId, then carries over the
        /// snapshot's Location and RuntimeId - the two pieces of per-instance state a fresh
        /// ICardDatabase.GetCardById lookup can't know on its own. Shared by every restore path
        /// that has a full CardDto to work from (player collections, Market, VoidPile).
        /// </summary>
        private static Card? ResolveCard(CardDto d, ICardDatabase db)
        {
            var card = db.GetCardById(d.DefinitionId);
            if (card == null) return null;

            card.Location = d.Location;
            card.RuntimeId = d.RuntimeId;
            return card;
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
                        RestoreSiteSpies(site, siteDto.Spies);
                    }
                }
            }
        }

        /// <summary>
        /// Clears a site's spies and repopulates it from the DTO's string-encoded colors,
        /// skipping any that don't parse. Split out of RestoreMap's own site loop - same
        /// "clear then repopulate, tolerating bad entries" shape RestoreCardIdList uses.
        /// </summary>
        private static void RestoreSiteSpies(Site site, List<string>? spyColors)
        {
            site.Spies.Clear();
            if (spyColors == null) return;

            foreach (var s in spyColors)
            {
                if (Enum.TryParse<PlayerColor>(s, out var color))
                {
                    site.Spies.Add(color);
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
                 var card = ResolveCard(d, db);
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
                     var card = ResolveCard(d, context.CardDatabase);
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
