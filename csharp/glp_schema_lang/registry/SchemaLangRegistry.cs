// Seeded overlay registry (feature 043-xsd-schema-language, T011; research R2).
//
// The E9 substrate (WireRegistry, SchemaRegistry) is a static, compile-time-closed table with
// no Register API — and FR-012 keeps it byte-identical. This instance class is the additive
// construction: it SEEDS itself at construction from WireRegistry.All + SchemaRegistry forms
// and appends 043 registrations to an in-memory overlay; lookups consult seed ∪ overlay as
// one logical registry. In-memory only — no PGLite, no files (the substrate's own persistence
// level). Lookups loud-fail (the corpus law); registration (T017/T031) is all-or-nothing.

using System.Security.Cryptography;
using System.Text;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang;

/// <summary>
/// One registry row (data-model §2). For 043-authored entries the 043 document text IS the
/// qmedit-family authoring form: the same verbatim text is stored under both the
/// <see cref="QmeditDsl"/> and <see cref="XsdSource"/> keys (lowering.md registration law 3).
/// For seeded 041 entries the keys differ: QmeditDsl = the 041 form, XsdSource = null.
/// </summary>
public sealed record RegistryRecord(
    byte PayloadType,
    string Functor,
    CompatMode? CompatMode,
    string? QmeditDsl,
    string? Cddl,
    string? XsdSource,
    string SchemaName,
    int Version,
    string? CddlSha256,
    string? QmeditSha256,
    OverrideRecord? Override = null,
    bool IsSeeded = false);

/// <summary>Ordered version history for one (SchemaName, Functor); transitive modes check the whole chain (R8).</summary>
public sealed record VersionChain(
    string SchemaName,
    string Functor,
    IReadOnlyList<RegistryRecord> Versions);

/// <summary>Union result of registration: records on success, a LoweringError otherwise.</summary>
public sealed record RegisterResult(IReadOnlyList<RegistryRecord>? Records, LoweringError? Error)
{
    public bool IsSuccess => Error is null;
}

public sealed class SchemaLangRegistry
{
    private readonly List<RegistryRecord> _seed = new();
    private readonly List<RegistryRecord> _overlay = new();

    public SchemaLangRegistry()
    {
        foreach (var entry in GlpRuntime.WireRegistry.WireRegistry.All)
        {
            // The static table's dual-DSL forms live in SchemaRegistry (E9); byte-only kinds
            // (il_program, result_envelope) carry no forms.
            string? qmedit = entry.QmeditDsl;
            string? cddl = entry.Cddl;
            if (SchemaRegistry.Has(entry.Functor))
            {
                var forms = SchemaRegistry.Get(entry.Functor);
                qmedit ??= forms.QmeditDsl;
                cddl ??= forms.Cddl;
            }
            _seed.Add(new RegistryRecord(
                entry.PayloadType,
                entry.Functor,
                entry.CompatMode,
                qmedit,
                cddl,
                XsdSource: null,
                SchemaName: entry.Functor,
                Version: 1,
                CddlSha256: cddl is null ? null : Sha256Hex(cddl),
                QmeditSha256: qmedit is null ? null : Sha256Hex(qmedit),
                IsSeeded: true));
        }
    }

    /// <summary>All rows, seed first then overlay, each in declaration/registration order.</summary>
    public IReadOnlyList<RegistryRecord> All => _seed.Concat(_overlay).ToList();

    public bool HasFunctor(string functor) => All.Any(r => r.Functor == functor);

    public bool HasPayloadType(byte payloadType) => All.Any(r => r.PayloadType == payloadType);

    /// <summary>Latest registered version for a functor; loud-fails on an unknown functor (FR-008).</summary>
    public RegistryRecord LookupByFunctor(string functor)
    {
        RegistryRecord? found = null;
        foreach (var record in All)
            if (record.Functor == functor && (found is null || record.Version > found.Version))
                found = record;
        return found ?? throw new NoSchemaRegisteredError(functor);
    }

    /// <summary>Latest registered version for a payload-type byte; loud-fails on an unknown byte.</summary>
    public RegistryRecord LookupByPayloadType(byte payloadType)
    {
        RegistryRecord? found = null;
        foreach (var record in All)
            if (record.PayloadType == payloadType && (found is null || record.Version > found.Version))
                found = record;
        return found ?? throw new NoSchemaRegisteredError($"payload type 0x{payloadType:X2}");
    }

    /// <summary>Ordered version history for a functor; loud-fails on an unknown functor.</summary>
    public VersionChain Versions(string functor)
    {
        var versions = All.Where(r => r.Functor == functor).OrderBy(r => r.Version).ToList();
        if (versions.Count == 0) throw new NoSchemaRegisteredError(functor);
        return new VersionChain(versions[^1].SchemaName, functor, versions);
    }

    /// <summary>Payload-type bytes currently taken in seed ∪ overlay (allocation input, R3).</summary>
    public IReadOnlyCollection<byte> UsedPayloadTypes =>
        All.Select(r => r.PayloadType).ToHashSet();

    // ------------------------------------------------------------------
    // Registration (T017; lowering.md §Registration laws)
    // ------------------------------------------------------------------

    /// <summary>
    /// First registration of a schema document's lowered artifacts. All-or-nothing: any
    /// collision over seed ∪ overlay registers NOTHING and never overwrites (US1 AS-3). The
    /// declared CompatMode is mandatory (clarification 3 — the signature enforces it). Each
    /// RegistryRecord stores {qmedit, cddl, xsd_source, sha256 hashes} together (FR-004, R9);
    /// for 043-authored entries the document text is stored under BOTH the QmeditDsl and
    /// XsdSource keys (registration law 3).
    /// </summary>
    public RegisterResult Register(SchemaDocument doc, LoweringArtifactSet artifacts, CompatMode mode)
    {
        var collisions = new List<string>();
        foreach (var registration in artifacts.Registrations)
        {
            if (HasFunctor(registration.Functor))
            {
                var existing = LookupByFunctor(registration.Functor);
                collisions.Add(
                    $"functor '{registration.Functor}' is already registered " +
                    $"(payload type 0x{existing.PayloadType:X2}, schema '{existing.SchemaName}' v{existing.Version}{(existing.IsSeeded ? ", seeded" : string.Empty)})");
            }
            if (HasPayloadType(registration.PayloadType))
            {
                var existing = LookupByPayloadType(registration.PayloadType);
                collisions.Add(
                    $"payload type 0x{registration.PayloadType:X2} requested for '{registration.Functor}' is already taken by functor '{existing.Functor}'");
            }
        }
        if (collisions.Count > 0)
            return new RegisterResult(null, new LoweringError(
                LoweringErrorKind.Collision,
                collisions,
                $"registration of schema '{doc.Name}' collides with existing registry content — nothing was written"));

        var records = artifacts.Registrations
            .Select(registration => new RegistryRecord(
                registration.PayloadType,
                registration.Functor,
                mode,
                QmeditDsl: doc.Source,
                Cddl: artifacts.Cddl,
                XsdSource: doc.Source,
                SchemaName: doc.Name,
                Version: doc.Version,
                CddlSha256: Sha256Hex(artifacts.Cddl),
                QmeditSha256: Sha256Hex(doc.Source)))
            .ToList();
        _overlay.AddRange(records);
        return new RegisterResult(records, null);
    }

    internal void AppendOverlay(RegistryRecord record) => _overlay.Add(record);

    internal static string Sha256Hex(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
