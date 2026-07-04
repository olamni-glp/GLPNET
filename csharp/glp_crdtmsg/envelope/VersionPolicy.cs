// Two-tier version tolerance (feature 041-crdtmsg-mvp, T019).
//
// Contract C5 (envelope-header-registry.md) / FR-007:
//   TIER 1 — envelope: emit-low / accept-range. The encoder stamps the LOWEST version it can
//   (EmitSchemaVersion); a decoder ACCEPTS any version in [Min,Max] (an additive-optional superset)
//   and skips additive-optional fields it does not know (e.g. the v2 capability slot).
//   TIER 2 — frame + term codec: HARD-REJECT an unexpected version byte (no tolerance at the codec
//   format level). The 1-byte CodecFormatVersion is that hard gate.

namespace GlpRuntime.CrdtMsg.Envelope;

public static class VersionPolicy
{
    /// <summary>TIER 2 — the codec container format version. Mismatch = hard reject (no tolerance).</summary>
    public const byte CodecFormatVersion = 0x01;

    /// <summary>TIER 1 — the schema version the encoder stamps (emit-low).</summary>
    public const int EmitSchemaVersion = 1;

    /// <summary>TIER 1 — lowest accepted schema version.</summary>
    public const int MinAcceptSchemaVersion = 1;

    /// <summary>TIER 1 — highest accepted schema version (v2 adds the additive-optional capability slot).</summary>
    public const int MaxAcceptSchemaVersion = 2;

    /// <summary>Hard-reject an unexpected codec format byte (TIER 2, FR-007).</summary>
    public static void CheckCodecFormatVersion(byte v)
    {
        if (v != CodecFormatVersion)
            throw new CrdtMsgException(
                $"Unsupported codec format version 0x{v:X2} (expected 0x{CodecFormatVersion:X2})");
    }

    /// <summary>Accept a schema version only inside the tolerated range (TIER 1, FR-007).</summary>
    public static void CheckSchemaVersionAccepted(int schemaVersion)
    {
        if (schemaVersion < MinAcceptSchemaVersion || schemaVersion > MaxAcceptSchemaVersion)
            throw new CrdtMsgException(
                $"Unsupported schema version {schemaVersion} " +
                $"(accept range [{MinAcceptSchemaVersion},{MaxAcceptSchemaVersion}])");
    }
}
