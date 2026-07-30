// RequestDispatcher — one request in, one terminal response out (T010/T022;
// contracts/wire-protocol.md rules 2/3).
//
//   LOAD_SOURCE → engine full pipeline (parse → typecheck → compile), ACK on
//                 success, RESULT(Failed envelope) on compile/type errors —
//                 the engine stays serving (FR-006). Successful loads are
//                 recorded as LoadedUnits (the snapshot's 0x05 source record).
//   RUN_GOAL    → execute; RESULT = 038 ResultEnvelope: ground-only subset,
//                 bindings pre-rendered engine-side to display strings (R6),
//                 captured program output as the envelope's UTF-8 blob (R3).
//   SNAPSHOT    → quiescent: capture + durable write, ACK "snapshot seq=N";
//                 busy: DEFERRED + parked — fires at the next quiescent moment
//                 (checked after every subsequent request), visible via STATUS
//                 (FR-014, wire rule 5).
//   STATUS      → ACK with session/state + pending-snapshot + last-seq summary.
//   SHUTDOWN    → graceful: automatic final snapshot (FR-014 trigger) then ACK
//                 and exit 0; a non-quiescent engine reports the skip loudly
//                 rather than emitting an inconsistent snapshot.
//   PING        → ACK (wire rule 7).

using System.Text;

using GlpRuntime.Engine;
using GlpRuntime.EngineHost.Snapshot;
using GlpRuntime.EngineHost.Store;
using GlpRuntime.Link.Primitives;
using GlpRuntime.ResultCodec;
using GlpRuntime.SplitProtocol;

using EngineExecutionStatus = GlpRuntime.Runtime.ExecutionStatus;
using RcConstString = GlpRuntime.ResultCodec.ConstString;
using RcConstTerm = GlpRuntime.ResultCodec.ConstTerm;
using RcExecutionStatus = GlpRuntime.ResultCodec.ExecutionStatus;
using RcTerm = GlpRuntime.ResultCodec.Term;

namespace GlpRuntime.EngineHost;

public sealed class RequestDispatcher
{
    private readonly GlpEngine _engine;
    private readonly EngineSession _session;
    private readonly Quiescence _quiescence;
    private readonly SnapshotStore _store;
    private readonly LinkRuntime? _linkRuntime;
    private readonly string _rootSelfSource;
    private readonly List<LoadedUnit> _units;
    private int _loadCounter;

    /// <summary>Set once SHUTDOWN has been ACKed; the server loop then exits 0 (wire rule 6).</summary>
    public bool ShutdownRequested { get; private set; }

    /// <summary>Seq of the most recent successful snapshot in this process (STATUS surface).</summary>
    public ulong? LastSnapshotSeq { get; private set; }

    public EngineSession Session => _session;

    /// <summary>The host's record of successfully loaded client units (snapshot 0x05).</summary>
    public IReadOnlyList<LoadedUnit> LoadedUnits => _units;

