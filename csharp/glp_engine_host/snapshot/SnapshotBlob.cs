// SnapshotBlob — format_version-1 blob layout (T017; contracts/snapshot-store.md).
//
//   header   { magic 'GSNP', format_version varint, engine_identity str,
//              created_utc i64, seq varint }
//   sections { section_tag u8, length varint, bytes } … to end of blob
//
// Byte conventions are the 029/038 house style via the shipped
// GlpRuntime.ResultCodec ByteWriter/ByteReader (LEB128 varints, LE i64,
// varint+UTF-8 strings) — one ByteIo implementation, not two (VIII).
//
// Loud-fail discipline (contract): unknown section tag, duplicate tag,
// truncated section, or trailing bytes ⇒ SnapshotException. Restore verifies
// section COMPLETENESS via RequireAllSections before the engine leaves
// `restoring` (FR-030).

using GlpRuntime.ResultCodec;

namespace GlpRuntime.EngineHost.Snapshot;

/// <summary>Loud-fail exception for any malformed snapshot blob (corrupt-snapshot taxonomy input).</summary>
public sealed class SnapshotException : Exception
{
    public SnapshotException(string message) : base(message) { }
    public SnapshotException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>The section tags of format_version 1 (contracts/snapshot-store.md).</summary>
public static class SnapshotSection
{
    public const byte HeapCells = 0x01;
    public const byte GoalQueue = 0x02;
    public const byte SuspendedGoals = 0x03;
    public const byte NextGoalId = 0x04;
    public const byte LoadedIlUnits = 0x05;
    public const byte Timers = 0x06;
    public const byte InfrastructureGoalIds = 0x07;
    public const byte GlpChannels = 0x08;
    public const byte LinkDefinitions = 0x09;

    /// <summary>Every tag of format_version 1, ascending.</summary>
    public static readonly byte[] All =
    {
        HeapCells, GoalQueue, SuspendedGoals, NextGoalId, LoadedIlUnits,
        Timers, InfrastructureGoalIds, GlpChannels, LinkDefinitions,
    };
}

/// <summary>
/// Heap-cell content variants inside section 0x01 (shared by SnapshotCapture's
/// encoder and SnapshotRestore's decoder — one tag table, two directions).
/// </summary>
public static class SnapshotCellContent
{
    public const byte Null = 0x00;
    public const byte Pointer = 0x01;
    public const byte SuspensionChain = 0x02;
    public const byte Term = 0x03;
    public const byte WriterContent = 0x04;
}

/// <summary>
/// Runtime-term subtags inside section 0x01/0x03 payloads. Int32 and Int64 are
/// DISTINCT on purpose: <c>object.Equals(2, 2L)</c> is false, so restoring an
/// Int32 constant as Int64 would silently change guard results — type fidelity
/// is load-bearing.
/// </summary>
public static class SnapshotTermTag
{
    public const byte ConstNull = 0x00;
    public const byte ConstInt32 = 0x01;
    public const byte ConstInt64 = 0x02;
    public const byte ConstDouble = 0x03;
    public const byte ConstString = 0x04;
    public const byte ConstBool = 0x05;
    public const byte Struct = 0x06;
    public const byte VarRef = 0x07;
    public const byte MutualRef = 0x08;
    public const byte Module = 0x09;
}

/// <summary>
/// Per-goal program-key descriptors inside section 0x03 (which runner a goal's
/// program key resolves to after restore).
/// </summary>
public static class SnapshotProgramKey
{
    public const byte None = 0x00;       // GetGoalProgram(...) == null
    public const byte Name = 0x01;       // string key (e.g. "main")
    public const byte Serve = 0x02;      // the engine's serve/2 bytecode object
    public const byte Module = 0x03;     // a module's merged (module ⊕ root-self) bytecode
}

/// <summary>
/// One decoded (or to-be-encoded) snapshot: versioned header + the section
/// payload bytes keyed by tag. The section payloads are opaque at this layer;
/// SnapshotCapture writes them and SnapshotRestore reads them.
/// </summary>
public sealed class SnapshotBlob
{
    public const uint Magic = 0x504E5347; // 'GSNP' little-endian ("G","S","N","P")
    public const int FormatVersion1 = 1;

