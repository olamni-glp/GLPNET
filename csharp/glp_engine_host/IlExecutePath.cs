// IlExecutePath — the engine host's compiler-free IL execute path (064 US3,
// T023; contracts/il-request-kind.md).
//
//   LOAD_IL     → verify + decode the shipped 062 CompiledIlEnvelope
//                 (version/digest obligations verbatim), register the decoded
//                 BytecodeProgram under its source_metadata module_ref, ACK
//                 "loaded module_ref=…". Any hardening failure is a TYPED
//                 IL_REFUSED response (malformed | il_version_mismatch |
//                 digest_mismatch | mid_transfer_truncation) — the engine keeps
//                 serving and NEVER falls back silently to the text path.
//   RUN_GOAL_IL → run a NULLARY goal_ref label ("functor/0" — the 062
//                 execute-on-B precedent, glp_il_codec.tests/GlpExecutor) against
//                 the session's loaded IL modules plus an optional inline
//                 one-shot envelope; reply with the existing 038 ResultEnvelope
//                 (status + captured '_output' blob; a compiled goal carries no
//                 query variables, so resolved_bindings is empty).
//
// COMPILER-FREE BY CONSTRUCTION (contract rule 5): this type touches ONLY the
// runtime seam (GlpRuntime.Runtime + GlpRuntime.Bytecode), the IL codec
// (GlpRuntime.IlCodec) and the result codec — never GlpRuntime.Compiler,
// GlpRuntime.Analysis or GlpEngine (whose load/run paths parse with the
// compiler). glp_split_protocol.tests/CompilerAbsenceTests walks this type's
// compiled method bodies and asserts exactly that; keep new code inside this
// file free of those namespaces. (A full ASSEMBLY-level split is blocked by the
// monolithic glp_runtime_net assembly — runtime sources reference
// GlpRuntime.Compiler — so the enforced boundary is this type, the whole IL
// dispatch path.)
//
// The execution harness mirrors GlpEngine._RunSingleGoalAsync minus the
// compiler-parsed argument setup a nullary goal does not need: one persistent
// GlpRuntimeEngine across the session (loaded modules + suspended goals
// survive between requests, like the text engine), a fresh runner/scheduler
// per goal over the CombinedProgram-style concatenation of loaded modules.

using System.Text;

using GlpRuntime.Bytecode;
using GlpRuntime.IlCodec;
using GlpRuntime.ResultCodec;
using GlpRuntime.Runtime;
using GlpRuntime.SplitProtocol;

using RcExecutionStatus = GlpRuntime.ResultCodec.ExecutionStatus;
using RtExecutionStatus = GlpRuntime.Runtime.ExecutionStatus;
using RtTerm = GlpRuntime.Runtime.Term;

namespace GlpRuntime.EngineHost;

public sealed class IlExecutePath
{
    private readonly GlpRuntimeEngine _runtime = new();

    /// <summary>Loaded IL modules, insertion-ordered (module_ref → program) —
    /// mirrors GlpEngine._loadedPrograms for the text path.</summary>
    private readonly Dictionary<string, BytecodeProgram> _modules = new(StringComparer.Ordinal);

    private int _goalId = 1;

    /// <summary>Max execution cycles — same default as GlpEngine.MaxCycles.</summary>
    public int MaxCycles { get; set; } = 10000;

    /// <summary>Loaded IL module refs (diagnostics/STATUS surface).</summary>
    public IReadOnlyCollection<string> ModuleRefs => _modules.Keys;

    // NOTE no SystemPredicatesImpl registration: it is internal to the runtime
    // assembly, and the 062 execute-on-B harness (GlpExecutor) runs decoded IL
    // without it. Body kernels ('_output', '_add', …) are default-registered by
    // GlpRuntimeEngine itself — those are what compiled programs call.

    /// <summary>LOAD_IL: verify + decode the envelope, register the module.</summary>
    public ResponseFrame Load(RequestFrame request)
    {
        DecodedProgram decoded;
        CompiledIlEnvelope envelope;
        try
        {
            (decoded, envelope) = CompiledIlEnvelopeCodec.Unwrap(request.Body);
        }
        catch (Exception ex) when (TryClassifyRefusal(ex, out var code))
        {
            // Typed refusal (contract rules 1–2): the engine keeps serving; the
            // refused envelope registers NOTHING (no partial module, no silent
            // fallback to the text path).
            return new ResponseFrame(request.RequestId, ResponseKind.IlRefused,
                new IlRefusal(code, ex.Message).Encode());
        }

        // Re-load under the same module_ref replaces (text-path _loadedPrograms
        // semantics for a re-sent filename).
        _modules[envelope.SourceMetadata] = decoded.Program;
        return ResponseFrame.Text(request.RequestId, ResponseKind.Ack,
            $"loaded module_ref={envelope.SourceMetadata} il_version={envelope.IlVersion}");
    }

