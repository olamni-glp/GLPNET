// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The durable per-actor board log (feature 102, T032).
//
// Contract federation-wire.md W4 / FR-011, FR-030.
//
// This is the EXISTING board substrate, not a new one: a grow-only, per-actor, append-only JSONL
// file under the resolvable board root, exactly as the scheduler's op-logs already are
// (D:\coop\buildkit\sched\<kind>\<actor>\<actor>-<kind>-NNNNNN.jsonl). Federation appends to it and
// reads it back; it does not redesign it.
//
// THERE IS NO DELETE, NO TRUNCATE, AND NO REWRITE ON THIS INTERFACE, and none may be added
// (FR-011 / FR-017). Absence of the capability, not a guard against calling it.

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>Append-only durable storage for board operations.</summary>
public interface IBoardLog
{
    /// <summary>Append one operation. Must be durable before the caller ships it (FR-030).</summary>
    Task AppendAsync(FederationOp op, CancellationToken ct = default);

    /// <summary>Read every operation back, in append order.</summary>
    Task<IReadOnlyList<FederationOp>> ReadAllAsync(CancellationToken ct = default);
}

/// <summary>A per-actor JSONL log file — one canonical operation per line.</summary>
public sealed class JsonlBoardLog : IBoardLog
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonlBoardLog(string path) => _path = path;

    public string Path => _path;

    public async Task AppendAsync(FederationOp op, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
            await File.AppendAllTextAsync(_path, op.ToCanonicalJson() + Environment.NewLine, ct)
                      .ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<FederationOp>> ReadAllAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path)) return Array.Empty<FederationOp>();
        var ops = new List<FederationOp>();
        foreach (var line in await File.ReadAllLinesAsync(_path, ct).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            // A malformed line is a LOUD fault, not a silently-skipped one: silently skipping is how
            // a board loses an operation and still reports itself converged.
            ops.Add(FederationOp.FromJson(line));
        }
        return ops;
    }
}

/// <summary>In-memory log, for tests that are exercising the legs rather than the disk.</summary>
public sealed class InMemoryBoardLog : IBoardLog
{
    private readonly List<FederationOp> _ops = new();

    /// <summary>When set, <see cref="AppendAsync"/> throws — used to prove append-before-ship (SC-014).</summary>
    public bool FailNextAppend { get; set; }

    public Task AppendAsync(FederationOp op, CancellationToken ct = default)
    {
        if (FailNextAppend) { FailNextAppend = false; throw new IOException("simulated durable-write failure"); }
        lock (_ops) _ops.Add(op);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FederationOp>> ReadAllAsync(CancellationToken ct = default)
    {
        lock (_ops) return Task.FromResult<IReadOnlyList<FederationOp>>(_ops.ToList());
    }

    public int Count { get { lock (_ops) return _ops.Count; } }
}
