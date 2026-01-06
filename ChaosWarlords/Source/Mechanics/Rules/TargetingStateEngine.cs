using System.Collections.Generic;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Rules
{
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
        /// <returns>The next ActionState, or ActionState.Normal if no more targeting is needed.</returns>
        public static ActionState DetermineNextState(IEnumerable<CardEffect> effects, ActionState currentState, bool isCurrentStateSkipped)
        {
            // If we are starting fresh (Normal), just find the first targeting state
            if (currentState == ActionState.Normal)
            {
                // Find first targeting state in the tree
                foreach (var effect in effects)
                {
                    var state = FindTargetingStateRecursive(effect);
                    if (state != ActionState.Normal) return state;
                }
                return ActionState.Normal;
            }

            bool foundCurrent = false;
            return TraverseForNext(effects, currentState, isCurrentStateSkipped, ref foundCurrent);
        }

        private static ActionState TraverseForNext(IEnumerable<CardEffect> effects, ActionState currentState, bool isCurrentStateSkipped, ref bool foundCurrent)
        {
            if (effects == null) return ActionState.Normal;

            foreach (var effect in effects)
            {
                var effectState = CardPlaySystem.GetTargetingState(effect);

                if (!foundCurrent)
                {
                    // Case 1: Searching for the Current State
                    if (effectState == currentState)
                    {
                        foundCurrent = true;

                        // Found it! Now look for the NEXT step.
                        // Priority 1: Children (Dependency), unless skipped.
                        if (!isCurrentStateSkipped && effect.OnSuccess != null)
                        {
                            var childState = FindTargetingStateRecursive(effect.OnSuccess);
                            if (childState != ActionState.Normal) return childState;
                        }
                        
                        // Priority 2: Next Sibling (Continue loop)
                        continue;
                    }

                    // Case 2: Current state not found here, check children.
                    if (effect.OnSuccess != null)
                    {
                        var nextInChild = TraverseForNext(new[] { effect.OnSuccess }, currentState, isCurrentStateSkipped, ref foundCurrent);
                        
                        // If we found 'Current' deep in the child tree...
                        if (foundCurrent)
                        {
                            // ...and the child tree gave us a Next state, return it.
                            if (nextInChild != ActionState.Normal) return nextInChild;
                            
                            // If child tree finished (returned Normal), we continue to Next Sibling.
                            continue;
                        }
                    }
                }
                else
                {
                    // Case 3: We have already passed the Current State.
                    // We are now looking for ANY valid targeting state in this subtree (Candidates for "Next")
                    
                    if (CardPlaySystem.IsTargetingEffect(effect.Type))
                    {
                        return effectState;
                    }

                    if (effect.OnSuccess != null)
                    {
                        var childState = FindTargetingStateRecursive(effect.OnSuccess);
                        if (childState != ActionState.Normal) return childState;
                    }
                }
            }

            return ActionState.Normal;
        }

        /// <summary>
        /// Finds the first targeting state in an effect subtree.
        /// </summary>
        private static ActionState FindTargetingStateRecursive(CardEffect? effect)
        {
            if (effect == null) return ActionState.Normal;

            if (CardPlaySystem.IsTargetingEffect(effect.Type))
            {
                return CardPlaySystem.GetTargetingState(effect);
            }

            if (effect.OnSuccess != null)
            {
                return FindTargetingStateRecursive(effect.OnSuccess);
            }

            return ActionState.Normal;
        }
    }
}
