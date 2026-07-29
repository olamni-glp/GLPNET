// RequestDispatcher — one request in, one terminal response out (T010;
// contracts/wire-protocol.md rules 2/3).
//
//   LOAD_SOURCE → engine full pipeline (parse → typecheck → compile), ACK on
//                 success, RESULT(Failed envelope) on compile/type errors —
//                 the engine stays serving (FR-006).
//   RUN_GOAL    → execute; RESULT = 038 ResultEnvelope: ground-only subset,
//                 bindings pre-rendered engine-side to display strings (R6),
//                 captured program output as the envelope's UTF-8 blob (R3).
//   STATUS      → ACK with the session/state summary.
//   PING        → ACK (wire rule 7).
//   SNAPSHOT / SHUTDOWN → wired fully in US2 (T022); SHUTDOWN already performs
//                 the clean exit path so US1 is operable end-to-end.

using System.Text;

using GlpRuntime.Engine;
using GlpRuntime.ResultCodec;
using GlpRuntime.SplitProtocol;

using EngineExecutionStatus = GlpRuntime.Runtime.ExecutionStatus;
using RcConstAtom = GlpRuntime.ResultCodec.ConstAtom;
using RcConstString = GlpRuntime.ResultCodec.ConstString;
using RcConstTerm = GlpRuntime.ResultCodec.ConstTerm;
using RcExecutionStatus = GlpRuntime.ResultCodec.ExecutionStatus;
using RcTerm = GlpRuntime.ResultCodec.Term;

namespace GlpRuntime.EngineHost;

public sealed class RequestDispatcher
{
    private readonly GlpEngine _engine;
    private readonly EngineSession _session;
    private int _loadCounter;

    /// <summary>Set once SHUTDOWN has been ACKed; the server loop then exits 0 (wire rule 6).</summary>
    public bool ShutdownRequested { get; private set; }

    public EngineSession Session => _session;

    public RequestDispatcher(GlpEngine engine, EngineSession session)
    {
        _engine = engine;
        _session = session;
    }

    /// <summary>
    /// Dispatch one request to its one terminal response. Never throws for
    /// engine-level errors (FR-006) — those come back as structured RESULT /
    /// PROTOCOL_ERROR responses with the engine still serving.
    /// </summary>
    public async Task<ResponseFrame> DispatchAsync(RequestFrame request)
    {
        // Wire rule 4: during restore only STATUS/PING are served.
        if (_session.State == EngineState.Restoring &&
            request.Kind is not (RequestKind.Status or RequestKind.Ping))
        {
            return ResponseFrame.Empty(request.RequestId, ResponseKind.EngineBusy);
        }

        try
        {
            return request.Kind switch
            {
                RequestKind.LoadSource => LoadSource(request),
                RequestKind.RunGoal => await RunGoalAsync(request).ConfigureAwait(false),
                RequestKind.Status => Status(request),
                RequestKind.Ping => ResponseFrame.Text(request.RequestId, ResponseKind.Ack, "pong"),
                RequestKind.Snapshot => Snapshot(request),
                RequestKind.Shutdown => Shutdown(request),
                _ => ResponseFrame.Text(request.RequestId, ResponseKind.ProtocolError,
                    $"unknown request kind 0x{(byte)request.Kind:X2}"),
            };
        }
        catch (Exception ex)
        {
            // Last-resort containment: report structurally, keep serving (FR-006).
            return ResponseFrame.Text(request.RequestId, ResponseKind.ProtocolError,
                $"engine error: {ex.Message}");
        }
    }

    private ResponseFrame LoadSource(RequestFrame request)
    {
        var source = request.BodyText();
        var name = $"_client_load_{++_loadCounter}";
        try
        {
            _engine.LoadSource(source, filename: name);
            return ResponseFrame.Text(request.RequestId, ResponseKind.Ack, "loaded");
        }
        catch (Exception ex)
        {
            // Compile/type errors are structured results that render meaningfully
            // and leave the engine usable (FR-006, US1 AS-4).
            var envelope = new ResultEnvelope(
                RcExecutionStatus.Failed,
                error: ex.Message);
            return new ResponseFrame(request.RequestId, ResponseKind.Result,
                ResultEnvelopeCodec.Encode(envelope));
        }
    }

    private async Task<ResponseFrame> RunGoalAsync(RequestFrame request)
    {
        var goal = request.BodyText();

        // Capture program output ('_output'/1 → OutputCallback) for the R3 blob.
        var output = new StringBuilder();
        var previousCallback = _engine.Runtime.OutputCallback;
        _engine.Runtime.OutputCallback = line => output.Append(line).Append('\n');

        ExecutionResult result;
        try
        {
            result = await _engine.RunGoalAsync(goal).ConfigureAwait(false);
        }
        finally
        {
            _engine.Runtime.OutputCallback = previousCallback;
        }

        // R6: ground-only subset with bindings pre-rendered to display strings by
        // the engine — the client holds no heap to render from (R7).
        var bindings = new List<KeyValuePair<string, RcTerm>>();
        foreach (var entry in result.Bindings)
        {
            var rendered = entry.Value is null
                ? "<unbound>"
                : TermRendering.FormatTerm(entry.Value, _engine);
            bindings.Add(new KeyValuePair<string, RcTerm>(
                entry.Key, new RcConstTerm(new RcConstString(rendered))));
        }

        var envelope = new ResultEnvelope(
            MapStatus(result.Status),
            resolvedBindings: bindings,
            captured: Encoding.UTF8.GetBytes(output.ToString()),
            error: result.Error);

        return new ResponseFrame(request.RequestId, ResponseKind.Result,
            ResultEnvelopeCodec.Encode(envelope));
    }

    private ResponseFrame Status(RequestFrame request)
    {
        var body =
            $"state={_session.StateWord} " +
            $"engine={_session.EngineIdentity} " +
            $"loaded_programs={_engine.LoadedPrograms.Count} " +
            $"pending_snapshot=none last_snapshot_seq=none";
        return ResponseFrame.Text(request.RequestId, ResponseKind.Ack, body);
    }

    private ResponseFrame Snapshot(RequestFrame request) =>
        // Wired fully by T022 (US2). Refusing loudly is honest; DEFERRED would
        // promise an eventual snapshot that US1 cannot deliver (wire rule 5).
        ResponseFrame.Text(request.RequestId, ResponseKind.ProtocolError,
            "SNAPSHOT is not available yet (061 US2 pending)");

    private ResponseFrame Shutdown(RequestFrame request)
    {
        // US2 (T022) adds the graceful final snapshot before exit (wire rule 6).
        _session.TransitionTo(EngineState.ShuttingDown);
        ShutdownRequested = true;
        return ResponseFrame.Text(request.RequestId, ResponseKind.Ack, "shutting_down");
    }

    private static RcExecutionStatus MapStatus(EngineExecutionStatus status) => status switch
    {
        EngineExecutionStatus.Succeeded => RcExecutionStatus.Success,
        EngineExecutionStatus.Suspended => RcExecutionStatus.Suspended,
        EngineExecutionStatus.Failed => RcExecutionStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
