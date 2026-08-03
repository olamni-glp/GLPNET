// feature 064 — durable listener service box (gavri variant), T007.
//
// Host-owned durable message log. Contract:
// specs/064-durable-listener-service-box/contracts/message-log-and-replay.md
//
// Reuses the SHIPPED crdtmsg op journal (IOpWal) rather than inventing a store:
//   * primary  — PgliteOpWal over the repo's single .pgdb/ cluster (Constitution VI-b),
//                reached with the existing COLAB_PG_CONN convention;
//   * fallback — the file OpWal under glpservice/wal/ (atomic temp→verify→rename commits);
//   * composition — primary-then-LOUD-degrade, the 061 snapshot-store discipline.
//
// Idempotence (SC-002 exactly-once) is the op's DVV dot: Append(op) is a no-op when the dot
// is already committed, so a crash between append and observation cannot duplicate a message.
// ZERO new GLP language surface (FR-006) — every line here is host code.

using System;
using System.Collections.Generic;
using System.IO;

using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Store;
using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;

namespace GlpRuntime.Repl.Host;

internal sealed class ServiceWal
{
    private const string PeerName = "service-box";
    private const string ConnEnv = "COLAB_PG_CONN";

    private readonly IOpWal _wal;
    private readonly PayloadSerializer _codec = new(PeerName);
    private long _counter;

    /// <summary>Which backend actually opened — surfaced in the resume diagnostics.</summary>
    public string BackendName { get; }

    private ServiceWal(IOpWal wal, string backendName)
    {
        _wal = wal;
        BackendName = backendName;
        _counter = wal.Ops.Count; // continue the dot sequence past the recovered log
    }

    /// <summary>
    /// Open the log for a registration: PGlite primary when COLAB_PG_CONN is configured and the
    /// cluster is reachable, else the file WAL under <c>&lt;repo&gt;/glpservice/wal/</c>. A primary
    /// failure degrades LOUDLY through <paramref name="diag"/> — never silently. Returns null only
    /// if BOTH backends fail, which the caller treats as "run without durability, loudly".
    /// </summary>
    public static ServiceWal? Open(string repoRoot, Action<string> diag)
    {
        var conn = Environment.GetEnvironmentVariable(ConnEnv);
        if (!string.IsNullOrWhiteSpace(conn))
        {
            try
            {
                return new ServiceWal(PgliteOpWal.Open(new NpgsqlColabOpDb(conn!)), "pglite");
            }
            catch (Exception e)
            {
                diag($"resume: WAL primary (pglite) unavailable, degrading to the file log: {e.Message}");
            }
        }

        try
        {
            var dir = Path.Combine(repoRoot, "glpservice", "wal");
            Directory.CreateDirectory(dir);
            return new ServiceWal(OpWal.Open(dir), "file");
        }
        catch (Exception e)
        {
            diag($"resume: WAL append failed on both backends: {e.Message}");
            return null;
        }
    }

    /// <summary>Ops in receipt (commit) order — the replay source.</summary>
    public IReadOnlyList<Op> Ops => _wal.Ops;

    /// <summary>
    /// Durably append one delivered ground term. Called from the pump's runner-thread delivery
    /// observer BEFORE the program can act on the message (FR-004). A failure is LOUD and
    /// non-fatal: the message is still delivered (availability over durability — the contract's
    /// both-backends-fail policy).
    /// </summary>
    public void Append(Term term, Action<string> diag)
    {
        try
        {
            var dot = new Dot(PeerName, ++_counter);
            _wal.Append(Op.Create(dot, Array.Empty<Dot>(), _codec.SerializeAgentMessage(term)));
        }
        catch (Exception e)
        {
            diag($"resume: WAL append failed on both backends: {e.Message}");
        }
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
}
