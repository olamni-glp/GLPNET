// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// GrammarFuzzer (feature 069, T017) — deterministic bounded generator of VALID GLP programs for the
// IL-parity fuzz (contract corpus-and-fuzz-contract.md F1/F3/F5, research D6).
//
// Targets the two ALL(*)-prediction-sensitive corners flagged in REPORT §7:
//   Corner 1 — variable-versus-comparison dispatch: guard clauses mixing `arith cmpOp arith` with the
//     leading-variable `V? = term` form and the guard-only comparison operators (=?= @< @> @=< @>=).
//     This is the IL-parity probe: both front-ends must lower the guard to the identical Goal AST.
//   Corner 2 — deep type-alternative nesting: a typeDef whose alternatives nest structures/lists to a
//     PRNG-chosen depth. Type defs do NOT reach codegen (only proc-decl top-level type names do), so
//     this is the PARSE-ACCEPTANCE-parity probe (comparator P4), not an IL probe.
//
// Determinism (F3): the program for a given (index, seed) is a pure function of those two numbers —
// no Math.random, no wall-clock. Same index+seed => byte-identical source => a divergence is replayable.
//
// Validity (F5): generated programs stay inside constructs BOTH front-ends accept, so the fuzz probes
// IL/parse parity, not parser error-recovery:
//   - all params are the constant type `Number?` so reader reuse in guards is SRSW-legal (manual §3);
//   - operators are ALWAYS space-separated, which deliberately excludes the glued-minus lexer corner
//     (`5 -3` => NUMBER NUMBER) — a documented lexer behaviour, not a bug, so out of scope here;
//   - only INFIX `mod` is generated; the `mod(...)` call form is the §1.14-gated T016 case and is
//     intentionally NOT emitted by this fuzzer;
//   - `=` guards are generated NON-CYCLIC (feature 069, DEC F3): `=` is a compile-time DEFINED guard
//     (manual §8) reduced by substitution, and a self-referential unification (`A? = B? * A?`) binds a
//     variable to a term containing itself. The production DefinedGuardEvaluator has no occurs-check
//     and stack-overflows on it (F-069-1, FINDINGS.md) — a shared-pipeline defect, not a parity
//     divergence. A cyclic `=` guard never yields IL, so it is outside this parity fuzz's scope; the
//     defect is filed as its own engine bug and recorded as a bounded condition in DECISION.md.

using System.Text;

namespace GlpGrammarSpike.Parity
{
    public sealed class GeneratedInput
    {
        public int Index;
        public string Id;
        public string Source;
    }

    public static class GrammarFuzzer
    {
        public const uint DefaultSeed = 0x9E3779B9u;

        // Guard-region comparison operators (Glp.g4 cmpOp). The last five are reachable ONLY in guard
        // position (REPORT §7) — the core of the variable-vs-comparison dispatch corner.
        private static readonly string[] Cmp =
            { "<", ">", "=<", ">=", "=", "=:=", "=\\=", "=?=", "@<", "@>", "@=<", "@>=" };

        // Pratt-table binary operators for arithmetic operands (infix mod only — not the mod(...) form).
        private static readonly string[] BinOp = { "+", "-", "*", "/", "//", "mod" };

        public static GeneratedInput Generate(int index, uint seed = DefaultSeed)
        {
            var rng = new Rng(Mix(seed, unchecked((uint)index)));
            var sb = new StringBuilder();
            string sfx = index.ToString();

            sb.Append("% fuzz#").Append(index).Append(" seed=").Append(seed)
              .Append(" — deterministic (source = f(index, seed)); regenerate to reproduce\n");

            // ── Corner 2: deep type-alternative nesting (distinct top-level functors per union rule) ──
            int depth = 1 + rng.Next(6); // 1..6
            sb.Append("Nest").Append(sfx).Append("(A) ::= leaf")
              .Append(" ; ").Append(Wrap(depth))
              .Append(" ; pair(A, A)")
              .Append(" ; [A | Nest").Append(sfx).Append("(A)]")
              .Append(".\n");

            // ── Corner 1: variable-vs-comparison dispatch (IL parity) ──
            // Head writers A, B each need >=1 reader (SRSW); the guard guarantees A? on the left operand
            // and B? on the right. Number is a constant type, so extra reader occurrences are SRSW-legal
            // (manual §3). Unused-in-a-clause writers use `_` (the otherwise clause).
            sb.Append("procedure p").Append(sfx).Append("(Number?, Number?, Constant).\n");
            int clauses = 1 + rng.Next(3); // 1..3 yes-clauses
            for (int c = 0; c < clauses; c++)
                sb.Append("p").Append(sfx).Append("(A, B, yes) :- ").Append(Guard(ref rng)).Append(" | true.\n");
            sb.Append("p").Append(sfx).Append("(_, _, no) :- otherwise | true.\n");

            return new GeneratedInput { Index = index, Id = "fuzz#" + index, Source = sb.ToString() };
        }

