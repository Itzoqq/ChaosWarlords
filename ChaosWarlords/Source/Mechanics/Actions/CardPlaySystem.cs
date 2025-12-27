using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using System;
using System.Linq;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Contexts;

using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class CardPlaySystem
    {
        private readonly MatchContext _matchContext;
        private readonly IMatchManager _matchManager;
        private readonly Action _onTargetingStarted;

        public CardPlaySystem(MatchContext matchContext, IMatchManager MatchManager, Action onTargetingStarted)
        {
            _matchContext = matchContext;
            _matchManager = MatchManager;
            _onTargetingStarted = onTargetingStarted;
        }

        public void PlayCard(Card card)
        {
            bool enteredTargeting = false;

            // 1. Check for Targeting Effects
            foreach (var effect in card.Effects)
            {
                if (IsTargetingEffect(effect.Type))
                {
                    if (HasValidTargets(effect.Type))
                    {
                        var state = GetTargetingState(effect.Type);
                        _matchContext.ActionSystem.StartTargeting(state, card);
                        _onTargetingStarted?.Invoke();
                        enteredTargeting = true;
                        break; // Stop checking other effects once we enter targeting
                    }
                    else
                    {
                        // SKIPPING TARGETING:
                        // If no targets exist, we define this as "Targeting phase complete/skipped"
                        // and proceed to play the card data.
                        GameLogger.Log($"Skipping targeting for {card.Name}: No valid targets for {effect.Type}.", LogChannel.Info);
                    }
                }
            }

            // 2. Play immediately if no targeting was started
            if (!enteredTargeting)
            {
                _matchManager.PlayCard(card);
            }
        }

        public bool HasViableTargets(Card card)
        {
            if (card == null) return false;
            
            // Optimization: checking Any directly
            if (!card.Effects.Any(e => IsTargetingEffect(e.Type))) return true;

            foreach (var effect in card.Effects)
            {
                if (IsTargetingEffect(effect.Type))
                {
                    if (HasValidTargets(effect.Type)) return true;
                }
            }
            return false;
        }

        private bool HasValidTargets(EffectType type)
        {
            var p = _matchContext.ActivePlayer;
            var map = _matchContext.MapManager;

            return type switch
            {
                EffectType.Assassinate => map.HasValidAssassinationTarget(p),
                EffectType.Supplant => map.HasValidAssassinationTarget(p),
                EffectType.ReturnUnit => map.HasValidReturnSpyTarget(p) || map.HasValidReturnTroopTarget(p),
                EffectType.PlaceSpy => map.HasValidPlaceSpyTarget(p),
                EffectType.MoveUnit => map.HasValidMoveSource(p),
                EffectType.Devour => p.Hand.Count > 0,
                _ => true
            };
        }

        public static bool IsTargetingEffect(EffectType type)
        {
            return type == EffectType.Assassinate ||
                   type == EffectType.ReturnUnit ||
                   type == EffectType.Supplant ||
                   type == EffectType.PlaceSpy ||
                   type == EffectType.MoveUnit ||
                   type == EffectType.Devour;
        }

        public static ActionState GetTargetingState(EffectType type)
        {
            return type switch
            {
                EffectType.Assassinate => ActionState.TargetingAssassinate,
                EffectType.ReturnUnit => ActionState.TargetingReturn,
                EffectType.Supplant => ActionState.TargetingSupplant,
                EffectType.PlaceSpy => ActionState.TargetingPlaceSpy,
                EffectType.MoveUnit => ActionState.TargetingMoveSource,
                EffectType.Devour => ActionState.TargetingDevourHand,
                _ => ActionState.Normal
            };
        }
    }
}



