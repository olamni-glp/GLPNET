// Shared result corpus — feature 038-result-codec-and-framecodec-ride (C# slice).
//
// Mirrors the SHAPES in glp_runtime/test/codec/corpus.dart entry-for-entry (the
// non-gated M1 corpus authored from the Dart reference, R9), plus a small GATED
// section (floats / 64-bit-int edges) exercised for round-trip only (NOT asserted
// byte-final — R11/R6). These same values feed the round-trip, no-heap, loud-fail,
// and golden byte-vector tests.

using System.Collections.Generic;
using System.Text;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.ResultCodec.Tests;

public static class Corpus
{
    // --- tiny term builders for readable corpus entries (mirror corpus.dart) ---
    private static Term Atom(string a) => new ConstTerm(new ConstAtom(a));
    private static Term Int(long n) => new ConstTerm(new ConstInt(n));
    private static Term Real(double d) => new ConstTerm(new ConstReal(d));
    private static Term Str(string s) => new ConstTerm(new ConstString(s));
    private static Term Struct(string f, params Term[] args) => new StructTerm(f, args);
    private static Term Var(string agent, long local) => new VarRef(new GlobalVarId(agent, local));

    /// <summary>A GLP list as cons/nil cells: [a,b,c] = .(a, .(b, .(c, nil))).</summary>
    private static Term GlpList(params Term[] elems)
    {
        Term acc = Atom("nil");
        for (int i = elems.Length - 1; i >= 0; i--)
            acc = Struct(".", elems[i], acc);
        return acc;
    }

    private static KeyValuePair<string, Term> B(string name, Term t) => new(name, t);
    private static KeyValuePair<string, GlobalVarId> V(string name, GlobalVarId id) => new(name, id);
    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    /// <summary>The non-gated in-scope corpus: name -> envelope (mirrors nonGatedCorpus).</summary>
    public static readonly IReadOnlyDictionary<string, ResultEnvelope> NonGated =
        new Dictionary<string, ResultEnvelope>
        {
            // --- trivial ---
            ["empty_success"] = new ResultEnvelope(ExecutionStatus.Success),

            // --- FB-M1-41 status + single bindings of each constant kind ---
            ["success_atom"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("X", Atom("foo")) }),
            ["success_int"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("N", Int(42)) }),
            ["success_int_negative"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("N", Int(-7)) }),
            ["success_string"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("S", Str("hello, world")) }),

            // --- FB-M1-17 deref: nested bound structures + lists ---
            ["success_nested_struct"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("T", Struct("point", Int(1), Int(2))) }),
            ["success_list"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("L", GlpList(Int(1), Int(2), Int(3))) }),
            ["success_deep_nested"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[]
                {
                    B("Tree", Struct("node",
                        Struct("node", Atom("leaf"), Int(1)),
                        Struct("node", Atom("leaf"), Int(2)))),
                }),

            // --- multiple bindings: canonical (declaration) order matters for parity ---
            ["multi_binding"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[]
                {
                    B("A", Atom("alpha")),
                    B("B", Int(2)),
                    B("C", Str("gamma")),
                }),

            // --- FB-M1-17/14 var->writer: a remaining unbound VarRef carries a GlobalVarId ---
            ["var_to_writer"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("Z", Struct("pair", Atom("a"), Var("agent1", 9))) },
                varToWriter: new[] { V("W", new GlobalVarId("agent1", 9)) }),

            // --- FB-M1-42 suspended goal + blocking-reader set ---
            ["suspended"] = new ResultEnvelope(ExecutionStatus.Suspended,
                suspended: new[] { new GlobalVarId("agent1", 3), new GlobalVarId("agent2", 5) }),
            ["suspended_with_binding"] = new ResultEnvelope(ExecutionStatus.Suspended,
                resolvedBindings: new[] { B("Partial", Struct("waiting_on", Var("agent1", 11))) },
                varToWriter: new[] { V("Q", new GlobalVarId("agent1", 11)) },
                suspended: new[] { new GlobalVarId("agent1", 11) }),

            // --- failed with error ---
            ["failed"] = new ResultEnvelope(ExecutionStatus.Failed,
                error: "no matching clause for goal foo/2"),

            // --- captured output (carried; excluded from byte-parity, included in round-trip) ---
            ["with_captured"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("R", Atom("done")) },
                captured: Utf8("line one\nline two\n")),
        };

    /// <summary>
    /// GATED entries — floats (tag 0x03, ED-6) and 64-bit-int edges. Exercised for
    /// round-trip + no-heap ONLY; NOT asserted byte-final until their gates clear (R11/R6).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ResultEnvelope> Gated =
        new Dictionary<string, ResultEnvelope>
        {
            ["gated_real"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("Pi", Real(3.141592653589793)) }),
            ["gated_real_neg_zero"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("Z", Real(-0.0)) }),
            ["gated_real_nan"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("N", Real(double.NaN)) }),
            ["gated_real_inf"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("I", Real(double.PositiveInfinity)) }),
            ["gated_int64_max"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("Max", Int(long.MaxValue)) }),
            ["gated_int64_min"] = new ResultEnvelope(ExecutionStatus.Success,
                resolvedBindings: new[] { B("Min", Int(long.MinValue)) }),
        };

    /// <summary>All corpus entries (non-gated + gated) for round-trip / no-heap coverage.</summary>
    public static IEnumerable<KeyValuePair<string, ResultEnvelope>> All()
    {
        foreach (var kv in NonGated) yield return kv;
        foreach (var kv in Gated) yield return kv;
    }

    public static ResultEnvelope ByName(string name) =>
        NonGated.TryGetValue(name, out var e) ? e : Gated[name];
}
