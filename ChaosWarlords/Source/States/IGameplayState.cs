using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.States
{
    public interface IGameplayState : IState
    {
        InputManager InputManager { get; }
        IUISystem UIManager { get; }
        IMapManager MapManager { get; }
        IMarketManager MarketManager { get; }
        IActionSystem ActionSystem { get; }
        TurnManager TurnManager { get; }

        IInputMode InputMode { get; set; }
        bool IsMarketOpen { get; set; }

        int HandY { get; }
        int PlayedY { get; }

        void EndTurn();
        void ToggleMarket();
        void CloseMarket();
        void SwitchToTargetingMode();
        void SwitchToNormalMode();

        void PlayCard(Card card);
        void ResolveCardEffects(Card card);
        void MoveCardToPlayed(Card card);

        // Removed ArrangeHandVisuals from Interface (Internal detail now)

        string GetTargetingText(ActionState state);

        // --- NEW METHODS FOR INPUT MODES ---
        Card GetHoveredHandCard();
        Card GetHoveredMarketCard();
    }
}