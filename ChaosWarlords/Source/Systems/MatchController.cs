using System;
using System.Linq;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    /// <summary>
    /// Handles the "Simulation" logic of the game.
    /// Responsible for enforcing rules, executing card effects, and managing turn flow.
    /// This class should have NO dependency on Graphics/UI.
    /// </summary>
    public class MatchController
    {
        private readonly MatchContext _context;

        public MatchController(MatchContext context)
        {
            _context = context;
        }

        public void PlayCard(Card card)
        {
            // 1. Notify TurnManager (Tracks stats like Focus)
            _context.TurnManager.PlayCard(card);

            // 2. Execute Immediate Effects
            ResolveCardEffects(card);

            // 3. Move from Hand to Played
            MoveCardToPlayed(card);
        }

        public void ResolveCardEffects(Card card)
        {
            // 1. Check Focus Condition
            // Check if ANY card of that aspect is already in the played pile.
            bool hasFocus = _context.ActivePlayer.PlayedCards.Any(c => c.Aspect == card.Aspect);

            foreach (var effect in card.Effects)
            {
                // 2. Skip conditional effects if condition is not met
                if (effect.RequiresFocus && !hasFocus)
                {
                    continue;
                }

                // 3. Apply Immediate Effects
                switch (effect.Type)
                {
                    case EffectType.GainResource:
                        if (effect.TargetResource == ResourceType.Power)
                            _context.ActivePlayer.Power += effect.Amount;
                        else if (effect.TargetResource == ResourceType.Influence)
                            _context.ActivePlayer.Influence += effect.Amount;
                        else if (effect.TargetResource == ResourceType.VictoryPoints)
                            _context.ActivePlayer.VictoryPoints += effect.Amount;
                        break;

                    case EffectType.DrawCard:
                        _context.ActivePlayer.DrawCards(effect.Amount);
                        break;

                    case EffectType.Devour:
                        // Only handle AUTO devour here (e.g. "Devour top card of deck"). 
                        // Targeted devour is handled by InputModes.
                        break;
                }
            }
        }

        public void MoveCardToPlayed(Card card)
        {
            if (_context.ActivePlayer.Hand.Contains(card))
            {
                _context.ActivePlayer.Hand.Remove(card);
                _context.ActivePlayer.PlayedCards.Add(card);
            }
        }

        /// <summary>
        /// Validation: Checks if the current turn can be ended.
        /// </summary>
        public bool CanEndTurn(out string reason)
        {
            if (_context.ActivePlayer.Hand.Count > 0)
            {
                reason = "You must play all cards in your hand before ending your turn.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public void EndTurn()
        {
            // 1. Map Rewards
            _context.MapManager.DistributeControlRewards(_context.ActivePlayer);

            // 2. Cleanup: Move Hand + Played -> Discard
            // This MUST happen before DrawCards to cycle deck correctly
            _context.ActivePlayer.CleanUpTurn();

            // 3. Draw New Hand
            _context.ActivePlayer.DrawCards(5);

            // 4. Switch Player
            _context.TurnManager.EndTurn();
            _context.ActionSystem.SetCurrentPlayer(_context.ActivePlayer);
        }
    }
}