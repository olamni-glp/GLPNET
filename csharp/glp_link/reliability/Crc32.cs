namespace GlpRuntime.Link.Reliability;

/// <summary>
/// Standard IEEE 802.3 / zlib CRC-32 (reflected, polynomial <c>0xEDB88320</c>,
/// init/final <c>0xFFFFFFFF</c>). Used by the frame codec (T021) for outer
/// integrity that the Dart serializer never had (corpus 13 §6). Chosen because it
/// is the universally-specified CRC variant, so the Dart mirror computes
/// byte-identical checksums (FR-060/061) with no shared table to keep in sync.
/// </summary>
public static class Crc32
{
    private const uint Polynomial = 0xEDB88320u;
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? Polynomial ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }

    /// <summary>CRC-32 of <paramref name="data"/>.</summary>
    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data)
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}
