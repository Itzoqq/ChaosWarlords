using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for muting player state (Resources, Hand, Deck, etc.).
    /// Centralizes all state changes to facilitate logging, networking, and replay systems.
    /// </summary>
    public interface IPlayerStateManager
    {
        // --- Resources ---
        void AddPower(Player player, int amount);
        bool TrySpendPower(Player player, int amount);

        void AddInfluence(Player player, int amount);
        bool TrySpendInfluence(Player player, int amount);

        void AddVictoryPoints(Player player, int amount);

        // --- Military ---
        void AddTroops(Player player, int amount);
        void RemoveTroops(Player player, int amount);

        void AddSpies(Player player, int amount);
        void RemoveSpies(Player player, int amount);

        void AddTrophy(Player player);

        // --- Card Management ---
        void DrawCards(Player player, int count, IGameRandom random);
        void PlayCard(Player player, Card card); // Moves from Hand to Played
        void AcquireCard(Player player, Card card); // Moves to Discard (bought/gained)
        bool TryPromoteCard(Player player, Card card, out string errorMessage); // Inner Circle
        void DevourCard(Player player, Card card); // Removes from deck/hand entirely

        // --- Turn Management ---
        void CleanUpTurn(Player player);
    }
}