    /// <summary>RUN_GOAL_IL: run a nullary goal_ref against loaded modules (+ optional inline one-shot).</summary>
    public async Task<ResponseFrame> RunGoalAsync(RequestFrame request)
    {
        RunGoalIlBody body;
        try
        {
            body = RunGoalIlBody.Decode(request.Body);
        }
        catch (SplitProtocolException ex)
        {
            return ResponseFrame.Text(request.RequestId, ResponseKind.ProtocolError, ex.Message);
        }

        // goal_ref is a label "functor/arity" with arity 0 — a compiled nullary
        // entry point. Anything else is refused loudly: without the compiler
        // there is no goal parser on this path (and never a silent text fallback).
        var slash = body.GoalRef.LastIndexOf('/');
        if (slash <= 0 || !int.TryParse(body.GoalRef[(slash + 1)..], out var arity))
        {
            return ResponseFrame.Text(request.RequestId, ResponseKind.ProtocolError,
                $"RUN_GOAL_IL goal_ref must be a label 'functor/arity', got '{body.GoalRef}'");
        }
        if (arity != 0)
        {
            return ResponseFrame.Text(request.RequestId, ResponseKind.ProtocolError,
                $"RUN_GOAL_IL goal_ref must be NULLARY ('functor/0', the 062 execute-on-B " +
                $"precedent) — compile arguments into a one-shot inline envelope; got '{body.GoalRef}'");
        }

        BytecodeProgram? inline = null;
        if (body.InlineEnvelope is not null)
        {
            try
            {
                (var decoded, _) = CompiledIlEnvelopeCodec.Unwrap(body.InlineEnvelope);
                inline = decoded.Program;
            }
            catch (Exception ex) when (TryClassifyRefusal(ex, out var code))
            {
                return new ResponseFrame(request.RequestId, ResponseKind.IlRefused,
                    new IlRefusal(code, ex.Message).Encode());
            }
        }

        var program = CombinedProgram(inline);
        if (!program.Labels.TryGetValue(body.GoalRef, out var entryPc))
        {
            // Mirrors the text path's "Predicate X not found" — a structured
            // RESULT failure, not a protocol error (the request was well-formed).
            var notFound = new ResultEnvelope(RcExecutionStatus.Failed,
                error: $"Predicate {body.GoalRef} not found");
            return new ResponseFrame(request.RequestId, ResponseKind.Result,
                ResultEnvelopeCodec.Encode(notFound));
        }

        // Capture program output ('_output'/1 → OutputCallback) for the R3 blob —
        // same capture the text path's RunGoalAsync performs.
        var output = new StringBuilder();
        var previousCallback = _runtime.OutputCallback;
        _runtime.OutputCallback = line => output.Append(line).Append('\n');

        RtExecutionStatus status;
        string? error = null;
        try
        {
            var goalId = _goalId++;
            _runtime.SetGoalEnv(goalId, new CallEnv(new Dictionary<int, RtTerm>()));
            _runtime.SetGoalProgram(goalId, "main");

            var runner = new BytecodeRunner(program);
            var scheduler = new Scheduler(
                rt: _runtime,
                runners: new Dictionary<object?, BytecodeRunner> { { "main", runner } });

            _runtime.Gq.Enqueue(new GoalRef(goalId, entryPc));

            var result = await scheduler.DrainAsyncWithStatus(
                maxCycles: MaxCycles, debug: false, showBindings: false, debugOutput: false)
                .ConfigureAwait(false);
            status = result.Status;
        }
        catch (Exception ex)
        {
            status = RtExecutionStatus.Failed;
            error = ex.Message;
        }
        finally
        {
            _runtime.OutputCallback = previousCallback;
        }

        var envelope = new ResultEnvelope(
            MapStatus(status),
            captured: Encoding.UTF8.GetBytes(output.ToString()),
            error: error);
        return new ResponseFrame(request.RequestId, ResponseKind.Result,
            ResultEnvelopeCodec.Encode(envelope));
    }

    /// <summary>
    /// CombinedProgram-style concatenation of the loaded IL modules (insertion
    /// order, first label occurrence wins — GlpEngine.CombinedProgram's rule),
    /// with the optional one-shot inline program appended last.
    /// </summary>
    private BytecodeProgram CombinedProgram(BytecodeProgram? inline)
    {
        var allOps = new List<object>();
        foreach (var module in _modules.Values)
            allOps.AddRange(module.Instructions);
        if (inline is not null)
            allOps.AddRange(inline.Instructions);
        return new BytecodeProgram(allOps);
    }

    /// <summary>
    /// Map a decode/verify failure onto the 062 hardening taxonomy (contract
    /// rule 1). IlCodecException texts are the codec's stable obligation
    /// wording; a raw EndOfStream mid-decode is a truncation by definition.
    /// Anything else coherent-but-wrong is malformed. Returns false only for
    /// exceptions that are NOT decode refusals (they propagate).
    /// </summary>
    private static bool TryClassifyRefusal(Exception ex, out IlRefusalCode code)
    {
        switch (ex)
        {
            case EndOfStreamException:
                code = IlRefusalCode.MidTransferTruncation;
                return true;
            case IlCodecException ce when ce.Message.Contains("digest mismatch", StringComparison.OrdinalIgnoreCase):
                code = IlRefusalCode.DigestMismatch;
                return true;
            case IlCodecException ce when ce.Message.Contains("Truncated", StringComparison.OrdinalIgnoreCase):
                code = IlRefusalCode.MidTransferTruncation;
                return true;
            case IlCodecException ce when ce.Message.Contains("il_version", StringComparison.OrdinalIgnoreCase):
                code = IlRefusalCode.IlVersionMismatch;
                return true;
            case IlCodecException:
            case OverflowException:   // checked length casts on absurd declared sizes
            case ArgumentException:   // corrupt varints/offsets surfacing in stream reads
                code = IlRefusalCode.Malformed;
                return true;
            default:
                code = default;
                return false;
        }
    }

    private static RcExecutionStatus MapStatus(RtExecutionStatus status) => status switch
    {
        RtExecutionStatus.Succeeded => RcExecutionStatus.Success,
        RtExecutionStatus.Suspended => RcExecutionStatus.Suspended,
        RtExecutionStatus.Failed => RcExecutionStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
