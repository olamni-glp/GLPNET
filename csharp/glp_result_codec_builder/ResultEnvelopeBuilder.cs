// Engine→envelope builder + server-side deep-resolve — feature 038 (T020/T021).
//
// C# reference slice: ports glp_runtime/lib/codec/result_envelope_builder.dart
// (the Dart source of truth) to the C# runtime. Bridges the live engine heap +
// DrainResult to the heap-independent ResultEnvelope value (data-model §1,
// contract §6). Lives in its own project (owner decision A) so the codec value
// types (GlpResultCodec) stay engine-free; depends on IHeapView (decision B),
// not the concrete HeapFCP, so deep-resolve is testable without a live engine.
//
// Deep-resolve: walk a runtime term over the heap to depth-32 — matching the
// engine reference `_ResolveDeepForTrace` (glp_engine.cs:607; depth-32, recurse
// into StructTerm args over the shallow `Heap.Dereference`). Unlike that trace
// helper, hitting the depth bound yields an EXPLICIT `$truncated` marker term,
// never a silent cut (R5 / Constitution II).
//
// Owner-ruled mappings (2026-06-30, mirror the Dart reference):
//   - every runtime ConstTerm(string) is an ATOM (tag 0x05); GLP carries no
//     heap-level string literal.
//   - GlobalVarId.AgentId = a per-glpnet-instance unique id; LocalId = the local
//     writer/heap addr.
//   - suspended = DrainResult.BlockingReaders (reader addrs) mapped to
//     GlobalVarId, sorted + deduped.

using System;
using System.Collections.Generic;
using System.Linq;

using Rt = GlpRuntime.Runtime;

namespace GlpRuntime.ResultCodec.Builder;

/// <summary>
/// Builds the heap-independent <see cref="ResultEnvelope"/> from a finished engine
/// run (deep-resolve + envelope assembly). Static; produces engine-free values.
/// </summary>
public static class ResultEnvelopeBuilder
{
    /// <summary>Deep-resolve depth bound — matches the Dart/C# reference (`_ResolveDeepForTrace`).</summary>
    public const int DeepResolveDepth = 32;

    /// <summary>
    /// The explicit truncation marker emitted at the depth bound (R5, contract §6).
    /// A normal, decodable term — deterministic and parity-stable; never a silent cut.
    /// </summary>
    public static Term TruncatedMarker() => new StructTerm("$truncated", Array.Empty<Term>());

    /// <summary>
    /// Map a runtime constant value to the codec Constant model.
    /// Owner-ruled (#2): every string is an atom; GLP carries no heap-level string.
    /// bool / null / other have no GLP result-term representation — surface, never mask.
    /// </summary>
    private static Constant MapConstValue(object? value) => value switch
    {
        long l => new ConstInt(l),
        int i => new ConstInt(i),
        double d => new ConstReal(d),
        string s => new ConstAtom(s),
        _ => throw new ResultCodecException(
            "deep-resolve: unsupported constant value of runtime type " +
            $"{value?.GetType().Name ?? "null"} ({value})"),
    };

    /// <summary>
    /// Server-side deep-resolve of a runtime term to the heap-independent codec Term.
    ///
    /// <paramref name="instanceId"/> is this glpnet instance's unique id (the AgentId
    /// of every GlobalVarId minted here). Resolves bound vars via the shallow
    /// <c>IHeapView.Dereference</c> and recurses into StructTerm args to
    /// <see cref="DeepResolveDepth"/>; an unbound VarRef becomes
    /// <c>VarRef(GlobalVarId(instanceId, addr))</c>; the depth bound yields
    /// <see cref="TruncatedMarker"/>.
    /// </summary>
    public static Term DeepResolveTerm(IHeapView heap, Rt.Term term, string instanceId, int depth = 0)
    {
        if (depth > DeepResolveDepth) return TruncatedMarker();
        var d = heap.Dereference(term);
        switch (d)
        {
            case Rt.StructTerm s:
                var args = new List<Term>(s.Args.Count);
                foreach (var a in s.Args)
                    args.Add(DeepResolveTerm(heap, a, instanceId, depth + 1));
                return new StructTerm(s.Functor, args);
            case Rt.ConstTerm c:
                return new ConstTerm(MapConstValue(c.Value));
            case Rt.VarRef v:
                // still unbound after deref → global writer identity (R7)
                return new VarRef(new GlobalVarId(instanceId, v.Addr));
            default:
                // MutualRefTerm / ModuleTerm etc. are outside the envelope term model.
                throw new ResultCodecException(
                    $"deep-resolve: unsupported runtime term {d.GetType().Name}");
        }
    }

    /// <summary>
    /// Build the heap-independent <see cref="ResultEnvelope"/> from a finished engine run.
    ///
    /// <para><paramref name="queryVarWriters"/>: query var name → its writer heap id, in
    /// the engine's canonical declaration order (the parity invariant — iteration order
    /// MUST be the caller's ordered sequence, never a hash order). A bound writer → a
    /// deep-resolved binding; an unbound writer → a var→writer global-id entry.</para>
    /// <para><paramref name="drainResult"/>: the scheduler's final DrainResult;
    /// <c>BlockingReaders</c> (reader addrs) become the suspended GlobalVarId set (sorted,
    /// deduped). <paramref name="instanceId"/>: this glpnet instance's unique id.</para>
    /// </summary>
    public static ResultEnvelope BuildResultEnvelope(
        IHeapView heap,
        IEnumerable<KeyValuePair<string, int>> queryVarWriters,
        Rt.DrainResult drainResult,
        string instanceId,
        byte[]? captured = null,
        string? error = null)
    {
        var resolvedBindings = new List<KeyValuePair<string, Term>>();
        var varToWriter = new List<KeyValuePair<string, GlobalVarId>>();

        foreach (var entry in queryVarWriters)
        {
            var name = entry.Key;
            var writerId = entry.Value;
            if (heap.IsBound(writerId))
            {
                resolvedBindings.Add(new KeyValuePair<string, Term>(
                    name, DeepResolveTerm(heap, new Rt.VarRef(writerId), instanceId)));
            }
            else
            {
                varToWriter.Add(new KeyValuePair<string, GlobalVarId>(
                    name, new GlobalVarId(instanceId, writerId)));
            }
        }

        // suspended = blocking-reader addrs → global ids, deterministic (sorted) + deduped
        var suspended = drainResult.BlockingReaders
            .Distinct()
            .OrderBy(addr => addr)
            .Select(addr => new GlobalVarId(instanceId, addr))
            .ToList();

        return new ResultEnvelope(
            status: MapStatus(drainResult.Status),
            resolvedBindings: resolvedBindings,
            varToWriter: varToWriter,
            suspended: suspended,
            captured: captured,
            error: error);
    }

    /// <summary>Map the runtime ExecutionStatus to the codec ExecutionStatus.</summary>
    private static ExecutionStatus MapStatus(Rt.ExecutionStatus status) => status switch
    {
        Rt.ExecutionStatus.Succeeded => ExecutionStatus.Success,
        Rt.ExecutionStatus.Suspended => ExecutionStatus.Suspended,
        Rt.ExecutionStatus.Failed => ExecutionStatus.Failed,
        _ => throw new ResultCodecException($"deep-resolve: unknown ExecutionStatus {status}"),
    };
}
