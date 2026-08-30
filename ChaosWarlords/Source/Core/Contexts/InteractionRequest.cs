using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Rules;

namespace ChaosWarlords.Source.Core.Contexts
{
    /// <summary>
    /// Represents a request from the Logic Layer (ActionSystem) for User Interaction.
    /// This decouples the Logic from the UI.
    /// </summary>
    public class InteractionRequest
    {
        public EffectContext Context { get; }
        public Card SourceCard { get; }
        public CardEffect SourceEffect { get; }
        
        // Callback to resume logic with the result
        // True = Accepted/Success, False = Declined/Fail
        public Action<bool> OnResponse { get; }

        public InteractionRequest(EffectContext context, Action<bool> onResponse)
        {
            Context = context;
            SourceCard = context.SourceCard!;
            SourceEffect = context.SourceEffect!;
            OnResponse = onResponse;
        }
    }
}
