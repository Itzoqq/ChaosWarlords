using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities.Cards
{
    public class Card
    {
        // --- Core Data Only ---
        public string Id { get; private set; }
        public string Name { get; private set; }
        public int Cost { get; private set; }
        public CardAspect Aspect { get; private set; }

        public int DeckVP { get; private set; }
        public int InnerCircleVP { get; private set; }
        public int InfluenceValue { get; private set; }

        public List<CardEffect> Effects { get; private set; } = new List<CardEffect>();
        public string Description { get; set; } = "";
        public CardLocation Location { get; set; }

        // Constants can remain here if they define the "physical standard" of a card in your world,
        // otherwise move them to LayoutConsts.cs
        public const int Width = 150;
        public const int Height = 200;

        public Card(string id, string name, int cost, CardAspect aspect, int deckVp, int innerCircleVp, int influence)
        {
            Id = id;
            Name = name;
            Cost = cost;
            Aspect = aspect;
            DeckVP = deckVp;
            InnerCircleVP = innerCircleVp;
            InfluenceValue = influence;
        }

        public void AddEffect(CardEffect effect)
        {
            Effects.Add(effect);
        }

        public Card Clone()
        {
            var newCard = new Card(Id, Name, Cost, Aspect, DeckVP, InnerCircleVP, InfluenceValue)
            {
                Description = Description,
                Location = Location
                // REMOVED: Position = Position, IsHovered = IsHovered
            };

            foreach (var effect in Effects)
            {
                newCard.Effects.Add(new CardEffect(effect.Type, effect.Amount, effect.TargetResource));
            }

            return newCard;
        }
    }
}

