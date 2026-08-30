using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules
{
    /// <summary>
    /// Pure logic engine responsible for determining the sequence of Targeting States
    /// based on a Card's Effect Tree.
    /// Extracts complex recursion and state transition logic from ActionSystem to adhere to SRP.
    /// </summary>
    /// <summary>
    /// Pure logic engine responsible for determining the sequence of Targeting States
    /// based on a Card's Effect Tree.
    /// Extracts complex recursion and state transition logic from ActionSystem to adhere to SRP.
    /// </summary>
    public static class TargetingStateEngine
    {
        /// <summary>
        /// Determines the Next targeting state required after the Current state.
        /// Traverses the effect tree to find the Current state, then looks for the next targeting requirement.
        /// </summary>
        /// <param name="effects">The root effects of the card.</param>
        /// <param name="currentState">The state we are currently in (or just finished).</param>
        /// <param name="isCurrentStateSkipped">If true, the current state's children (OnSuccess) will be skipped.</param>
        /// <param name="ruleEngine">The CardRuleEngine to resolve strategies.</param>
        /// <returns>The next ActionState, or ActionState.Normal if no more targeting is needed.</returns>
        public static ActionState DetermineNextState(IEnumerable<CardEffect> effects, ActionState currentState, bool isCurrentStateSkipped, CardRuleEngine ruleEngine)
        {
            ArgumentNullException.ThrowIfNull(ruleEngine);

            // If we are starting fresh (Normal), just find the first targeting state
            if (currentState == ActionState.Normal)
            {
                // Find first targeting state in the tree
                foreach (var effect in effects)
                {
                    var state = FindTargetingStateRecursive(effect, ruleEngine);
                    if (state != ActionState.Normal) return state;
                }
                return ActionState.Normal;
            }

            bool foundCurrent = false;
            return TraverseForNext(effects, currentState, isCurrentStateSkipped, ref foundCurrent, ruleEngine);
        }

        private static ActionState TraverseForNext(IEnumerable<CardEffect> effects, ActionState currentState, bool isCurrentStateSkipped, ref bool foundCurrent, CardRuleEngine ruleEngine)
        {
            if (effects == null) return ActionState.Normal;

            foreach (var effect in effects)
            {
                var effectState = ruleEngine.GetStrategy(effect.Type).GetTargetingState(effect);

                if (!foundCurrent)
                {
                    // Still searching for the current state
                    var result = SearchForCurrentState(effect, effectState, currentState, isCurrentStateSkipped, ref foundCurrent, ruleEngine);
                    if (result != ActionState.Normal) return result;
                }
                else
                {
                    // Already found current state, looking for next targeting state
                    var result = FindNextTargetingState(effect, effectState, ruleEngine);
                    if (result != ActionState.Normal) return result;
                }
            }

            return ActionState.Normal;
        }

        /// <summary>
        /// Searches for the current state in the effect tree.
        /// Returns the next state if found and processed, otherwise Normal.
        /// </summary>
        private static ActionState SearchForCurrentState(
            CardEffect effect,
            ActionState effectState,
            ActionState currentState,
            bool isCurrentStateSkipped,
            ref bool foundCurrent,
            CardRuleEngine ruleEngine)
        {
            // Case 1: This effect IS the current state
            if (effectState == currentState)
            {
                foundCurrent = true;
                return ProcessFoundCurrentState(effect, isCurrentStateSkipped, ruleEngine);
            }

            // Case 2: Current state might be in children
            if (effect.OnSuccess != null)
            {
                return SearchInChildTree(effect, currentState, isCurrentStateSkipped, ref foundCurrent, ruleEngine);
            }

            return ActionState.Normal;
        }

        /// <summary>
        /// Processes the effect after finding the current state.
        /// Looks for the next targeting state in children (if not skipped).
        /// </summary>
        private static ActionState ProcessFoundCurrentState(CardEffect effect, bool isCurrentStateSkipped, CardRuleEngine ruleEngine)
        {
            // Priority 1: Children (Dependency), unless skipped
            if (!isCurrentStateSkipped && effect.OnSuccess != null)
            {
                var childState = FindTargetingStateRecursive(effect.OnSuccess, ruleEngine);
                if (childState != ActionState.Normal) return childState;
            }

            // Priority 2: Next Sibling (handled by continuing the loop)
            return ActionState.Normal;
        }

        /// <summary>
        /// Searches for the current state in the child tree.
        /// If found, returns the next state from the child tree or continues to siblings.
        /// </summary>
        private static ActionState SearchInChildTree(
            CardEffect effect,
            ActionState currentState,
            bool isCurrentStateSkipped,
            ref bool foundCurrent,
            CardRuleEngine ruleEngine)
        {
            var nextInChild = TraverseForNext(new[] { effect.OnSuccess! }, currentState, isCurrentStateSkipped, ref foundCurrent, ruleEngine);

            // If we found 'Current' deep in the child tree...
            if (foundCurrent)
            {
                // ...and the child tree gave us a Next state, return it
                if (nextInChild != ActionState.Normal) return nextInChild;

                // If child tree finished (returned Normal), continue to Next Sibling
            }

            return ActionState.Normal;
        }

        /// <summary>
        /// Finds the next targeting state after we've already passed the current state.
        /// Looks for ANY valid targeting state in this subtree.
        /// </summary>
        private static ActionState FindNextTargetingState(CardEffect effect, ActionState effectState, CardRuleEngine ruleEngine)
        {
            // Check if this effect itself is a targeting effect
            if (ruleEngine.GetStrategy(effect.Type).IsTargetingEffect)
            {
                return effectState;
            }

            // Check children for targeting effects
            if (effect.OnSuccess != null)
            {
                var childState = FindTargetingStateRecursive(effect.OnSuccess, ruleEngine);
                if (childState != ActionState.Normal) return childState;
            }

            return ActionState.Normal;
        }

        /// <summary>
        /// Finds the first targeting state in an effect subtree.
        /// </summary>
        private static ActionState FindTargetingStateRecursive(CardEffect? effect, CardRuleEngine ruleEngine)
        {
            if (effect == null) return ActionState.Normal;

            var strategy = ruleEngine.GetStrategy(effect.Type);
            if (strategy.IsTargetingEffect)
            {
                return strategy.GetTargetingState(effect);
            }

            if (effect.OnSuccess != null)
            {
                return FindTargetingStateRecursive(effect.OnSuccess, ruleEngine);
            }

            return ActionState.Normal;
        }
    }
}
