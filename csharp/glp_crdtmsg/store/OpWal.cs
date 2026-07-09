// Append-only op-WAL (feature 041-crdtmsg-mvp, T022 + T025).
//
// Contract C10 / data-model §9 / research R8 (the shipped 040 responder-store shape):
//   temp → SHA-256 verify → atomic commit → journal. Each committed op is a self-verifying file
//   `ops/<seq>.op` = [32-byte SHA-256 of the op bytes][op bytes], written to `<seq>.op.tmp`, read back
//   and verified, then ATOMICALLY renamed into place (the durable commit point). A journal line is
//   appended as an audit trail. Recovery is zero-loss to the atomic-commit point: it scans the
//   committed op files (the rename is the commit), verifies each self-hash (loud-fail on corruption),
//   and discards orphan `.tmp` files left by an interrupted, never-acknowledged write.
//
// The op is keyed by its DVV DOT (op_id) — the store seam, DISTINCT from any message msg_id (T025).
// Appending an op whose dot is already present is a no-op (idempotent, FR-015).
//
// 048 (bk-colab-yngenios-transport, T012): this file WAL now implements the extracted IOpWal seam and
// remains AS-IS alongside the PGlite-backed PgliteOpWal (048 D1/K1 — the colab journal-of-record in
// PGlite); callers choose the backend, the seam is identical.

using System.Security.Cryptography;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Envelope;

namespace GlpRuntime.CrdtMsg.Store;

public sealed class OpWal : IOpWal
{
    private const int ShaLen = 32;

    private readonly string _opsDir;
    private readonly string _journalPath;
    private readonly Dictionary<Dot, Op> _byDot = new();
    private readonly List<Op> _commitOrder = new();
    private long _nextSeq;

    private OpWal(string dir)
    {
        _opsDir = Path.Combine(dir, "ops");
        _journalPath = Path.Combine(dir, "journal");
    }

    /// <summary>Open (creating if needed) a WAL rooted at <paramref name="dir"/> and recover it.</summary>
    public static OpWal Open(string dir)
    {
        Directory.CreateDirectory(Path.Combine(dir, "ops"));
        var wal = new OpWal(dir);
        wal.Recover();
        return wal;
    }

    /// <summary>Ops in commit order (append order); a rebuildable projection folds these (T023).</summary>
    public IReadOnlyList<Op> Ops => _commitOrder;

    /// <summary>Count of distinct committed ops.</summary>
    public int Count => _byDot.Count;

    /// <summary>True if this op's dot is already committed.</summary>
    public bool Contains(Dot dot) => _byDot.ContainsKey(dot);

    /// <summary>The committed dot-set — the seed for Merkle anti-entropy (T024).</summary>
    public IReadOnlySet<Dot> DotSet() => new HashSet<Dot>(_byDot.Keys);

    /// <summary>
    /// Durably append an op. Idempotent by dot (returns false if already present, FR-015). Follows the
    /// 040 shape: write self-verifying temp → read-back verify → atomic rename → journal.
    /// </summary>
    public bool Append(Op op)
    {
        if (_byDot.ContainsKey(op.Id)) return false;

        long seq = _nextSeq;
        byte[] opBytes = OpCodec.Encode(op);
        byte[] sha = SHA256.HashData(opBytes);

        string finalPath = Path.Combine(_opsDir, $"{seq:D12}.op");
        string tmpPath = finalPath + ".tmp";

        using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(sha);
            fs.Write(opBytes);
            fs.Flush(flushToDisk: true);
        }

        // temp → SHA-256 verify (read back before committing)
        byte[] readBack = File.ReadAllBytes(tmpPath);
        if (readBack.Length != ShaLen + opBytes.Length ||
            !SHA256.HashData(readBack.AsSpan(ShaLen)).AsSpan().SequenceEqual(sha))
        {
            File.Delete(tmpPath);
            throw new CrdtMsgException($"op-WAL temp verify failed at seq {seq}");
        }

        // atomic commit (rename is the durable commit point)
        File.Move(tmpPath, finalPath, overwrite: false);

        // journal (audit trail)
        File.AppendAllText(_journalPath, $"{seq} {Convert.ToHexStringLower(sha)} {op.Id}\n");

        _byDot[op.Id] = op;
        _commitOrder.Add(op);
        _nextSeq = seq + 1;
        return true;
    }

    /// <summary>Merge a batch (anti-entropy delta) — idempotent; returns the count newly applied.</summary>
    public int Merge(IEnumerable<Op> ops)
    {
        int applied = 0;
        foreach (var op in ops)
            if (Append(op)) applied++;
        return applied;
    }

    private void Recover()
    {
        // Discard orphan temps: an interrupted, never-acknowledged write. Not a commit → not recovered.
        foreach (var tmp in Directory.EnumerateFiles(_opsDir, "*.op.tmp"))
            File.Delete(tmp);

        var committed = Directory.EnumerateFiles(_opsDir, "*.op")
            .Select(p => (path: p, seq: ParseSeq(p)))
            .Where(t => t.seq >= 0)
            .OrderBy(t => t.seq)
            .ToList();

        foreach (var (path, seq) in committed)
        {
            byte[] file = File.ReadAllBytes(path);
            if (file.Length < ShaLen)
                throw new CrdtMsgException($"op-WAL file {Path.GetFileName(path)} shorter than its hash prefix");
            var storedSha = file.AsSpan(0, ShaLen);
            var opBytes = file.AsSpan(ShaLen).ToArray();
            if (!SHA256.HashData(opBytes).AsSpan().SequenceEqual(storedSha))
                throw new CrdtMsgException($"op-WAL corruption: hash mismatch in {Path.GetFileName(path)}");

            Op op = OpCodec.Decode(opBytes);
            if (_byDot.TryAdd(op.Id, op))
                _commitOrder.Add(op);
            _nextSeq = Math.Max(_nextSeq, seq + 1);
        }
    }

    private static long ParseSeq(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path); // "<seq>"
        return long.TryParse(name, out long seq) ? seq : -1;
    }
}
