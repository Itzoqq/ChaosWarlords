using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities.Cards
{
    /// <summary>
    /// Represents a card in the game, including its stats, cost, and effects.
    /// Cards can flyweight-like definitions or instantiated objects in a player's deck.
    /// </summary>
    public class Card
    {
        // --- Core Data Only ---

        /// <summary>
        /// Unique identifier for this specific instance of the card.
        /// Essential for distinguishing between identical cards (e.g. two "Drow Soldier" cards).
        /// Settable internally only so StateRestorer can carry the original value across a
        /// rollback-rebuilt Card (a fresh instance) - see CardDto.RuntimeId.
        /// </summary>
        public Guid RuntimeId { get; internal set; } = Guid.NewGuid();

        /// <summary>
        /// Unique identifier for the card definition (e.g. "c_obsidian_golem").
        /// NOTE: despite the name, this is NOT the catalog/lookup key by itself once created via
        /// CardFactory - CardFactory.GenerateUniqueId appends a per-instance suffix to it (so
        /// two cards from the same cards.json entry get distinct Ids), which is exactly why
        /// DefinitionId exists as a separate field below. Kept as-is (rather than renamed) to
        /// avoid touching the many call sites/tests that already read Card.Id.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// The plain, un-suffixed catalog key this card was created from (e.g. "wight",
        /// "soldier") - what ICardDatabase.GetCardById actually looks up by. Defaults to Id
        /// when not given explicitly (the common case for hand-built/test cards, where Id is
        /// already the plain key and there's no separate suffixed runtime Id to distinguish).
        /// CardFactory is the only production code that ever passes a value that differs from
        /// Id - see its CreateFromData/CreateSoldier/CreateNoble.
        /// </summary>
        public string DefinitionId { get; private set; }

        /// <summary>
        /// The localized name of the card.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The influence cost required to purchase this card from the market.
        /// </summary>
        public int Cost { get; private set; }

        /// <summary>
        /// The elemental or factional aspect of the card (e.g. Shadow, Undead).
        /// </summary>
        public CardAspect Aspect { get; private set; }

        /// <summary>
        /// Victory Points worth when in the deck at end of game.
        /// </summary>
        public int DeckVP { get; private set; }

        /// <summary>
        /// Victory Points worth when promoted to the Inner Circle.
        /// </summary>
        public int InnerCircleVP { get; private set; }

        /// <summary>
        /// Influence generated when this card is played.
        /// </summary>
        public int InfluenceValue { get; private set; }

        /// <summary>
        /// List of special effects triggered when the card is played.
        /// </summary>
        public List<CardEffect> Effects { get; private set; } = [];

        /// <summary>
        /// User-visible description of what the card does.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Current location of this card instance (Deck, Hand, Discard, etc.).
        /// </summary>
        public CardLocation Location { get; set; }

        /// <summary>
        /// If true, this card is never actually devoured or promoted - any attempt instead
        /// redirects it to CardLocation.Supply (e.g. Insane Outcast: "If Insane Outcast would
        /// be devoured or promoted, return it to the supply instead"). Checked by
        /// PlayerStateManager.DevourCard/Player.TryPromoteCard and DevourSelfStrategy.
        /// </summary>
        public bool RedirectsToSupplyOnDevourOrPromote { get; set; }

        // Constants moved to GameConstants.CardRendering for centralization
        public static int Width => GameConstants.CardRendering.CardWidth;
        public static int Height => GameConstants.CardRendering.CardHeight;

        public Card(string id, string name, int cost, CardAspect aspect, int deckVp, int innerCircleVp, int influence, string? definitionId = null)
        {
            Id = id;
            DefinitionId = definitionId ?? id;
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
            var newCard = new Card(Id, Name, Cost, Aspect, DeckVP, InnerCircleVP, InfluenceValue, DefinitionId)
            {
                Description = Description,
                Location = Location,
                RedirectsToSupplyOnDevourOrPromote = RedirectsToSupplyOnDevourOrPromote,
                // Preserved rather than left auto-generated: Clone() is used to hydrate a DTO
                // snapshot back into a live Card (CardDto.ToEntity), where the clone represents
                // the SAME logical instance materializing elsewhere, not a second copy
                // coexisting alongside the original - see StateRestorer for the equivalent
                // restore path that also carries RuntimeId across.
                RuntimeId = RuntimeId
            };

            foreach (var effect in Effects)
            {
                newCard.Effects.Add(CloneEffect(effect));
            }

            return newCard;
        }

        // Fully recursive - both OnSuccess ("and then") and Alternative ("instead, if
        // declined/impossible") chains can nest arbitrarily deep (e.g. Cloaker:
        // PlaceSpy.Alternative = ReturnOwnSpy.OnSuccess = Assassinate).
        private static CardEffect CloneEffect(CardEffect effect)
        {
            var newEffect = new CardEffect(effect.Type, effect.Amount, effect.TargetResource)
            {
                RequiresFocus = effect.RequiresFocus,
                IsOptional = effect.IsOptional,
                TargetLocation = effect.TargetLocation,
                ReplaceWithSource = effect.ReplaceWithSource,
                TargetNeutralTroopOnly = effect.TargetNeutralTroopOnly,
                IgnoresPresenceRequirement = effect.IgnoresPresenceRequirement,
                DynamicAmountSource = effect.DynamicAmountSource,
                DynamicAmountDivisor = effect.DynamicAmountDivisor,
                Condition = effect.Condition // Reference copy for condition (usually shared/immutable)
            };

            if (effect.OnSuccess != null)
            {
                newEffect.OnSuccess = CloneEffect(effect.OnSuccess);
            }

            if (effect.Alternative != null)
            {
                newEffect.Alternative = CloneEffect(effect.Alternative);
            }

            return newEffect;
        }
    }
}

