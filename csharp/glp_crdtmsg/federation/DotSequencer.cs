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
        for (int attempt = 0; ; attempt++)
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
            catch (IOException) when (attempt < 50)
            {
                Thread.Sleep(10);   // another process holds the sequence file; it releases in µs
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
