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

            switch (effect.Type)
            {
                case EffectType.GainResource:
                    if (effect.TargetResource == ResourceType.Power)
                        context.ActivePlayer.Power += amount;
                    else if (effect.TargetResource == ResourceType.Influence)
                        context.ActivePlayer.Influence += amount;
                    break;

                case EffectType.DrawCard:
                    context.ActivePlayer.DrawCards(amount);
                    break;

                case EffectType.Promote:
                    // Just adds the credit. Actual selection happens in UI/Input phase.
                    context.TurnManager.CurrentTurnContext.AddPromotionCredit(sourceCard, amount);
                    GameLogger.Log($"Promotion pending! Added {amount} point(s) from {sourceCard.Name}.", LogChannel.Info);
                    break;

                case EffectType.MoveUnit:
                    // Movement is handled by ActionSystem targeting, this is just confirmation
                    GameLogger.Log($"{sourceCard.Name}: Movement effect resolved.", LogChannel.Info);
                    break;

                case EffectType.Assassinate:
                    context.ActionSystem.StartTargeting(ActionState.TargetingAssassinate, sourceCard);
                    GameLogger.Log($"{sourceCard.Name}: Select a valid target to Assassinate.", LogChannel.Input);
                    break;

                case EffectType.Supplant:
                    context.ActionSystem.StartTargeting(ActionState.TargetingSupplant, sourceCard);
                    GameLogger.Log($"{sourceCard.Name}: Select a valid target to Supplant.", LogChannel.Input);
                    break;

                case EffectType.PlaceSpy:
                    context.ActionSystem.StartTargeting(ActionState.TargetingPlaceSpy, sourceCard);
                    GameLogger.Log($"{sourceCard.Name}: Select a Site to Place Spy.", LogChannel.Input);
                    break;

                case EffectType.ReturnUnit:
                    context.ActionSystem.StartTargeting(ActionState.TargetingReturn, sourceCard);
                    GameLogger.Log($"{sourceCard.Name}: Select a unit to Return.", LogChannel.Input);
                    break;

                case EffectType.Devour:
                    context.ActionSystem.TryStartDevourHand(sourceCard);
                    break;
            }
        }
    }
}