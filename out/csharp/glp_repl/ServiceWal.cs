// feature 064 — durable listener service box (gavri variant), T007.
//
// Host-owned durable message log. Contract:
// specs/064-durable-listener-service-box/contracts/message-log-and-replay.md
//
// Reuses the SHIPPED crdtmsg op journal (IOpWal) rather than inventing a store:
//   * primary  — PgliteOpWal over the repo's single .pgdb/ cluster (Constitution VI-b),
//                reached with the existing COLAB_PG_CONN convention;
//   * fallback — the file OpWal under glpservice/wal/ (atomic temp→verify→rename commits);
//   * composition — primary-then-LOUD-degrade PER APPEND, the 061 snapshot-store discipline:
//     a primary append failure attempts the fallback, and only when BOTH fail is the message
//     delivered without durability (loudly — the contract's both-backends-fail policy).
//
// Log identity (review fix, cycle01 F1): the service's ops are keyed by peer "service-box"
// and appended into box "service-box" — the shared colab_op journal holds unrelated peers'
// traffic, so every read (replay enumeration, counter recovery) filters to the service's own
// peer name. Filtering by PEER keeps pre-fix file-WAL history (box "default") readable.
//
// Idempotence (SC-002 exactly-once) is the op's DVV dot: Append(op) is a no-op when the dot
// is already committed, so a crash between append and observation cannot duplicate a message.
// ZERO new GLP language surface (FR-006) — every line here is host code.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Store;
using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;

namespace GlpRuntime.Repl.Host;

internal sealed class ServiceWal : IDisposable
{
    private const string PeerName = "service-box";
    private const string BoxName = "service-box"; // never pollute the shared journal's box "default"
    private const string ConnEnv = "COLAB_PG_CONN";

    private readonly IOpWal _primary;
    private readonly IOpWal? _fallback;      // file WAL behind a pglite primary; null otherwise
    private readonly bool _soleBackend;      // no COLAB_PG_CONN ⇒ the file WAL is the ONLY backend
    private readonly NpgsqlColabOpDb? _db;   // owned pglite connection (disposed with this wal)
    private readonly PayloadSerializer _codec = new(PeerName);
    private long _counter;

    /// <summary>Which backend opened as primary — surfaced in the resume diagnostics.</summary>
    public string BackendName { get; }

    private ServiceWal(IOpWal primary, IOpWal? fallback, string backendName, bool soleBackend,
        NpgsqlColabOpDb? db)
    {
        _primary = primary;
        _fallback = fallback;
        _soleBackend = soleBackend;
        _db = db;
        BackendName = backendName;
        // Continue the dot sequence past the highest COMMITTED service counter — across BOTH
        // backends, so a prior run's degraded appends can never collide (review fix F3). Ops.Count
        // is wrong twice over: it counts foreign peers' ops and misses counter gaps.
        _counter = MaxServiceCounter(primary);
        if (fallback is not null)
            _counter = Math.Max(_counter, MaxServiceCounter(fallback));
    }

    /// <summary>
    /// Open the log for a registration: PGlite primary when COLAB_PG_CONN is configured and the
    /// cluster is reachable — with the file WAL under <c>&lt;repo&gt;/glpservice/wal/</c> kept
    /// armed as the per-append fallback — else the file WAL alone. Every degrade is LOUD through
    /// <paramref name="diag"/> — never silent. Returns null only if no backend opens, which the
    /// caller treats as "run without durability, loudly".
    /// </summary>
    public static ServiceWal? Open(string repoRoot, Action<string> diag)
    {
        var conn = Environment.GetEnvironmentVariable(ConnEnv);
        bool soleBackend = string.IsNullOrWhiteSpace(conn);

        IOpWal? pglite = null;
        NpgsqlColabOpDb? db = null;
        if (!soleBackend)
        {
            try
            {
                db = new NpgsqlColabOpDb(conn!);
                pglite = PgliteOpWal.Open(db);
            }
            catch (Exception e)
            {
                db?.Dispose(); // review fix F6a: never leak the cluster connection on an open failure
                db = null;
                diag($"resume: WAL primary (pglite) unavailable, degrading to the file log: {e.Message}");
            }
        }

        IOpWal? file = null;
        try
        {
            var dir = Path.Combine(repoRoot, "glpservice", "wal");
            Directory.CreateDirectory(dir);
            file = OpWal.Open(dir);
        }
        catch (Exception e)
        {
            if (pglite is null)
            {
                diag(soleBackend
                    ? $"resume: WAL append failed: {e.Message}"
                    : $"resume: WAL append failed on both backends: {e.Message}");
                return null;
            }
            diag($"resume: WAL fallback (file log) unavailable — pglite only: {e.Message}");
        }

        return pglite is not null
            ? new ServiceWal(pglite, file, "pglite", soleBackend: false, db)
            : new ServiceWal(file!, null, "file", soleBackend, db: null);
    }

