// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Durable, contiguous, cross-process dot-counter allocation (feature 102, codex round-2 finding
// `allocate-durable-unique-dot-counters`).
//
// Contract federation-wire.md W3 / FR-010, FR-028.
//
// WHAT WAS WRONG. The console minted a dot counter as `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`.
// Two failures, both silent:
//
//   1. COLLISION. Two `post` processes inside the same millisecond emit the same (nodeId, counter)
//      for DIFFERENT operations. The fold deduplicates on exactly that dot, so one of the two
//      operations is discarded — by the mechanism whose entire job is to never lose an operation.
//      A clock that moves backwards (NTP step, VM resume) reopens the same hole for a whole window.
//
//   2. SPARSITY. Timestamp counters leave enormous gaps. Under the reconciliation frontier a gap is
//      indistinguishable from a hole, so every exchange carries the sparse set forever and the
//      frontier never compacts.
//
// A counter is an identity allocator, not a timestamp. It must be MONOTONE, CONTIGUOUS and DURABLE
// across processes. So it is persisted, allocated under an OS file lock (the same discipline the
// PGLite bridge uses for its cross-process guard), and seeded from the highest counter already in
// the local log so a lost sequence file can never re-issue a counter that is already on the board.

using GlpRuntime.CrdtMsg.Crdt;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>
/// Allocates this host's own dot counters: 1, 2, 3, … never repeating, never going backwards,
/// surviving process exit, and safe against a second process racing it.
/// </summary>
public sealed class DotSequencer
{
    private readonly string _path;
    private readonly string _nodeId;
    private readonly long _floor;

    /// <param name="path">The sequence file. Lives beside node.key, outside the repo.</param>
    /// <param name="nodeId">The peer half of every dot this allocates.</param>
    /// <param name="floor">
    /// The highest counter already present in the local log for this node id. The sequence file is
    /// convenience; the LOG is the truth. Seeding from the log means a deleted or truncated sequence
    /// file causes a harmless jump, never a re-issue.
    /// </param>
    public DotSequencer(string path, string nodeId, long floor = 0)
    {
        _path = path;
        _nodeId = nodeId;
        _floor = floor;
    }

    /// <summary>
    /// How long an allocation waits for a contended sequence file before it gives up and raises.
    /// This is a fault boundary, not a correctness budget: allocation is correct at any wait, and
    /// exceeding this means a holder is stuck, not that the host is merely busy.
    /// </summary>
    public static readonly TimeSpan ContentionTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The default sequence path — beside the node identity, outside the repo.</summary>
    public static string DefaultPath() =>
        Path.Combine(Path.GetDirectoryName(FederationConfig.DefaultPath())!, "dot.seq");

    /// <summary>
    /// Allocate the next dot. The read-modify-write happens while holding an exclusive handle on the
    /// sequence file, so two processes cannot both observe the same value.
    /// </summary>
    public Dot Next()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        // Retry only for the contended-file case. Any other failure is a real fault and is raised.
        //
        // Contention is EXPECTED here — several callers legitimately allocate at once, and this
        // class exists to be "safe against a second process racing it". Two properties make that
        // wait sound, and the previous `Sleep(10)` capped at 50 attempts had neither:
        //
        //   * A DEADLINE, not an attempt count. The old budget was 50 x 10ms = 500ms, justified by
        //     the comment "it releases in us". This very method falsifies that: it calls
        //     Flush(flushToDisk: true) — a real FlushFileBuffers — while still holding the file, so
        //     a holder's tenure is MILLISECONDS. 200 concurrent allocations need more queue than
        //     500ms, the unluckiest caller ran out of attempts, and the IOException escaped.
        //
        //   * JITTER. A fixed 10ms period is a thundering herd: every waiter wakes on the same
        //     boundary, one wins, the rest re-collide, and one caller can lose repeatedly.
        //     Randomising each wait de-synchronises the convoy.
        //
        // Measured on an idle host, ConcurrentAllocationsNeverCollide alone, each run a separate
        // process: 2 failures in 12 runs BEFORE this change, 0 failures in 30 runs AFTER it. The
        // deadline stays bounded on purpose — a genuinely stuck holder must still fail loudly
        // rather than hang forever.
        long deadline = Environment.TickCount64 + (long)ContentionTimeout.TotalMilliseconds;
        int backoffCapMs = 1;
        while (true)
        {
            try
            {
                using var fs = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                                              FileShare.None);
                long stored = 0;
                using (var reader = new StreamReader(fs, leaveOpen: true))
                {
                    string? text = reader.ReadToEnd().Trim();
                    if (!string.IsNullOrEmpty(text)) long.TryParse(text, out stored);
                }

                long next = Math.Max(stored, _floor) + 1;

                fs.SetLength(0);
                fs.Position = 0;
                using (var writer = new StreamWriter(fs, leaveOpen: true))
                {
                    writer.Write(next.ToString());
                    writer.Flush();
                }
                fs.Flush(flushToDisk: true);   // durable BEFORE the caller uses the counter

                return new Dot(_nodeId, next);
            }
            catch (IOException) when (Environment.TickCount64 < deadline)
            {
                // Full jitter over a doubling window: every waiter picks a different wake instant,
                // so the convoy breaks up instead of re-colliding on a shared boundary.
                Thread.Sleep(Random.Shared.Next(1, backoffCapMs + 1));
                if (backoffCapMs < 16) backoffCapMs *= 2;
            }
        }
    }

    /// <summary>
    /// The highest counter this node id already has in <paramref name="ops"/>. Used as the floor, so
    /// the allocator can never re-issue a counter that is already on the board.
    /// </summary>
    public static long HighestFor(string nodeId, IEnumerable<FederationOp> ops)
    {
        long high = 0;
        foreach (var op in ops)
            if (string.Equals(op.OpId.PeerName, nodeId, StringComparison.Ordinal) && op.OpId.Counter > high)
                high = op.OpId.Counter;
        return high;
    }
}
