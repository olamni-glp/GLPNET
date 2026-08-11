// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Unit tests for the parity comparator's first-diff localization and the grammar fuzzer's
// determinism + the DEC F3 non-cyclic-`=` generation invariant (feature 069, T021). All pure —
// no engine pipeline, no filesystem.

using System;
using Xunit;
using GlpGrammarSpike.Parity;

namespace GlpGrammarSpike.Parity.Tests
{
    public class FirstDiffTests
    {
        [Fact]
        public void Identical_arrays_return_minus_one()
        {
            Assert.Equal(-1, IlParityComparator.FirstDiff(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }));
        }

        [Fact]
        public void Empty_arrays_return_minus_one()
        {
            Assert.Equal(-1, IlParityComparator.FirstDiff(new byte[0], new byte[0]));
        }

        [Fact]
        public void Difference_at_offset_zero_localizes_to_zero()
        {
            Assert.Equal(0, IlParityComparator.FirstDiff(new byte[] { 9, 2, 3 }, new byte[] { 1, 2, 3 }));
        }

        [Fact]
        public void Difference_in_the_middle_localizes_to_that_offset()
        {
            Assert.Equal(2, IlParityComparator.FirstDiff(new byte[] { 1, 2, 3, 4 }, new byte[] { 1, 2, 9, 4 }));
        }

        [Fact]
        public void Prefix_shorter_localizes_to_shorter_length()
        {
            // a is a strict prefix of b -> first diff at a.Length.
            Assert.Equal(3, IlParityComparator.FirstDiff(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3, 4 }));
            // symmetric: b is the shorter prefix of a.
            Assert.Equal(3, IlParityComparator.FirstDiff(new byte[] { 1, 2, 3, 4 }, new byte[] { 1, 2, 3 }));
        }
    }

    public class GrammarFuzzerDeterminismTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(23)]   // the historic F-069-1 index
        [InlineData(9999)]
        public void Same_index_and_seed_produce_byte_identical_source(int index)
        {
            var a = GrammarFuzzer.Generate(index);
            var b = GrammarFuzzer.Generate(index);
            Assert.Equal(a.Source, b.Source);
            Assert.Equal(index, a.Index);
            Assert.Equal("fuzz#" + index, a.Id);
        }

        [Fact]
        public void Different_seed_changes_the_source_for_a_fixed_index()
        {
            var a = GrammarFuzzer.Generate(7, GrammarFuzzer.DefaultSeed);
            var b = GrammarFuzzer.Generate(7, GrammarFuzzer.DefaultSeed ^ 0x1u);
            Assert.NotEqual(a.Source, b.Source);
        }

        [Fact]
        public void The_generated_stream_is_not_constant()
        {
            // Determinism must not collapse to one program: adjacent indices vary.
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 50; i++) seen.Add(GrammarFuzzer.Generate(i).Source);
            Assert.True(seen.Count > 40, "expected varied generation, got " + seen.Count + " distinct of 50");
        }
    }

    public class NonCyclicUnificationInvariantTests
    {
        // DEC F3 / F-069-1 regression guard: no generated `=` (unification) guard may place the same
        // variable on both sides — that is exactly the cyclic case that overflows the production
        // DefinedGuardEvaluator (no occurs-check). Other cmpOps do no substitution and are exempt.
        [Fact]
        public void No_generated_equals_guard_shares_a_variable_across_its_two_sides()
        {
            for (int i = 0; i < 3000; i++)
            {
                string src = GrammarFuzzer.Generate(i).Source;
                foreach (var line in src.Split('\n'))
                {
                    int lo = line.IndexOf(":- ", StringComparison.Ordinal);
                    int hi = line.IndexOf(" | true.", StringComparison.Ordinal);
                    if (lo < 0 || hi < 0 || hi <= lo) continue;
                    string guard = line.Substring(lo + 3, hi - (lo + 3));

                    // " = " (space-equals-space) matches ONLY the bare unification operator; the
                    // multi-char cmpOps (=:= =\= =?= =< >=) never contain that exact substring.
                    int eq = guard.IndexOf(" = ", StringComparison.Ordinal);
                    if (eq < 0) continue;
                    string lhs = guard.Substring(0, eq);
                    string rhs = guard.Substring(eq + 3);

                    bool aBoth = lhs.Contains("A?") && rhs.Contains("A?");
                    bool bBoth = lhs.Contains("B?") && rhs.Contains("B?");
                    Assert.False(aBoth || bBoth,
                        "cyclic `=` guard generated at index " + i + ": '" + guard + "'");
                }
            }
        }
    }
}
