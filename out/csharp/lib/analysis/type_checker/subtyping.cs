// lib/analysis/type_checker/subtyping.cs
//
// Subtyping for GLP output types.
// Specification: docs/type system/subtyping.md
// Paper Reference: Section 4.6, Definition 4.7 (Subtyping)
//
// A <: B iff every simple prefix of A is accepted by B,
// and at mode inversion points the direction reverses (contravariance).

using System.Collections.Generic;
using System.Diagnostics;

namespace GlpRuntime.Analysis.TypeChecker
{
    public static class Subtyping
    {
        /// <summary>
        /// Check if output type A is a subtype of output type B.
        ///
        /// Both stateA and stateB must be output types (IsDual == false).
        /// Uses coinductive algorithm with visited set for cycle detection.
        ///
        /// Paper Reference: Definition 4.7 (Subtyping)
        /// </summary>
        public static bool IsSubtype(DFAState stateA, DFAState stateB, ProgramDFA dfa)
        {
            return CheckSubtype(stateA, stateB, dfa, new HashSet<string>(StringComparer.Ordinal));
        }

        /// <summary>
        /// Core coinductive subtyping algorithm.
        ///
        /// Spec section 4.1: isSubtype(stateA, stateB, dfa, visited)
        /// </summary>
        private static bool CheckSubtype(DFAState stateA, DFAState stateB, ProgramDFA dfa, HashSet<string> visited)
        {
            // Coinductive: if we've already assumed this pair, succeed (spec 4.5)
            string pairKey = $"{stateA.Name}:{stateB.Name}";
            if (visited.Contains(pairKey)) return true;
            visited.Add(pairKey);

            // Reflexivity
            if (stateA.Equals(stateB)) return true;

            // Both must be output types (not dual)
            Debug.Assert(!stateA.IsDual && !stateB.IsDual);

            // Wildcard/final handling (spec 4.4)
            // _FINAL_ is treated as equivalent to _ for subtyping purposes
            if (stateB.IsWildcard || stateB.IsAnonymousFinal) return true;
            if (stateA.IsWildcard || stateA.IsAnonymousFinal) return false;

            // Primitive type lattice (spec 4.3)
            if (stateA.IsPrimitiveType || stateB.IsPrimitiveType)
                return CheckPrimitiveSubtype(stateA, stateB);

            // User-defined types: check transitions (spec 4.1)
            Automaton automA = dfa.LookupAutomaton(stateA.Name);
            Automaton automB = dfa.LookupAutomaton(stateB.Name);

            // Every transition from A must have a matching transition in B
            foreach (var kvp in automA.Transitions)
            {
                var (fromState, label) = kvp.Key;
                // Only check transitions from the start state of A
                if (!fromState.Equals(stateA)) continue;

                DFAState targetA = kvp.Value;
                DFAState? targetB = automB.Transition(stateB, label);

                // A has a transition B lacks → not a subtype
                if (targetB is null) return false;

                // Skip trivially equal targets
                if (targetA.Equals(targetB)) continue;

                // Check target compatibility (spec 4.2)
                if (!CheckTargetSubtype(targetA, targetB, dfa, visited)) return false;
            }

            return true;
        }

        /// <summary>
        /// Target compatibility check (spec section 4.2).
        ///
        /// Handles covariance for output positions and contravariance at mode inversions.
        /// </summary>
        private static bool CheckTargetSubtype(DFAState targetA, DFAState targetB, ProgramDFA dfa, HashSet<string> visited)
        {
            // Case 1: Both output types → covariant recursion
            if (!targetA.IsDual && !targetB.IsDual)
                return CheckSubtype(targetA, targetB, dfa, visited);

            // Case 2: Both dual types → contravariant recursion (reversed)
            if (targetA.IsDual && targetB.IsDual)
            {
                DFAState innerA = dfa.LookupState(targetA.BaseName); // output type A'
                DFAState innerB = dfa.LookupState(targetB.BaseName); // output type B'
                return CheckSubtype(innerB, innerA, dfa, visited); // REVERSED
            }

            // Case 3: Mixed → incompatible mode structure
            return false;
        }

        /// <summary>
        /// Primitive type subtyping lattice (spec section 4.3).
        ///
        /// Integer &lt;: Number, Real &lt;: Number.
        /// Otherwise, primitive types must be identical.
        /// </summary>
        private static bool CheckPrimitiveSubtype(DFAState stateA, DFAState stateB)
        {
            // _ is top for output types (handled in caller, but be safe)
            if (stateB.IsWildcard) return true;
            if (stateA.IsWildcard) return false;

            // Integer <: Number
            if (stateA.IsIntegerType && stateB.IsNumberType) return true;

            // Real <: Number
            if (stateA.IsRealType && stateB.IsNumberType) return true;

            // Otherwise must be identical
            return string.Equals(stateA.BaseName, stateB.BaseName, StringComparison.Ordinal);
        }
    }
}