    public RequestDispatcher(
        GlpEngine engine,
        EngineSession session,
        Quiescence quiescence,
        SnapshotStore store,
        LinkRuntime? linkRuntime,
        string rootSelfSource,
        IEnumerable<LoadedUnit>? restoredUnits = null)
    {
        _engine = engine;
        _session = session;
        _quiescence = quiescence;
        _store = store;
        _linkRuntime = linkRuntime;
        _rootSelfSource = rootSelfSource;
        _units = restoredUnits?.ToList() ?? new List<LoadedUnit>();
        _loadCounter = _units.Count; // restored units keep their names; new loads continue after them
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

        ResponseFrame response;
        try
        {
            response = request.Kind switch
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

        // FR-014: a parked snapshot fires at the next quiescent moment. The
        // current request already has its terminal response; the deferred
        // snapshot's outcome is observable via STATUS.
        if (_quiescence.SnapshotPending && _quiescence.IsQuiescent &&
            _session.State == EngineState.Serving)
        {
            try
            {
                if (TryTakeSnapshot(out var seq, out _))
                {
                    _quiescence.ClearPending();
                    Console.WriteLine($"glp_engine_host: deferred snapshot written (seq={seq})");
                }
            }
            catch (Exception ex)
            {
                // Loud, but the in-flight response still goes out (FR-006).
                Console.Error.WriteLine($"glp_engine_host: deferred snapshot FAILED: {ex.Message}");
            }
        }

        return response;
    }

    private ResponseFrame LoadSource(RequestFrame request)
    {
        var source = request.BodyText();
        var name = $"_client_load_{++_loadCounter}";
        try
        {
            _engine.LoadSource(source, filename: name);
            _units.Add(new LoadedUnit(name, source)); // snapshot 0x05 record — success only
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
        var pending = _quiescence.SnapshotPending
            ? $"deferred_since={_quiescence.PendingSince:O}"
            : "none";
        var body =
            $"state={_session.StateWord} " +
            $"engine={_session.EngineIdentity} " +
            $"loaded_programs={_engine.LoadedPrograms.Count} " +
            $"pending_snapshot={pending} " +
            $"last_snapshot_seq={(LastSnapshotSeq?.ToString() ?? "none")}";
        return ResponseFrame.Text(request.RequestId, ResponseKind.Ack, body);
    }

    private ResponseFrame Snapshot(RequestFrame request)
    {
        if (!_quiescence.IsQuiescent)
        {
            // FR-014: defer, report the parked state (wire rule 5) — never an
            // inconsistent snapshot.
            _quiescence.ParkSnapshotRequest();
            return ResponseFrame.Empty(request.RequestId, ResponseKind.Deferred);
        }

        if (!TryTakeSnapshot(out var seq, out var degradations))
        {
            // A timer fired between the quiescence check and the disarm — the
            // engine turned out busy after all: same deferral contract.
            _quiescence.ParkSnapshotRequest();
            return ResponseFrame.Empty(request.RequestId, ResponseKind.Deferred);
        }

        _quiescence.ClearPending();
        var body = $"snapshot seq={seq}";
        if (degradations.Count > 0)
            body += " DEGRADED: " + string.Join("; ", degradations); // US2/AS-4 — loud to the requester
        return ResponseFrame.Text(request.RequestId, ResponseKind.Ack, body);
    }

    private ResponseFrame Shutdown(RequestFrame request)
    {
        // FR-014: graceful shutdown triggers an automatic final snapshot.
        string finalNote;
        if (_quiescence.IsQuiescent)
        {
            try
            {
                finalNote = TryTakeSnapshot(out var seq, out var degradations)
                    ? $"final_snapshot_seq={seq}" +
                      (degradations.Count > 0 ? " DEGRADED: " + string.Join("; ", degradations) : "")
                    : "final_snapshot=skipped(timer fired during disarm — engine not quiescent)";
            }
            catch (Exception ex)
            {
                finalNote = $"final_snapshot=FAILED({ex.Message})";
            }
        }
        else
        {
            // Never an inconsistent snapshot (FR-014) — skip loudly instead.
            finalNote = "final_snapshot=skipped(engine not quiescent: " +
                        $"{_engine.Runtime.Gq.Length} goal(s) queued)";
        }

        _session.TransitionTo(EngineState.ShuttingDown);
        ShutdownRequested = true;
        return ResponseFrame.Text(request.RequestId, ResponseKind.Ack, $"shutting_down {finalNote}");
    }

    /// <summary>
    /// Disarm timers, capture, durably write, re-arm — the full FR-010..FR-015
    /// path. False when the engine turned out non-quiescent after the timer
    /// disarm (caller defers). Store/capture errors propagate loudly.
    /// </summary>
    private bool TryTakeSnapshot(out ulong seq, out IReadOnlyList<string> degradations)
    {
        seq = 0;
        degradations = Array.Empty<string>();

        _session.TransitionTo(EngineState.Snapshotting);
        try
        {
            var disarmed = _quiescence.DisarmTimersForCapture();
            if (disarmed is null)
                return false; // a timer fired concurrently — defer (FR-014)

            try
            {
                seq = _store.NextSeq();
                var blob = SnapshotCapture.Capture(
                    _engine, _linkRuntime, _units, _rootSelfSource, disarmed,
                    _session.EngineIdentity, seq);
                var encoded = blob.Encode();
                _store.Write(blob, encoded, seq);
                LastSnapshotSeq = seq;
                degradations = _store.LastWriteDegradations.ToList();
                return true;
            }
            finally
            {
                _quiescence.RearmTimers(disarmed);
            }
        }
        finally
        {
            if (_session.State == EngineState.Snapshotting)
                _session.TransitionTo(EngineState.Serving);
        }
    }

    private static RcExecutionStatus MapStatus(EngineExecutionStatus status) => status switch
    {
        EngineExecutionStatus.Succeeded => RcExecutionStatus.Success,
        EngineExecutionStatus.Suspended => RcExecutionStatus.Suspended,
        EngineExecutionStatus.Failed => RcExecutionStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
