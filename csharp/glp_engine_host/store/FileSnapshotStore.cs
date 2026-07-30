// FileSnapshotStore — the gitignored plain-file fallback backend (T020;
// contracts/snapshot-store.md "Fallback — File").
//
// Layout (one directory per engine identity):
//   <root>/<engine-identity>/snap-<seq>.gsnp     — the blob
//   <root>/<engine-identity>/manifest.json       — the completeness record, updated LAST
//
// Durable-then-visible (FR-013): the blob is written to a temp name, flushed to
// disk (fsync), and atomically renamed; only THEN is the manifest rewritten
// (itself temp-write → fsync → atomic replace). A crash at ANY point leaves the
// manifest describing only complete snapshots — a torn blob write is an orphan
// file that is never listed, and Latest()/BySeq()/List() consult the manifest
// only.

using System.Text.Json;

namespace GlpRuntime.EngineHost.Store;

public sealed class FileSnapshotStore : ISnapshotBackend
{
    private readonly string _dir;
    private readonly string _manifestPath;

    public string Name => "file";

    public FileSnapshotStore(string rootDir, string engineIdentity)
    {
        _dir = Path.Combine(rootDir, Sanitize(engineIdentity));
        Directory.CreateDirectory(_dir);
        _manifestPath = Path.Combine(_dir, "manifest.json");
    }

    // ---------------------------------------------------------------- manifest

    private sealed record ManifestEntry(ulong Seq, long CreatedUtcMs, long Size, int FormatVersion);

    private List<ManifestEntry> ReadManifest()
    {
        if (!File.Exists(_manifestPath))
            return new List<ManifestEntry>();
        var json = File.ReadAllText(_manifestPath);
        return JsonSerializer.Deserialize<List<ManifestEntry>>(json)
            ?? throw new SnapshotStoreException($"corrupt snapshot manifest at {_manifestPath}");
    }

    private void WriteManifestLast(List<ManifestEntry> entries)
    {
        var tmp = _manifestPath + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(
                entries, new JsonSerializerOptions { WriteIndented = true });
            fs.Write(bytes);
            fs.Flush(flushToDisk: true); // fsync before the atomic replace
        }
        File.Move(tmp, _manifestPath, overwrite: true); // atomic on NTFS
    }

    // ---------------------------------------------------------------- backend

    public ulong MaxSeq()
    {
        var manifest = ReadManifest();
        return manifest.Count == 0 ? 0UL : manifest.Max(e => e.Seq);
    }

    public void Write(ulong seq, long createdUtcMs, int formatVersion, byte[] blob)
    {
        var manifest = ReadManifest();
        if (manifest.Any(e => e.Seq == seq))
            throw new SnapshotStoreException(
                $"snapshot seq {seq} already exists in the file store (monotonic-seq violation)");

        // 1. Blob: temp-write → fsync → atomic rename. Overwrite is SAFE and
        // necessary: the manifest is the only completeness record, so a blob file
        // at this path can only be an ORPHAN from a crash between blob-rename and
        // manifest-write (codexreview 20260730T070051Z: overwrite:false made that
        // orphan permanently block every retry of the seq — snapshots wedged).
        var blobPath = BlobPath(seq);
        var tmp = blobPath + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(blob);
            fs.Flush(flushToDisk: true);
        }
        File.Move(tmp, blobPath, overwrite: true);

        // 2. Manifest LAST — the completeness commit point (FR-013).
        manifest.Add(new ManifestEntry(seq, createdUtcMs, blob.LongLength, formatVersion));
        manifest.Sort((a, b) => a.Seq.CompareTo(b.Seq));
        WriteManifestLast(manifest);
    }

    public (ulong Seq, byte[] Blob)? Latest()
    {
        var manifest = ReadManifest();
        if (manifest.Count == 0) return null;
        var top = manifest.OrderByDescending(e => e.Seq).First();
        return (top.Seq, ReadBlobChecked(top));
    }

    public byte[]? BySeq(ulong seq)
    {
        var entry = ReadManifest().FirstOrDefault(e => e.Seq == seq);
        return entry is null ? null : ReadBlobChecked(entry);
    }

    public IReadOnlyList<SnapshotMeta> List() =>
        ReadManifest()
            .OrderBy(e => e.Seq)
            .Select(e => new SnapshotMeta(e.Seq, e.CreatedUtcMs, e.Size, e.FormatVersion))
            .ToList();

    private byte[] ReadBlobChecked(ManifestEntry entry)
    {
        var path = BlobPath(entry.Seq);
        if (!File.Exists(path))
            throw new SnapshotStoreException(
                $"manifest lists seq {entry.Seq} but {path} is missing (store corruption)");
        var bytes = File.ReadAllBytes(path);
        if (bytes.LongLength != entry.Size)
            throw new SnapshotStoreException(
                $"seq {entry.Seq}: blob size {bytes.LongLength} disagrees with the manifest ({entry.Size})");
        return bytes;
    }

    private string BlobPath(ulong seq) => Path.Combine(_dir, $"snap-{seq}.gsnp");

    private static string Sanitize(string engineIdentity)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(engineIdentity.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
