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

/// <summary>Union result of registration: records on success, a LoweringError or the schema
/// validation error list (FR-002/FR-014 — an invalid document never registers) otherwise.</summary>
public sealed record RegisterResult(
    IReadOnlyList<RegistryRecord>? Records,
    LoweringError? Error,
    IReadOnlyList<SchemaValidationError>? SchemaErrors = null)
{
    public bool IsSuccess => Error is null && SchemaErrors is null;
}

/// <summary>Union result of a version check: a verdict, or a refusal record (clarification 3 /
/// version-monotonicity law / schema validity — checks run over VALIDATED documents only).</summary>
public sealed record CheckVersionResult(
    CompatVerdict? Verdict,
    NoCompatModeDeclaredError? NoModeError,
    VersionNotMonotonicError? VersionError = null,
    IReadOnlyList<SchemaValidationError>? SchemaErrors = null);

/// <summary>Union result of a version registration (contracts/compat-evolution.md §API).</summary>
public sealed record RegisterVersionResult(
    IReadOnlyList<RegistryRecord>? Records,
    NoCompatModeDeclaredError? NoModeError,
    CompatVerdict? RequiresOverride,
    VersionNotMonotonicError? VersionError = null,
    IReadOnlyList<SchemaValidationError>? SchemaErrors = null);

