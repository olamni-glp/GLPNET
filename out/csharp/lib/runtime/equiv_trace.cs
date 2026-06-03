// Feature 020 — candidate-side trace-equivalence instrumentation (T017).
//
// NOT a codegen-converted Dart file: this is hand-written instrumentation for
// the differential equivalence oracle. It emits the canonical EV/OUT wire
// format consumed by normalize.parse_csharp (the five R1 event kinds), per
// specs/020-trace-equivalence-fidelity/contracts/trace_normalization.md.
//
// HARD GATE 6 / anti-drift §4.A: emission is strictly additive and
// side-effect-only. It is OFF unless the GLP_EQUIV_TRACE env var names an
// output file; when off, every method is a cheap no-op (one cached bool
// check), so normal runs are behaviourally + performance-unchanged. The Dart
// golden (glp_runtime/) is NEVER modified — parse_dart adapts its existing
// :trace/:debug text. This is the C# candidate side only (R10).
//
// Wire format (one record per line; addresses are relabeled to logical vars by
// the Python normalizer, so raw heap addresses are emitted verbatim here):
//   EV <seq> BYTECODE_OP opcode=<name> pc=<int>
//   EV <seq> WRITER_BIND writer=<addr> shape=<canonical_term_shape>
//   EV <seq> SUSPEND     reader=<addr> goal=<goal_id>
//   EV <seq> REACTIVATE  goal=<goal_id>
//   EV <seq> UNIFY       outcome=success|suspend|fail [vars=<addr,addr,...>]
//   OUT <succeed|suspend|fail> [<var>=<shape> ...]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GlpRuntime.Runtime;

namespace GlpRuntime;

/// <summary>Flag-gated canonical trace sink for the feature-020 equivalence oracle.</summary>
public static class EquivTrace
{
    private static readonly object _lock = new();
    private static bool _init;
    private static bool _on;
    private static string? _path;
    private static int _seq;
    private static readonly StringBuilder _buf = new();

    /// <summary>True when GLP_EQUIV_TRACE names an output file (emission active).</summary>
    public static bool Enabled => Ready();

    private static bool Ready()
    {
        if (_init) return _on;
        _init = true;
        var p = Environment.GetEnvironmentVariable("GLP_EQUIV_TRACE");
        _on = !string.IsNullOrWhiteSpace(p);
        _path = _on ? p : null;
        return _on;
    }

    /// <summary>Begin a fresh run: reset the sequence counter and clear the buffer.</summary>
    public static void Reset()
    {
        if (!Ready()) return;
        lock (_lock) { _seq = 0; _buf.Clear(); }
    }

    // The BYTECODE_OP spine is aligned to the Dart golden's `:debug` observability
    // (option-a, decided 2026-06-03): the Dart runner (glp_runtime/lib/bytecode/
    // runner.dart) prints a per-op `[DEBUG] PC <pc>: <Op>` line at the TOP of each
    // of these handlers (unconditionally per dispatch). Emitting any others (Label,
    // HeadNil, Otherwise, the Put*/Set*/Spawn/Proceed body ops, …) would manufacture
    // FALSE divergences against the read-only golden (R10). A genuine behavioural
    // divergence WITHIN this set (e.g. a guard taking a different soft-fail path)
    // still surfaces correctly.
    //
    // GetValue is DELIBERATELY EXCLUDED: its Dart print fires only in one sub-case
    // (runner.dart:2037, VarRef→reader alias) — not for ConstTerm/StructTerm args —
    // so it is not reliably observable per-dispatch and would diverge whenever Dart
    // takes a non-printing GetValue branch. Sub-case-accurate GetValue emission is a
    // T022 refinement; excluding it minimizes false divergences for now.
    private static readonly HashSet<string> _spineOps = new(StringComparer.Ordinal)
    {
        "ClauseTry", "Push", "Pop", "UnifyStructure", "HeadStructure",
        "UnifyVariable", "GetVariable", "Commit",
        "NoMoreClauses", "Guard", "Ground", "NoReaders", "GroundEqual",
    };

    // ── The five R1 event kinds ──────────────────────────────────────────────

    public static void Op(string opcode, int pc)
    {
        if (!Ready()) return;
        if (!_spineOps.Contains(opcode)) return;  // align to Dart :debug observability
        Write($"EV {Next()} BYTECODE_OP opcode={San(opcode)} pc={pc}");
    }

    public static void WriterBind(int writer, object? value)
    {
        if (!Ready()) return;
        Write($"EV {Next()} WRITER_BIND writer={writer} shape={ShapeOf(value)}");
    }

    public static void Suspend(int reader, int goalId)
    {
        if (!Ready()) return;
        Write($"EV {Next()} SUSPEND reader={reader} goal=g{goalId}");
    }

    public static void Reactivate(int goalId)
    {
        if (!Ready()) return;
        Write($"EV {Next()} REACTIVATE goal=g{goalId}");
    }

    public static void Unify(string outcome, IEnumerable<int>? vars = null)
    {
        if (!Ready()) return;
        var list = vars?.ToList();
        var v = (list != null && list.Count > 0)
            ? " vars=" + string.Join(",", list.Select(x => x.ToString(CultureInfo.InvariantCulture)))
            : string.Empty;
        Write($"EV {Next()} UNIFY outcome={outcome}{v}");
    }

    /// <summary>Emit the final OUT record (status + query bindings) and flush.</summary>
    public static void Out(string status, IEnumerable<(string name, string shape)> bindings)
    {
        if (!Ready()) return;
        var sb = new StringBuilder($"OUT {status}");
        foreach (var (name, shape) in bindings)
            sb.Append($" {San(name)}={San(shape)}");
        Write(sb.ToString());
        Flush();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Address-free, depth-capped canonical shape of a heap term.</summary>
    public static string ShapeOf(object? term, int depth = 0)
    {
        if (depth > 8) return "_";
        switch (term)
        {
            case null:
                return "_";
            case ConstTerm c:
                return c.Value == null ? "nil" : $"const({San(c.Value.ToString() ?? "nil")})";
            case StructTerm s:
                var inner = string.Join(",", s.Args.Select(a => ShapeOf(a, depth + 1)));
                return $"{San(s.Functor)}/{s.Args.Count}({inner})";
            case VarRef:
                return "var";
            default:
                return $"const({San(term.ToString() ?? "nil")})";
        }
    }

    private static int Next() { lock (_lock) { return _seq++; } }

    private static void Write(string line) { lock (_lock) { _buf.AppendLine(line); } }

    private static void Flush()
    {
        if (_path == null) return;
        lock (_lock) { File.WriteAllText(_path, _buf.ToString()); }
    }

    // The wire format is whitespace/'='-delimited, so values must not contain
    // either. Collapse them deterministically (both REPL dialects sanitize the
    // same way; cross-dialect shape canonicalization is finalized at T022).
    private static string San(string s) =>
        s.Replace(' ', '_').Replace('=', ':').Replace('\n', '_').Replace('\r', '_').Replace('\t', '_');
}