        // wrap(wrap(...(A)...)) nested `depth` deep.
        private static string Wrap(int depth)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < depth; i++) sb.Append("wrap(");
            sb.Append('A');
            for (int i = 0; i < depth; i++) sb.Append(')');
            return sb.ToString();
        }

        // A guard, guaranteeing A? appears on the left and B? on the right (SRSW: each head writer needs
        // a reader). 1/4 the leading-variable dispatch form `A? = B?`; else `<A?-expr> cmpOp <B?-expr>`.
        private static string Guard(ref Rng rng)
        {
            if (rng.Next(4) == 0)
                return rng.Bool() ? "A? = B?" : "B? = A?";
            string op = Cmp[rng.Next(Cmp.Length)];
            // F2 (feature 069, DEC F3): scope `=` to NON-CYCLIC unification. `=` is a defined guard
            // reduced by substitution with no occurs-check in production (F-069-1). The overflow needs
            // the SAME variable on both sides, so for `=` we draw the two operands from DISJOINT
            // variable sets (left: A? only, right: B? only) — no shared variable, no cycle. Every other
            // cmpOp does no term substitution, so its operands stay unrestricted (A?/B? on either side).
            bool unify = op == "=";
            string left  = ExprFrom(ref rng, "A?", unify ? "A?" : null);
            string right = ExprFrom(ref rng, "B?", unify ? "B?" : null);
            return left + " " + op + " " + right;
        }

        // Arithmetic operand whose LEFT edge is `lead`, then 0..2 random `binop leaf` tails. Always
        // space-separated. Leaves are A?/B?/small-int; Number's constant-type relaxation makes any reader
        // repetition SRSW-legal, so the tails are unconstrained. When `onlyVar` is non-null (the
        // non-cyclic `=` case, F2), leaf head-variables are restricted to it so the two sides of a `=`
        // share no variable.
        private static string ExprFrom(ref Rng rng, string lead, string onlyVar)
        {
            var sb = new StringBuilder(lead);
            int extra = rng.Next(3); // 0..2
            for (int k = 0; k < extra; k++)
                sb.Append(' ').Append(BinOp[rng.Next(BinOp.Length)]).Append(' ').Append(Leaf(ref rng, onlyVar));
            return sb.ToString();
        }

        private static string Leaf(ref Rng rng, string onlyVar)
        {
            if (onlyVar != null)
                return rng.Bool() ? onlyVar : rng.Next(50).ToString();
            int p = rng.Next(3);
            return p == 0 ? "A?" : p == 1 ? "B?" : rng.Next(50).ToString();
        }

        // Splitmix-style avalanche so adjacent indices give well-separated streams; never returns 0.
        private static uint Mix(uint seed, uint index)
        {
            uint h = unchecked(seed ^ (index + 0x9E3779B9u + (seed << 6) + (seed >> 2)));
            unchecked
            {
                h ^= h >> 16; h *= 0x7FEB352Du;
                h ^= h >> 15; h *= 0x846CA68Bu;
                h ^= h >> 16;
            }
            return h == 0 ? 1u : h;
        }

        // Deterministic xorshift32 (self-contained; independent of any runtime RNG implementation).
        private struct Rng
        {
            private uint _s;
            public Rng(uint seed) { _s = seed == 0 ? 0xDEADBEEFu : seed; }
            public uint NextU() { unchecked { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; } return _s; }
            public int Next(int n) { return (int)(NextU() % (uint)n); } // n > 0
            public bool Bool() { return (NextU() & 1u) == 0u; }
        }
    }
}