    /// <summary>
    /// The service's OWN ops in receipt (commit) order — the replay source. The primary backend
    /// may be the SHARED colab journal, so foreign peers' rows are filtered out (review fix F1).
    /// </summary>
    public IReadOnlyList<Op> Ops => _primary.Ops.Where(op => op.Id.PeerName == PeerName).ToList();

    /// <summary>
    /// Durably append one delivered ground term. Called from the pump's runner-thread delivery
    /// observer BEFORE the program can act on the message (FR-004). Composition is per append:
    /// primary, then the file fallback, and only when BOTH fail is the message delivered without
    /// durability — LOUDLY (the contract's both-backends-fail policy). The counter advances only
    /// when the dot is consumed (committed here, or found already committed); a genuine append
    /// failure retries the same counter on the next message.
    /// </summary>
    public void Append(Term term, Action<string> diag)
    {
        long candidate = _counter + 1;
        var op = Op.Create(new Dot(PeerName, candidate), Array.Empty<Dot>(),
            _codec.SerializeAgentMessage(term), BoxName);

        string primaryCause;
        try
        {
            if (_primary.Append(op))
            {
                _counter = candidate;
                return;
            }
            // Duplicate dot: this counter is already committed (an earlier run / a lock-step
            // writer), so it is consumed either way — advance past it, and be LOUD that THIS
            // payload was discarded (review fix F3: a false return is never silent).
            _counter = candidate;
            diag($"resume: WAL append refused duplicate dot ({PeerName},{candidate}) — message NOT persisted");
            return;
        }
        catch (Exception e)
        {
            primaryCause = e.Message;
        }

        if (_fallback is not null)
        {
            try
            {
                if (_fallback.Append(op))
                {
                    _counter = candidate;
                    diag($"resume: WAL primary (pglite) append failed, degraded to the file log: {primaryCause}");
                    return;
                }
                _counter = candidate;
                diag($"resume: WAL append refused duplicate dot ({PeerName},{candidate}) — message NOT persisted");
                return;
            }
            catch (Exception e)
            {
                diag($"resume: WAL append failed on both backends: {primaryCause}; {e.Message}");
                return;
            }
        }

        diag(_soleBackend
            ? $"resume: WAL append failed: {primaryCause}"
            : $"resume: WAL append failed on both backends: {primaryCause}");
    }

    /// <summary>
    /// Decode one stored op back to the ground term that was received. The payloads this log
    /// stores are ground (the link layer is pure ground-relay), so the imported-variable
    /// allocator is never exercised on the happy path; <paramref name="allocateImportedVar"/>
    /// is threaded from the caller's heap so a non-ground payload fails loudly there rather
    /// than being silently reshaped here.
    /// </summary>
    public Term Decode(Op op, Func<bool, int> allocateImportedVar) =>
        _codec.DeserializeAgentMessagePayload(op.Payload, allocateImportedVar);

    /// <summary>Release the pglite backend's cluster connection (review fix F6a).</summary>
    public void Dispose() => _db?.Dispose();

    private static long MaxServiceCounter(IOpWal wal)
    {
        long max = 0;
        foreach (var op in wal.Ops)
            if (op.Id.PeerName == PeerName && op.Id.Counter > max)
                max = op.Id.Counter;
        return max;
    }
}