/// <summary>
/// Seeded overlay registry over the static E9 tables.
/// </summary>
/// <remarks>Single-threaded by contract: instances mutate plain in-memory lists with no
/// synchronization and are NOT safe for concurrent use — callers own any cross-thread
/// coordination (matching the substrate's compile-time-closed, single-writer usage).</remarks>
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
    /// First registration of a schema document's lowered artifacts. The document is
    /// re-validated at entry — an invalid document refuses with its schema-error list and
    /// writes nothing. All-or-nothing: any collision (functor, payload-type byte, or schema
    /// name) over seed ∪ overlay registers NOTHING and never overwrites (US1 AS-3). The
    /// declared CompatMode is mandatory (clarification 3 — the signature enforces it). Each
    /// RegistryRecord stores {qmedit, cddl, xsd_source, sha256 hashes} together (FR-004, R9);
    /// for 043-authored entries the document text is stored under BOTH the QmeditDsl and
    /// XsdSource keys (registration law 3).
    /// </summary>
    public RegisterResult Register(SchemaDocument doc, LoweringArtifactSet artifacts, CompatMode mode)
    {
        // No unvalidated document ever reaches the overlay (FR-002/FR-014): the registration
        // entry point re-validates so a later ParseStoredXsd can never blame a "registry
        // invariant broken" for what was a schema-validation failure at registration time.
        if (SchemaErrorsOf(doc) is { } schemaErrors)
            return new RegisterResult(null, null, schemaErrors);

        // One combined lookup index per Register call (latest version wins, first row scanned
        // breaks ties — the LookupByFunctor/LookupByPayloadType law) instead of re-materializing
        // seed ∪ overlay once per registration.
        var byFunctor = new Dictionary<string, RegistryRecord>();
        var byPayloadType = new Dictionary<byte, RegistryRecord>();
        RegistryRecord? sameSchemaName = null;
        foreach (var record in _seed.Concat(_overlay))
        {
            if (!byFunctor.TryGetValue(record.Functor, out var f) || record.Version > f.Version)
                byFunctor[record.Functor] = record;
            if (!byPayloadType.TryGetValue(record.PayloadType, out var p) || record.Version > p.Version)
                byPayloadType[record.PayloadType] = record;
            if (record.SchemaName == doc.Name && (sameSchemaName is null || record.Version > sameSchemaName.Version))
                sameSchemaName = record;
        }

        var collisions = new List<string>();
        // A schema name colliding with an already-registered one (spec edge case): first
        // registration requires a fresh schema identity — new versions of an existing schema
        // go through RegisterVersion, which legitimately re-uses the name.
        if (sameSchemaName is not null)
            collisions.Add(
                $"schema name '{doc.Name}' is already registered " +
                $"(functor '{sameSchemaName.Functor}' v{sameSchemaName.Version}{(sameSchemaName.IsSeeded ? ", seeded" : string.Empty)}) — " +
                "a first registration requires a fresh schema name; use RegisterVersion to evolve an existing schema");
        var newFunctors = new HashSet<string>();
        var newPayloadTypes = new HashSet<byte>();
        foreach (var registration in artifacts.Registrations)
        {
            if (byFunctor.TryGetValue(registration.Functor, out var existing))
            {
                collisions.Add(
                    $"functor '{registration.Functor}' is already registered " +
                    $"(payload type 0x{existing.PayloadType:X2}, schema '{existing.SchemaName}' v{existing.Version}{(existing.IsSeeded ? ", seeded" : string.Empty)})");
            }
            if (byPayloadType.TryGetValue(registration.PayloadType, out var taken))
            {
                collisions.Add(
                    $"payload type 0x{registration.PayloadType:X2} requested for '{registration.Functor}' is already taken by functor '{taken.Functor}'");
            }
            // Duplicates WITHIN one artifact set are the same collision class: registering both
            // rows would create ambiguous lookups (all-or-nothing, US1 AS-3).
            if (!newFunctors.Add(registration.Functor))
                collisions.Add(
                    $"functor '{registration.Functor}' appears more than once in the artifact set — duplicate registration within one document");
            if (!newPayloadTypes.Add(registration.PayloadType))
                collisions.Add(
                    $"payload type 0x{registration.PayloadType:X2} requested for '{registration.Functor}' appears more than once in the artifact set");
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

    // ------------------------------------------------------------------
    // Versioned registration (T031; compat-evolution.md §API, FR-011)
    // ------------------------------------------------------------------

    /// <summary>
    /// Evolution check of a proposed new version against the type's DECLARED mode. Refusal
    /// law (clarification 3): a type with no declared mode refuses with an explicit
    /// NoCompatModeDeclaredError — never a silently assumed default. Under Transitive the
    /// check runs against EVERY stored version in the chain; the first failing pair is the
    /// verdict returned.
    /// </summary>
    public CheckVersionResult CheckVersion(SchemaDocument newDoc)
    {
        // Schema validity first (FR-002/FR-014): evolution checks are defined over VALIDATED
        // documents — an added element with an unresolved type ref or a brand-new invalid kind
        // is never resolved by the common-element comparison below, so an unvalidated document
        // must refuse HERE, before anything can be stored and later detonate downstream.
        if (SchemaErrorsOf(newDoc) is { } schemaErrors)
            return new CheckVersionResult(null, null, null, schemaErrors);

        // Version monotonicity (FR-014): the proposed version must strictly exceed the latest
        // stored version of every previously-registered kind — refused before any comparison.
        if (VersionMonotonicityError(newDoc) is { } versionError)
            return new CheckVersionResult(null, null, versionError);

        CompatVerdict? lastVerdict = null;
        foreach (var message in newDoc.Messages)
        {
            // A kind present only in the new document is additive — compatible by definition
            // (the CompatChecker law); it enters the registry as a first registration.
            if (!HasFunctor(message.Functor)) continue;

            var chain = Versions(message.Functor);
            var latest = chain.Versions[^1];
            if (latest.CompatMode is not GlpRuntime.WireRegistry.CompatMode mode)
                return new CheckVersionResult(null, new NoCompatModeDeclaredError(message.Functor));

            var toCheck = mode == GlpRuntime.WireRegistry.CompatMode.Transitive
                ? chain.Versions
                : new[] { latest };
            foreach (var version in toCheck)
            {
                var verdict = CompatChecker.Check(ParseStoredXsd(version), newDoc, mode);
                lastVerdict = verdict;
                if (!verdict.IsCompatible)
                    return new CheckVersionResult(verdict, null); // first failing pair named
            }
        }
        return new CheckVersionResult(
            lastVerdict ?? throw new InvalidOperationException(
                newDoc.Messages.Count == 0
                    ? "CheckVersion requires a document with at least one message"
                    : $"no message kind in schema '{newDoc.Name}' has a registered version — use Register for a first registration"),
            null);
    }

    /// <summary>Non-null iff the document fails schema validation (FR-002). Every document
    /// entry point (Register / CheckVersion / RegisterVersion / RegisterVersionWithOverride)
    /// refuses on these errors, so no unvalidated document can reach the overlay — the error
    /// attribution names schema validation, never a broken registry invariant (FR-014).</summary>
    private static IReadOnlyList<SchemaValidationError>? SchemaErrorsOf(SchemaDocument doc)
    {
        var validated = SchemaValidator.ValidateDocument(doc);
        return validated.IsValid ? null : validated.Errors;
    }

    /// <summary>Non-null iff the proposed document's version does not strictly exceed the
    /// latest stored version of some previously-registered kind it declares.</summary>
    private VersionNotMonotonicError? VersionMonotonicityError(SchemaDocument newDoc)
    {
        foreach (var message in newDoc.Messages)
        {
            if (!HasFunctor(message.Functor)) continue;
            var latest = LookupByFunctor(message.Functor);
            if (newDoc.Version <= latest.Version)
                return new VersionNotMonotonicError(message.Functor, newDoc.Version, latest.Version);
        }
        return null;
    }

    /// <summary>
    /// Register a new version: refuses without a declared mode; an incompatible verdict is
    /// returned as RequiresOverride and NOTHING is written (US4 AS-3). A new version of a
    /// kind keeps the kind's payload-type byte.
    /// </summary>
    public RegisterVersionResult RegisterVersion(SchemaDocument newDoc, LoweringArtifactSet artifacts)
    {
        var check = CheckVersion(newDoc);
        if (check.SchemaErrors is not null)
            return new RegisterVersionResult(null, null, null, null, check.SchemaErrors);
        if (check.VersionError is not null)
            return new RegisterVersionResult(null, null, null, check.VersionError);
        if (check.NoModeError is not null)
            return new RegisterVersionResult(null, check.NoModeError, null);
        if (!check.Verdict!.IsCompatible)
            return new RegisterVersionResult(null, null, check.Verdict);
        return new RegisterVersionResult(AppendVersionRecords(newDoc, artifacts, null), null, null);
    }

    /// <summary>
    /// Register an INCOMPATIBLE version with an explicit recorded override; the record is
    /// stored on the RegistryRecord and retrievable with it. The no-declared-mode refusal
    /// still applies — an override does not substitute for a declared mode.
    /// </summary>
    public IReadOnlyList<RegistryRecord> RegisterVersionWithOverride(
        SchemaDocument newDoc,
        LoweringArtifactSet artifacts,
        OverrideRecord overrideRecord)
    {
        // An override acknowledges an INCOMPATIBILITY; it does not license an invalid document.
        if (SchemaErrorsOf(newDoc) is { } schemaErrors)
            throw new InvalidOperationException(
                $"schema '{newDoc.Name}' does not validate — " +
                $"{schemaErrors.Count} schema error(s): {string.Join("; ", schemaErrors)}");
        if (VersionMonotonicityError(newDoc) is { } versionError)
            throw new InvalidOperationException(versionError.Message);
        var anyRegistered = false;
        foreach (var message in newDoc.Messages)
        {
            if (!HasFunctor(message.Functor)) continue; // additive new kind — first registration
            anyRegistered = true;
            var latest = Versions(message.Functor).Versions[^1];
            if (latest.CompatMode is null)
                throw new InvalidOperationException(new NoCompatModeDeclaredError(message.Functor).Message);
            EnsureNoDrift(latest);
        }
        if (!anyRegistered)
            throw new InvalidOperationException(
                $"no message kind in schema '{newDoc.Name}' has a registered version — use Register for a first registration");
        return AppendVersionRecords(newDoc, artifacts, overrideRecord);
    }

    private IReadOnlyList<RegistryRecord> AppendVersionRecords(
        SchemaDocument doc,
        LoweringArtifactSet artifacts,
        OverrideRecord? overrideRecord)
    {
        // A kind added by this version enters as a first registration: it takes the byte the
        // lowering artifact allocated against the live registry and inherits the document's
        // declared mode — read from the first previously-registered kind in the document
        // (all-new documents are refused upstream: they must go through Register).
        GlpRuntime.WireRegistry.CompatMode? inheritedMode = null;
        foreach (var registration in artifacts.Registrations)
            if (HasFunctor(registration.Functor))
            {
                inheritedMode = LookupByFunctor(registration.Functor).CompatMode;
                break;
            }

        var records = artifacts.Registrations
            .Select(registration =>
            {
                var existing = HasFunctor(registration.Functor)
                    ? LookupByFunctor(registration.Functor)
                    : null;
                if (existing is null && HasPayloadType(registration.PayloadType))
                    throw new InvalidOperationException(
                        $"payload type 0x{registration.PayloadType:X2} requested for new kind '{registration.Functor}' is already taken — lower the document against the live registry");
                return new RegistryRecord(
                    existing?.PayloadType ?? registration.PayloadType, // the kind keeps its byte across versions
                    registration.Functor,
                    existing?.CompatMode ?? inheritedMode,
                    QmeditDsl: doc.Source,
                    Cddl: artifacts.Cddl,
                    XsdSource: doc.Source,
                    SchemaName: doc.Name,
                    Version: doc.Version,
                    CddlSha256: Sha256Hex(artifacts.Cddl),
                    QmeditSha256: Sha256Hex(doc.Source),
                    Override: overrideRecord);
            })
            .ToList();
        _overlay.AddRange(records);
        return records;
    }

    private static SchemaDocument ParseStoredXsd(RegistryRecord record)
    {
        if (record.XsdSource is null)
            throw new InvalidOperationException(
                $"cannot evolution-check '{record.Functor}' v{record.Version}: the stored entry has no " +
                "XSD-level source — lift and re-register it through the 043 layer first");
        EnsureNoDrift(record); // FR-013: drift refuses BEFORE any compat comparison
        var validated = SchemaValidator.Validate(record.XsdSource);
        if (!validated.IsValid)
            throw new InvalidOperationException(
                $"registry invariant broken: stored XSD source for '{record.Functor}' v{record.Version} no longer validates");
        return validated.Document!;
    }

    /// <summary>FR-013 on the evolution path: a drifted stored form (out-of-band edit) refuses
    /// loudly — evolution must never compare against stale registry truth.</summary>
    private static void EnsureNoDrift(RegistryRecord record)
    {
        if (Lifter.ComputeDrift(record) is not { } drift) return;
        throw new InvalidOperationException(
            $"stored {(drift.Form == RegistryForm.Cddl ? "CDDL" : "qmedit")} form for '{record.Functor}' v{record.Version} has drifted out-of-band " +
            $"(registration-time sha256 {drift.StoredSha256} != current {drift.CurrentSha256}) — resolve the drift before evolution-checking or registering a new version");
    }

    internal void AppendOverlay(RegistryRecord record) => _overlay.Add(record);

    /// <summary>TEST SEAM (T024): mimic an out-of-band E9-level CDDL edit — the stored text
    /// changes but the registration-time hash record does not (drift detection input, R9).</summary>
    internal void MutateOverlayCddlOutOfBand(string functor, string newCddl) =>
        MutateOverlay(functor, r => r with { Cddl = newCddl });

    /// <summary>TEST SEAM (T024): mimic an out-of-band qmedit-form edit.</summary>
    internal void MutateOverlayQmeditOutOfBand(string functor, string newQmedit) =>
        MutateOverlay(functor, r => r with { QmeditDsl = newQmedit });

    private void MutateOverlay(string functor, Func<RegistryRecord, RegistryRecord> mutate)
    {
        for (var i = _overlay.Count - 1; i >= 0; i--)
        {
            if (_overlay[i].Functor != functor) continue;
            _overlay[i] = mutate(_overlay[i]);
            return;
        }
        throw new NoSchemaRegisteredError(functor);
    }

    internal static string Sha256Hex(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
