using System;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Managers
{
    public class PlayerStateManager : IPlayerStateManager
    {
        private readonly IGameLogger _logger;

        public PlayerStateManager(IGameLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // --- Resources ---

        public void AddPower(Player player, int amount)
        {
            if (amount <= 0) return;
            player.Power += amount;
            _logger.Log($"[State] {player.DisplayName} gained {amount} Power. Total: {player.Power}", LogChannel.Info);
        }

        public bool TrySpendPower(Player player, int amount)
        {
            if (amount <= 0) return false;
            if (player.Power >= amount)
            {
                player.Power -= amount;
                _logger.Log($"[State] {player.DisplayName} spent {amount} Power. Remaining: {player.Power}", LogChannel.Info);
                return true;
            }
            return false;
        }

        public void AddInfluence(Player player, int amount)
        {
            if (amount <= 0) return;
            player.Influence += amount;
            _logger.Log($"[State] {player.DisplayName} gained {amount} Influence. Total: {player.Influence}", LogChannel.Info);
        }

        public bool TrySpendInfluence(Player player, int amount)
        {
            if (amount <= 0) return false;
            if (player.Influence >= amount)
            {
                player.Influence -= amount;
                _logger.Log($"[State] {player.DisplayName} spent {amount} Influence. Remaining: {player.Influence}", LogChannel.Info);
                return true;
            }
            return false;
        }

        public void AddVictoryPoints(Player player, int amount)
        {
            if (amount == 0) return;
            player.VictoryPoints += amount;
            _logger.Log($"[State] {player.DisplayName} gained {amount} VP. Total: {player.VictoryPoints}", LogChannel.Info);
        }

        // --- Military ---

        public void AddTroops(Player player, int amount)
        {
            if (amount <= 0) return;
            player.TroopsInBarracks += amount;
            _logger.Log($"[State] {player.DisplayName} recruited {amount} Troops. Total: {player.TroopsInBarracks}", LogChannel.Info);
        }

        public void RemoveTroops(Player player, int amount)
        {
            if (amount <= 0) return;
            player.TroopsInBarracks = Math.Max(0, player.TroopsInBarracks - amount);
            _logger.Log($"[State] {player.DisplayName} lost {amount} Troops. Remaining: {player.TroopsInBarracks}", LogChannel.Info);
        }

        public void AddSpies(Player player, int amount)
        {
            if (amount <= 0) return;
            player.SpiesInBarracks += amount;
            _logger.Log($"[State] {player.DisplayName} recruited {amount} Spies. Total: {player.SpiesInBarracks}", LogChannel.Info);
        }

        public void RemoveSpies(Player player, int amount)
        {
            if (amount <= 0) return;
            player.SpiesInBarracks = Math.Max(0, player.SpiesInBarracks - amount);
            _logger.Log($"[State] {player.DisplayName} lost {amount} Spies. Remaining: {player.SpiesInBarracks}", LogChannel.Info);
        }

        public void AddTrophy(Player player)
        {
            player.TrophyHall++;
            _logger.Log($"[State] {player.DisplayName} obtained a Trophy! Total: {player.TrophyHall}", LogChannel.Info);
        }

        // --- Card Management ---

        public void DrawCards(Player player, int count, IGameRandom random)
        {
            if (count <= 0) return;
            
            // Snapshot hand before draw to calculate diff (or just log what's added)
            int preCount = player.Hand.Count;
            
            // Delegate to Player implementation which handles deck/shuffle logic
            player.DrawCards(count, random);
            
            _logger.Log($"[State] {player.DisplayName} drew {count} cards.", LogChannel.Info);
            
            // Log specifically WHICH cards were added (the last 'count' cards)
            // Note: Player.DrawCards adds to the END of the list.
            for (int i = preCount; i < player.Hand.Count; i++)
            {
                 var card = player.Hand[i];
                 _logger.Log($"   > Drawn: {card.Name} (ID: {card.Id})", LogChannel.Info);
            }
            
            LogHandState(player);
        }

        public void LogHandState(Player player)
        {
             _logger.Log($"[Hand Dump] {player.DisplayName} Hand ({player.Hand.Count}):", LogChannel.Debug);
             foreach(var card in player.Hand)
             {
                 _logger.Log($"   - {card.Name} [{card.Id}]", LogChannel.Debug);
             }
        }

        public void PlayCard(Player player, Card card)
        {
            if (card is null || !player.Hand.Contains(card)) return;

            player.Hand.Remove(card);
            card.Location = CardLocation.Played;
            player.PlayedCards.Add(card);
            
            _logger.Log($"[State] {player.DisplayName} played '{card.Name}'", LogChannel.Info);
        }

        public void AcquireCard(Player player, Card card)
        {
            if (card is null) return;
            player.DeckManager.AddToDiscard(card);
            _logger.Log($"[State] {player.DisplayName} acquired '{card.Name}'", LogChannel.Info);
        }

        public bool TryPromoteCard(Player player, Card card, out string errorMessage)
        {
            // Using Player's logic for now, or move it here?
            // Moving logic here allows better centralization, but Player already has it.
            // Let's use player's logic but wrap it.
            bool success = player.TryPromoteCard(card, out errorMessage);
            if (success)
            {
                _logger.Log($"[State] {player.DisplayName} promoted '{card.Name}'", LogChannel.Info);
            }
            else
            {
                _logger.Log($"[State] Promotion failed for '{card.Name}': {errorMessage}", LogChannel.Warning);
            }
            return success;
        }

        public void DevourCard(Player player, Card card)
        {
            // Logic to remove card from game
            bool removed = player.Hand.Remove(card);
            if (!removed) removed = player.PlayedCards.Remove(card);
            
            // If we found it:
            if (removed)
            {
                card.Location = CardLocation.Void;
                _logger.Log($"[State] {player.DisplayName} devoured (trashed) '{card.Name}'", LogChannel.Info);
            }
        }

        public void CleanUpTurn(Player player)
        {
            _logger.Log($"[Cleanup] Clearning hand for {player.DisplayName}. Content was:", LogChannel.Debug);
            LogHandState(player);
            
            player.CleanUpTurn();
            _logger.Log($"[State] {player.DisplayName} ended turn. Hand and Played cards discarded.", LogChannel.Info);
        }
    }
}
