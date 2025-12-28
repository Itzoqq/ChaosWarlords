using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Rules
{
    /// <summary>
    /// Pure Logic Class. Responsible solely for executing the mechanics of card effects.
    /// </summary>
    public class CardEffectProcessor
    {
        public static void ResolveEffects(Card card, MatchContext context, bool hasFocus)
        {
            foreach (var effect in card.Effects)
            {
                // Logic to Gate Effects based on Focus
                if (effect.RequiresFocus && !hasFocus) continue;

                ApplyEffect(effect, card, context);
            }
        }

        private static void ApplyEffect(CardEffect effect, Card sourceCard, MatchContext context)
        {
            Action action = effect.Type switch
            {
                EffectType.GainResource => () => ApplyGainResource(effect, context),
                EffectType.DrawCard => () => ApplyDrawCard(effect, context),
                EffectType.Promote => () => ApplyPromote(effect, sourceCard, context),
                EffectType.MoveUnit => () => ApplyMoveUnit(sourceCard, context),
                EffectType.Assassinate => () => ApplyAssassinate(sourceCard, context),
                EffectType.Supplant => () => ApplySupplant(sourceCard, context),
                EffectType.PlaceSpy => () => ApplyPlaceSpy(sourceCard, context),
                EffectType.ReturnUnit => () => ApplyReturnUnit(sourceCard, context),
                EffectType.Devour => () => ApplyDevour(sourceCard, context),
                _ => () => { }
            };

            action();
        }

        private static void ApplyGainResource(CardEffect effect, MatchContext context)
        {
            if (effect.TargetResource == ResourceType.Power)
                context.PlayerStateManager.AddPower(context.ActivePlayer, effect.Amount);
            else if (effect.TargetResource == ResourceType.Influence)
                context.PlayerStateManager.AddInfluence(context.ActivePlayer, effect.Amount);
        }

        private static void ApplyDrawCard(CardEffect effect, MatchContext context)
        {
            context.PlayerStateManager.DrawCards(context.ActivePlayer, effect.Amount, context.Random);
        }

        private static void ApplyPromote(CardEffect effect, Card sourceCard, MatchContext context)
        {
            context.TurnManager.CurrentTurnContext.AddPromotionCredit(sourceCard, effect.Amount);
            GameLogger.Log($"Promotion pending! Added {effect.Amount} point(s) from {sourceCard.Name}.", LogChannel.Info);
        }

        private static void ApplyMoveUnit(Card sourceCard, MatchContext context)
        {
            if (context.MapManager.HasValidMoveSource(context.ActivePlayer))
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingMoveSource, sourceCard);
                GameLogger.Log($"{sourceCard.Name}: Select a unit to Move.", LogChannel.Input);
            }
            else
            {
                GameLogger.Log($"{sourceCard.Name}: No valid units to move.", LogChannel.Warning);
            }
        }

        private static void ApplyAssassinate(Card sourceCard, MatchContext context)
        {
            if (context.MapManager.HasValidAssassinationTarget(context.ActivePlayer))
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingAssassinate, sourceCard);
                GameLogger.Log($"{sourceCard.Name}: Select a valid target to Assassinate.", LogChannel.Input);
            }
            else
            {
                GameLogger.Log($"{sourceCard.Name}: No valid targets to Assassinate.", LogChannel.Warning);
            }
        }

        private static void ApplySupplant(Card sourceCard, MatchContext context)
        {
            bool canAssassinate = context.MapManager.HasValidAssassinationTarget(context.ActivePlayer);
            bool hasTroops = context.ActivePlayer.TroopsInBarracks > 0;

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
        }

        private static void ApplyPlaceSpy(Card sourceCard, MatchContext context)
        {
            if (context.MapManager.HasValidPlaceSpyTarget(context.ActivePlayer) && context.ActivePlayer.SpiesInBarracks > 0)
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingPlaceSpy, sourceCard);
                GameLogger.Log($"{sourceCard.Name}: Select a Site to Place Spy.", LogChannel.Input);
            }
            else
            {
                if (context.ActivePlayer.SpiesInBarracks <= 0) GameLogger.Log($"{sourceCard.Name}: Cannot Place Spy (No Spies in Barracks).", LogChannel.Warning);
                else GameLogger.Log($"{sourceCard.Name}: No valid sites to Place Spy.", LogChannel.Warning);
            }
        }

        private static void ApplyReturnUnit(Card sourceCard, MatchContext context)
        {
            if (context.MapManager.HasValidReturnTroopTarget(context.ActivePlayer))
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingReturn, sourceCard);
                GameLogger.Log($"{sourceCard.Name}: Select a unit to Return.", LogChannel.Input);
            }
            else
            {
                GameLogger.Log($"{sourceCard.Name}: No valid units to Return.", LogChannel.Warning);
            }
        }

        private static void ApplyDevour(Card sourceCard, MatchContext context)
        {
            if (context.ActivePlayer.Hand.Count > 0)
            {
                context.ActionSystem.TryStartDevourHand(sourceCard);
            }
            else
            {
                GameLogger.Log($"{sourceCard.Name}: Hand empty, cannot Devour.", LogChannel.Warning);
            }
        }
    }
}


