using System;
using System.Linq;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class MatchController
    {
        private readonly MatchContext _context;

        public MatchController(MatchContext context)
        {
            _context = context;
        }

        public void PlayCard(Card card)
        {
            // --- 1. PRE-CALCULATION (SNAPSHOT) ---
            // Calculate logic-dependent state BEFORE mutating any lists.
            // This prevents "Temporal Coupling" bugs where changing the order of 
            // the lines below would break the "Focus" check.

            // Check count BEFORE incrementing (so we look for > 0, not > 1)
            int currentCount = _context.TurnManager.CurrentTurnContext.GetAspectCount(card.Aspect);
            bool playedAnother = currentCount > 0;

            // Check hand BEFORE moving the card (card is still in Hand, so we exclude it)
            bool canRevealFromHand = _context.ActivePlayer.Hand.Any(c => c.Aspect == card.Aspect && c != card);

            // Determine Focus state now
            bool hasFocus = playedAnother || canRevealFromHand;

            // --- 2. STATE MUTATION ---
            // Now safe to update the game state.
            _context.TurnManager.PlayCard(card); // Update stats
            MoveCardToPlayed(card);              // Move visually/logically

            // --- 3. EXECUTION ---
            // Pass the pre-calculated 'hasFocus' boolean. 
            // This method is now pure regarding decision logic; it just executes.
            ResolveCardEffects(card, hasFocus);
        }

        // UPDATED: Now accepts 'hasFocus' as a parameter
        public void ResolveCardEffects(Card card, bool hasFocus)
        {
            // NOTE: The internal calculation logic was removed from here.
            // This makes this method robust; it doesn't care if the card 
            // is currently in the Hand, Discard, or Void.

            foreach (var effect in card.Effects)
            {
                // Skip conditional effects if Focus requirements aren't met
                if (effect.RequiresFocus && !hasFocus)
                {
                    continue;
                }

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
                        // Auto-devour logic would go here
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
            _context.ActivePlayer.CleanUpTurn();

            // 3. Draw New Hand
            _context.ActivePlayer.DrawCards(5);

            // 4. Switch Player (This now resets TurnContext internally)
            _context.TurnManager.EndTurn();

            // 5. Update ActionSystem target
            _context.ActionSystem.SetCurrentPlayer(_context.ActivePlayer);
        }
    }
}