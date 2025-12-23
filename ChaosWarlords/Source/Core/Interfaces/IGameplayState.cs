using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.States
{
    public interface IGameplayState : IState
    {
        // --- Managers ---
        InputManager InputManager { get; }
        IUISystem UIManager { get; }
        IMapManager MapManager { get; }
        IMarketManager MarketManager { get; }
        IActionSystem ActionSystem { get; }
        ITurnManager TurnManager { get; }
        MatchContext MatchContext { get; }
        IMatchController MatchController { get; }

        // --- State Properties ---
        // Removed 'set' because this is now managed by the InputCoordinator
        IInputMode InputMode { get; }
        bool IsMarketOpen { get; set; }

        // --- Layout ---
        int HandY { get; }
        int PlayedY { get; }

        // --- Flow Control ---
        bool CanEndTurn(out string reason);
        void EndTurn();
        void ToggleMarket();
        void CloseMarket();

        void SwitchToTargetingMode();
        void SwitchToNormalMode();
        void SwitchToPromoteMode(int amount);

        // --- Gameplay Actions ---
        void PlayCard(Card card);
        // REMOVED: void ResolveCardEffects(Card card); -> Logic moved to CardEffectProcessor
        void MoveCardToPlayed(Card card);

        // --- Query Methods ---
        bool HasViableTargets(Card card);
        string GetTargetingText(ActionState state);

        // --- Interaction Helpers ---
        // These delegates are required for InputModes to interact with the View
        Card GetHoveredHandCard();
        Card GetHoveredPlayedCard();
        Card GetHoveredMarketCard();
    }
}