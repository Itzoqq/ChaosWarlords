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
            // We must calculate Focus BEFORE moving the card to 'Played' or modifying the turn stats.
            // Focus Condition: Played another card of same aspect OR Reveal one from hand.

            int currentCount = _context.TurnManager.CurrentTurnContext.GetAspectCount(card.Aspect);
            bool playedAnother = currentCount > 0;

            // Check hand for a DIFFERENT card of the same aspect
            bool canRevealFromHand = _context.ActivePlayer.Hand.Any(c => c.Aspect == card.Aspect && c != card);

            bool hasFocus = playedAnother || canRevealFromHand;

            // --- 2. STATE MUTATION ---

            // Remove from Hand if present
            if (_context.ActivePlayer.Hand.Contains(card))
            {
                _context.ActivePlayer.Hand.Remove(card);
            }

            // Add to PlayedCards
            _context.ActivePlayer.PlayedCards.Add(card);

            // Update Stats (This increments the Aspect count for the turn)
            _context.TurnManager.PlayCard(card);

            // Resolve effects with the pre-calculated Focus state
            ResolveCardEffects(card, hasFocus);
        }

        public void ResolveCardEffects(Card card, bool hasFocus)
        {
            foreach (var effect in card.Effects)
            {
                // --- Logic to Gate Effects based on Focus ---
                // If the specific effect instruction requires Focus, 
                // and we do NOT have Focus, we skip this effect entirely.
                if (effect.RequiresFocus && !hasFocus)
                {
                    continue;
                }

                int finalAmount = effect.Amount;

                // Optional: If you implement Focus as a "Bonus" to a base effect (e.g. Gain 1 Power, Focus +2)
                // You would handle that here using a property like effect.FocusBonus.
                // But for "Atomic" effects (where the whole line is conditional), the check above is sufficient.

                switch (effect.Type)
                {
                    case EffectType.GainResource:
                        if (effect.TargetResource == ResourceType.Power)
                            _context.ActivePlayer.Power += finalAmount;
                        else if (effect.TargetResource == ResourceType.Influence)
                            _context.ActivePlayer.Influence += finalAmount;
                        break;

                    case EffectType.DrawCard:
                        _context.ActivePlayer.DrawCards(finalAmount);
                        break;

                    case EffectType.Promote:
                        // We do NOT trigger targeting here. We just add the credit.
                        // Actual promotion happens at the end of the turn.
                        _context.TurnManager.CurrentTurnContext.AddPromotionCredit(card, finalAmount);
                        GameLogger.Log($"Promotion pending! Added {finalAmount} point(s) from {card.Name}.", LogChannel.Info);
                        break;

                    case EffectType.Devour:
                        // Logic for devouring cards if applicable
                        break;

                    case EffectType.MoveUnit:
                        // The actual movement logic is handled by the ActionSystem/MapManager 
                        // during the targeting phase. We log it here to confirm resolution.
                        GameLogger.Log($"{card.Name}: Movement effect resolved.", LogChannel.Info);
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
            if (_context.TurnManager.CurrentTurnContext.PendingPromotionsCount > 0)
            {
                // Optional: Could block here if strictly enforcing cleanup
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

            // 4. Switch Player
            _context.TurnManager.EndTurn();
        }
    }
}