    public int FormatVersion { get; }
    public string EngineIdentity { get; }
    public long CreatedUtcMs { get; }
    public ulong Seq { get; }
    public IReadOnlyDictionary<byte, byte[]> Sections { get; }

    public SnapshotBlob(
        int formatVersion,
        string engineIdentity,
        long createdUtcMs,
        ulong seq,
        IReadOnlyDictionary<byte, byte[]> sections)
    {
        FormatVersion = formatVersion;
        EngineIdentity = engineIdentity;
        CreatedUtcMs = createdUtcMs;
        Seq = seq;
        Sections = sections;
    }

    /// <summary>FR-030: loud-fail unless every format_version-1 section is present.</summary>
    public void RequireAllSections()
    {
        foreach (var tag in SnapshotSection.All)
            if (!Sections.ContainsKey(tag))
                throw new SnapshotException(
                    $"incomplete snapshot: section 0x{tag:X2} is missing (FR-030 completeness check)");
    }

    /// <summary>The payload of one section; loud-fail when absent.</summary>
    public byte[] Section(byte tag) =>
        Sections.TryGetValue(tag, out var bytes)
            ? bytes
            : throw new SnapshotException($"incomplete snapshot: section 0x{tag:X2} is missing");

    // ---------------------------------------------------------------- encode

    public byte[] Encode()
    {
        var w = new ByteWriter();
        w.WriteByte((byte)'G');
        w.WriteByte((byte)'S');
        w.WriteByte((byte)'N');
        w.WriteByte((byte)'P');
        w.WriteVarUInt(FormatVersion);
        w.WriteString(EngineIdentity);
        w.WriteInt64LE(CreatedUtcMs);
        if (Seq > long.MaxValue)
            throw new SnapshotException($"snapshot seq {Seq} exceeds the encodable range");
        w.WriteInt64LE((long)Seq);

        // Ascending tag order — deterministic bytes for the round-trip test.
        foreach (var tag in Sections.Keys.OrderBy(t => t))
        {
            var payload = Sections[tag];
            w.WriteByte(tag);
            w.WriteVarUInt(payload.Length);
            w.WriteBytes(payload);
        }
        return w.TakeBytes();
    }

    // ---------------------------------------------------------------- decode

    public static SnapshotBlob Decode(byte[] bytes)
    {
        var r = new ByteReader(bytes);
        try
        {
            var magic = r.ReadBytes(4);
            if (magic[0] != 'G' || magic[1] != 'S' || magic[2] != 'N' || magic[3] != 'P')
                throw new SnapshotException(
                    $"not a snapshot blob: bad magic 0x{magic[0]:X2}{magic[1]:X2}{magic[2]:X2}{magic[3]:X2} (want 'GSNP')");

            int formatVersion = r.ReadVarUInt();
            if (formatVersion != FormatVersion1)
                throw new SnapshotException(
                    $"unsupported snapshot format_version {formatVersion} (this build reads {FormatVersion1})");

            string engineIdentity = r.ReadString();
            long createdUtcMs = r.ReadInt64LE();
            long seqRaw = r.ReadInt64LE();
            if (seqRaw < 0)
                throw new SnapshotException($"corrupt snapshot: negative seq {seqRaw}");

            var sections = new Dictionary<byte, byte[]>();
            while (!r.AtEnd)
            {
                byte tag = r.ReadByte();
                if (Array.IndexOf(SnapshotSection.All, tag) < 0)
                    throw new SnapshotException(
                        $"corrupt snapshot: unknown section tag 0x{tag:X2} (format_version {formatVersion})");
                if (sections.ContainsKey(tag))
                    throw new SnapshotException($"corrupt snapshot: duplicate section tag 0x{tag:X2}");
                int len = r.ReadVarUInt();
                sections[tag] = r.ReadBytes(len);
            }
            // ByteReader bounds-checks every read, so reaching here with AtEnd true
            // means no trailing bytes remain by construction.

            return new SnapshotBlob(formatVersion, engineIdentity, createdUtcMs, (ulong)seqRaw, sections);
        }
        catch (ResultCodecException ex)
        {
            // Truncated read / overlong varint from the ByteIo layer → the same loud
            // corrupt-snapshot surface the taxonomy consumes.
            throw new SnapshotException($"corrupt snapshot: {ex.Message}", ex);
        }
    }
}
