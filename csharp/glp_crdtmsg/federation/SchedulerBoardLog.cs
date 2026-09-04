// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The board log that attaches to the EXISTING scheduler board (feature 102, codex round-2 finding
// `attach-federation-to-the-existing-board-log`).
//
// Contract federation-wire.md W4 / FR-009, FR-011.
//
// TWO SCHEMAS SHARE ONE ROOT, AND THAT IS THE WHOLE PROBLEM.
//
//   scheduler-native   {"actor":"gavriella","op_id":"gavriella:000001","op_type":"claim",
//                       "seq":1,"wp_id":"…","timestamp":"…", …}
//   federation-native  {"op_id":{"peer":"<nodeid>","counter":7},"origin":"…","kind":"…", …}
//
// The scheduler's lanes read the first. Federation speaks the second. The previous implementation
// resolved the mismatch by writing its own private file under the CONFIG directory — so real lane
// claims never entered the federated fold and federated operations never reached the oracle the
// lanes actually read. Two boards, permanently disjoint, both reporting healthy. That is the second
// oracle the feature forbids.
//
// SO: READ BOTH, ADAPT THE NATIVE ONE. A scheduler line is adapted into a FederationOp — its
// (actor, seq) becomes the dot, its actor the origin, its op_type the kind, and the ENTIRE original
// line is carried verbatim as the body. Nothing is rewritten, reordered or dropped (FR-011); the
// adaptation is a read-side view, and the original bytes are still what is on disk.
//
// THE WRITE SIDE IS DELIBERATELY NOT SYMMETRIC, and this is a decision, not an omission. Appending
// federation-shaped lines into a lane's live `<actor>-ops-NNNNNN.jsonl` would feed records of an
// unknown schema to every existing scheduler reader on four hosts. Until an engineer rules on that
// interop, federation writes to its own kind (`fedops`) UNDER THE SAME ROOT — one board root, one
// discoverable substrate, no foreign lines in the lanes' live segments. `WriteMode.LaneSegment`
// implements the symmetric behaviour for when that ruling is made; it is not the default.

using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>Where federation appends its own operations within the board root.</summary>
public enum BoardWriteMode
{
    /// <summary>
    /// A federation-owned kind (<c>&lt;root&gt;/fedops/&lt;actor&gt;/</c>). Same root, same board,
    /// but no foreign-schema line enters a lane's live op-log. The default, and the safe one.
    /// </summary>
    FederationKind,

    /// <summary>
    /// The lane's own <c>&lt;root&gt;/ops/&lt;actor&gt;/</c> segment. Full symmetry — federated
    /// operations land where the lanes already read. REQUIRES the scheduler's readers to tolerate a
    /// federation-shaped line; do not select this before that is established.
    /// </summary>
    LaneSegment,
}

/// <summary>
/// Append-only board log over the existing scheduler board root. Reads EVERY actor's log so the
/// fold is the board, not this host's own corner of it.
/// </summary>
public sealed class SchedulerBoardLog : IBoardLog
{
    /// <summary>The kind directory federation owns when not writing into lane segments.</summary>
    public const string FederationKindName = "fedops";

    private readonly string _root;
    private readonly string _actor;
    private readonly BoardWriteMode _mode;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SchedulerBoardLog(string root, string actor, BoardWriteMode mode = BoardWriteMode.FederationKind)
    {
        _root = root;
        _actor = actor;
        _mode = mode;
    }

    /// <summary>The board root this log is attached to — reported, so it is never guessed at.</summary>
    public string Root => _root;

    /// <summary>Lines that were neither federation-shaped nor adaptable. COUNTED, never silently dropped.</summary>
    public int UnreadableLines { get; private set; }

    /// <summary>Scheduler-native lines adapted into the fold. Reported, so the number is visible.</summary>
    public int AdaptedLines { get; private set; }

    /// <summary>The file this host appends to.</summary>
    public string WritePath => _mode == BoardWriteMode.LaneSegment
        ? BoardRoot.ActorLogPath(_root, _actor)
        : Path.Combine(_root, FederationKindName, _actor, $"{_actor}-{FederationKindName}-000001.jsonl");

    public async Task AppendAsync(FederationOp op, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            string path = WritePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.AppendAllTextAsync(path, op.ToCanonicalJson() + Environment.NewLine, ct)
                      .ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<FederationOp>> ReadAllAsync(CancellationToken ct = default)
    {
        UnreadableLines = 0;
        AdaptedLines = 0;

        var ops = new List<FederationOp>();
        var paths = new List<string>(BoardRoot.AllActorLogs(_root));

        string fedDir = Path.Combine(_root, FederationKindName);
        if (Directory.Exists(fedDir))
            paths.AddRange(Directory.EnumerateDirectories(fedDir)
                                    .SelectMany(d => Directory.EnumerateFiles(d, $"*-{FederationKindName}-*.jsonl"))
                                    .OrderBy(p => p, StringComparer.Ordinal));

        foreach (var path in paths)
        {
            foreach (var line in await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var op = Parse(line);
                if (op is null) { UnreadableLines++; continue; }
                ops.Add(op);
            }
        }
        return ops;
    }

    /// <summary>
    /// Parse one line as either schema, or return null. Null is COUNTED and reported by the caller —
    /// a line silently skipped is how a board loses an operation and still reports itself converged.
    /// </summary>
    private FederationOp? Parse(string line)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(line); }
        catch (JsonException) { return null; }

        using (doc)
        {
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;

            // Federation-native: op_id is an OBJECT {peer, counter}.
            if (r.TryGetProperty("op_id", out var id) && id.ValueKind == JsonValueKind.Object)
            {
                try { return FederationOp.FromJson(line); }
                catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
                {
                    return null;
                }
            }

            // Scheduler-native: op_id is a STRING "<actor>:<seq>", with actor + seq beside it.
            var adapted = AdaptSchedulerLine(r);
            if (adapted is not null) AdaptedLines++;
            return adapted;
        }
    }

    /// <summary>
    /// Adapt a scheduler-native op into the federated view. The original line is carried VERBATIM as
    /// the body, so nothing about the lane's record is lost, reinterpreted, or written back changed.
    /// </summary>
    public static FederationOp? AdaptSchedulerLine(JsonElement r)
    {
        if (!r.TryGetProperty("actor", out var actorEl) || actorEl.ValueKind != JsonValueKind.String)
            return null;
        string actor = actorEl.GetString()!;
        if (string.IsNullOrWhiteSpace(actor)) return null;

        long seq;
        if (r.TryGetProperty("seq", out var seqEl) && seqEl.ValueKind == JsonValueKind.Number
            && seqEl.TryGetInt64(out var s))
        {
            seq = s;
        }
        else if (r.TryGetProperty("op_id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                 && idEl.GetString() is { } idText && idText.LastIndexOf(':') is var i && i >= 0
                 && long.TryParse(idText[(i + 1)..], out var parsed))
        {
            seq = parsed;
        }
        else return null;

        string kind = r.TryGetProperty("op_type", out var kEl) && kEl.ValueKind == JsonValueKind.String
            ? kEl.GetString()! : "sched_op";

        return FederationOp.Create(new Dot(actor, seq), actor, kind, r.Clone());
    }
}
