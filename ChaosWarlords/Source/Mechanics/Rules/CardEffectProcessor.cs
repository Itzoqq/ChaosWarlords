using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    /// <summary>
    /// Pure Logic Class. Responsible solely for executing the mechanics of card effects.
    /// </summary>
    public class CardEffectProcessor
    {
        public void ResolveEffects(Card card, MatchContext context, bool hasFocus)
        {
            foreach (var effect in card.Effects)
            {
                // Logic to Gate Effects based on Focus
                if (effect.RequiresFocus && !hasFocus) continue;

                ApplyEffect(effect, card, context);
            }
        }

        private void ApplyEffect(CardEffect effect, Card sourceCard, MatchContext context)
        {
            int amount = effect.Amount;
            Player activePlayer = context.ActivePlayer;

            switch (effect.Type)
            {
                case EffectType.GainResource:
                    if (effect.TargetResource == ResourceType.Power)
                        activePlayer.Power += amount;
                    else if (effect.TargetResource == ResourceType.Influence)
                        activePlayer.Influence += amount;
                    break;

                case EffectType.DrawCard:
                    activePlayer.DrawCards(amount);
                    break;

                case EffectType.Promote:
                    // Just adds the credit. Actual selection happens in UI/Input phase.
                    context.TurnManager.CurrentTurnContext.AddPromotionCredit(sourceCard, amount);
                    GameLogger.Log($"Promotion pending! Added {amount} point(s) from {sourceCard.Name}.", LogChannel.Info);
                    break;

                case EffectType.MoveUnit:
                    // Movement is handled by ActionSystem targeting, this is just confirmation
                    if (context.MapManager.HasValidMoveSource(activePlayer))
                    {
                        context.ActionSystem.StartTargeting(ActionState.TargetingMoveSource, sourceCard);
                        GameLogger.Log($"{sourceCard.Name}: Select a unit to Move.", LogChannel.Input);
                    }
                    else
                    {
                        GameLogger.Log($"{sourceCard.Name}: No valid units to move.", LogChannel.Warning);
                    }
                    break;

                case EffectType.Assassinate:
                    if (context.MapManager.HasValidAssassinationTarget(activePlayer))
                    {
                        context.ActionSystem.StartTargeting(ActionState.TargetingAssassinate, sourceCard);
                        GameLogger.Log($"{sourceCard.Name}: Select a valid target to Assassinate.", LogChannel.Input);
                    }
                    else
                    {
                        GameLogger.Log($"{sourceCard.Name}: No valid targets to Assassinate.", LogChannel.Warning);
                    }
                    break;

                case EffectType.Supplant:
                    bool canAssassinate = context.MapManager.HasValidAssassinationTarget(activePlayer);
                    bool hasTroops = activePlayer.TroopsInBarracks > 0;

                    if (canAssassinate && hasTroops)
                    {
                        context.ActionSystem.StartTargeting(ActionState.TargetingSupplant, sourceCard);
                        GameLogger.Log($"{sourceCard.Name}: Select a valid target to Supplant.", LogChannel.Input);
                    }
                    else
                    {
                        if (!hasTroops) GameLogger.Log($"{sourceCard.Name}: Cannot Supplant (No Troops in Barracks).", LogChannel.Warning);
                        else GameLogger.Log($"{sourceCard.Name}: No valid targets to Supplant.", LogChannel.Warning);
                    }
                    break;

                case EffectType.PlaceSpy:
                    if (context.MapManager.HasValidPlaceSpyTarget(activePlayer) && activePlayer.SpiesInBarracks > 0)
                    {
                        context.ActionSystem.StartTargeting(ActionState.TargetingPlaceSpy, sourceCard);
                        GameLogger.Log($"{sourceCard.Name}: Select a Site to Place Spy.", LogChannel.Input);
                    }
                    else
                    {
                         if (activePlayer.SpiesInBarracks <= 0) GameLogger.Log($"{sourceCard.Name}: Cannot Place Spy (No Spies in Barracks).", LogChannel.Warning);
                         else GameLogger.Log($"{sourceCard.Name}: No valid sites to Place Spy.", LogChannel.Warning);
                    }
                    break;

                case EffectType.ReturnUnit:
                    // Check for both Spy Return or Troop Return targets?
                    // The effect "ReturnUnit" usually implies returning an enemy troop OR spy.
                    // Tyrants rules: "Return a troop" or "Return a spy" are often specific. 
                    // But if the card just says "Return", it usually means "Return a troop".
                    // Let's check CardEffects.cs enum if we have separate types?
                    // It seems we only have ReturnUnit. 
                    // Looking at ActionSystem.cs: HandleReturn handles TROOPS logic.
                    // HandleReturnSpy is separate.
                    // So return implies TROOPS here.
                    
                    if (context.MapManager.HasValidReturnTroopTarget(activePlayer))
                    {
                        context.ActionSystem.StartTargeting(ActionState.TargetingReturn, sourceCard);
                        GameLogger.Log($"{sourceCard.Name}: Select a unit to Return.", LogChannel.Input);
                    }
                    else
                    {
                         GameLogger.Log($"{sourceCard.Name}: No valid units to Return.", LogChannel.Warning);
                    }
                    break;

                case EffectType.Devour:
                    // context.ActionSystem.TryStartDevourHand(sourceCard) already checks hand count.
                    // But we can add a log here if we want consistency, or just let it handle it.
                    // Let's rely on the method but we can check here to be explicit.
                    if (activePlayer.Hand.Count > 0)
                    {
                        context.ActionSystem.TryStartDevourHand(sourceCard);
                    }
                    else
                    {
                         GameLogger.Log($"{sourceCard.Name}: Hand empty, cannot Devour.", LogChannel.Warning);
                    }
                    break;
            }
        }
    }
}