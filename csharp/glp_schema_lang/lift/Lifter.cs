// Lifter (feature 043-xsd-schema-language, T026; research R9/R10).
//
// Contract: specs/043-xsd-schema-language/contracts/lift-fidelity.md.
//   Law 1 — the lift source is the REGISTERED CDDL artifact (qmedit is the human surface).
//   Law 2 — never approximate: out-of-subset constructs are per-construct report entries.
//   Law 3 — entries with no CDDL artifact lift to a whole-entry Partial report.
//   Law 4 — Lift(Lower(doc)) is structurally equivalent modulo canonical naming (FR-010).
//   Drift  — stored registration-time hashes are compared against the CURRENT forms on every
//            lift of an entry with stored XSD source; the rendering is built from current
//            registry truth, never the stale stored source (FR-013, R9).
//
// Canonical naming: CDDL rule names become UpperCamel type names ('crdt-model' → 'CrdtModel');
// an inline `uint` synthesizes the canonical simple type `Uint: int { min 0 }`. The size-control
// upper sentinel (CddlEmitter.NoUpperSizeBound) maps back to an absent maxLength, and a lower
// bound of 0 maps to an absent minLength — the exact inverses of the emitter's defaults.

namespace GlpRuntime.SchemaLang;

public static class Lifter
{
    public static LiftResult Lift(SchemaLangRegistry registry, string functor)
    {
        var record = registry.LookupByFunctor(functor); // throws NoSchemaRegisteredError
        var drift = ComputeDrift(record);

        if (record.Cddl is null)
            return new LiftResult(
                null,
                new FidelityReport(FidelityOutcome.Partial, new[]
                {
                    new UnexpressibleConstruct(
                        $"functor '{functor}' (payload type 0x{record.PayloadType:X2})",
                        "registry entry",
                        "no CDDL artifact — byte+functor registration only"),
                }),
                drift);

        var parsed = CddlSubsetParser.Parse(record.Cddl);
        var unexpressible = new List<UnexpressibleConstruct>(parsed.Unexpressible);

        var messageRuleName = CddlEmitter.RuleName(functor);
        var messageRule = parsed.Rules.FirstOrDefault(r => r.Name == messageRuleName);
        if (messageRule is null)
        {
            unexpressible.Add(new UnexpressibleConstruct(
                messageRuleName, "CDDL artifact",
                $"the artifact contains no rule '{messageRuleName}' for functor '{functor}'"));
            return new LiftResult(null, new FidelityReport(FidelityOutcome.Partial, unexpressible), drift);
        }

        // Sibling message rules (other registered functors sharing THIS record's CDDL
        // artifact) are their own registry entries, not constructs of THIS entry — excluded
        // from the rendering. The exclusion is scoped to the artifact: an UNRELATED document's
        // functor whose rule name happens to equal a helper rule here must not suppress the
        // helper (that would fail re-validation and mis-report a legal artifact as Partial,
        // violating lift law 1 / FR-010).
        var functorRuleNames = registry.All
            .Where(r => r.Cddl == record.Cddl)
            .Select(r => CddlEmitter.RuleName(r.Functor))
            .ToHashSet();

        var builder = new Builder(parsed.Rules, unexpressible);
        var message = builder.BuildMessage(functor, messageRule);
        foreach (var rule in parsed.Rules)
            if (rule.Name != messageRuleName && !functorRuleNames.Contains(rule.Name))
                builder.BuildType(rule);

        if (message is null)
            return new LiftResult(null, new FidelityReport(FidelityOutcome.Partial, unexpressible), drift);

        var doc = new SchemaDocument(
            record.SchemaName,
            record.Version,
            builder.Types,
            new[] { message },
            Source: string.Empty);
        doc = doc with { Source = SchemaDslPrinter.Print(doc) };

        var validated = SchemaValidator.ValidateDocument(doc);
        if (!validated.IsValid)
        {
            foreach (var error in validated.Errors)
                unexpressible.Add(new UnexpressibleConstruct(error.Construct, error.Location.ToString(),
                    $"lifted rendering does not re-validate: {error.Message}"));
            return new LiftResult(null, new FidelityReport(FidelityOutcome.Partial, unexpressible), drift);
        }

        var fidelity = unexpressible.Count == 0
            ? FidelityReport.Full
            : new FidelityReport(FidelityOutcome.Partial, unexpressible);
        return new LiftResult(doc, fidelity, drift);
    }

    /// <summary>Drift check shared with the registry's evolution paths (FR-013). At most ONE
    /// report per call: the CDDL form (the formal form) takes precedence over qmedit when both
    /// have drifted (lift-fidelity.md §Drift detection).</summary>
    internal static DriftReport? ComputeDrift(RegistryRecord record)
    {
        // Drift detection is defined over entries that carry stored XSD-level source (FR-013).
        if (record.XsdSource is null) return null;
        if (record.Cddl is not null && record.CddlSha256 is not null)
        {
            var current = SchemaLangRegistry.Sha256Hex(record.Cddl);
            if (current != record.CddlSha256)
                return new DriftReport(RegistryForm.Cddl, record.CddlSha256, current);
        }
        if (record.QmeditDsl is not null && record.QmeditSha256 is not null)
        {
            var current = SchemaLangRegistry.Sha256Hex(record.QmeditDsl);
            if (current != record.QmeditSha256)
                return new DriftReport(RegistryForm.Qmedit, record.QmeditSha256, current);
        }
        return null;
    }

    // ------------------------------------------------------------------
    // CDDL mini-AST → schema AST
    // ------------------------------------------------------------------

    private sealed class UnexpressibleException : Exception
    {
        public UnexpressibleException(string construct, string reason) : base(reason) => Construct = construct;

        public string Construct { get; }
    }

    private sealed class Builder
    {
        private readonly IReadOnlyList<CddlRule> _rules;
        private readonly HashSet<string> _ruleNames;
        private readonly List<UnexpressibleConstruct> _unexpressible;
        private readonly List<NamedType> _types = new();
        private bool _uintSynthesized;

        public Builder(IReadOnlyList<CddlRule> rules, List<UnexpressibleConstruct> unexpressible)
        {
            _rules = rules;
            _ruleNames = rules.Select(r => r.Name).ToHashSet();
            _unexpressible = unexpressible;
        }

        public IReadOnlyList<NamedType> Types => _types;

        public MessageDecl? BuildMessage(string functor, CddlRule rule)
        {
            var composition = BuildComposition(rule);
            return composition is null ? null : new MessageDecl(functor, composition, Location(rule.Line));
        }

        public void BuildType(CddlRule rule)
        {
            var name = CanonicalTypeName(rule.Name);
            var location = Location(rule.Line);
            try
            {
                switch (rule.Type)
                {
                    case CddlMap or CddlChoice:
                        var composition = BuildComposition(rule);
                        if (composition is not null)
                            _types.Add(new ComplexType(name, location, composition));
                        break;
                    case CddlEnum en:
                    {
                        for (var i = 0; i < en.Members.Count; i++)
                            if (en.Members[i].Index != i)
                                throw new UnexpressibleException(rule.Name,
                                    "enum indices are not the declaration order 0..n-1");
                        _types.Add(new SimpleType(name, location, PrimitiveKind.Str, new Facet[]
                        {
                            new EnumerationFacet(en.Members.Select(m => m.Name).ToList(), location),
                        }));
                        break;
                    }
                    case CddlIntAlternates alts:
                        _types.Add(new SimpleType(name, location, PrimitiveKind.Int, new Facet[]
                        {
                            new EnumerationFacet(alts.Values.Select(v => v.ToString()).ToList(), location),
                        }));
                        break;
                    case CddlRange range:
                        _types.Add(new SimpleType(name, location, PrimitiveKind.Int, new Facet[]
                        {
                            new MinValueFacet(range.Lo, location),
                            new MaxValueFacet(range.Hi, location),
                        }));
                        break;
                    case CddlPrim prim:
                        _types.Add(BuildSimpleFromPrim(name, location, prim));
                        break;
                    default:
                        throw new UnexpressibleException(rule.Name, "rule form has no 043 counterpart");
                }
            }
            catch (UnexpressibleException ex)
            {
                _unexpressible.Add(new UnexpressibleConstruct(
                    ex.Construct, $"line {rule.Line}",
                    $"rule '{rule.Name}': {ex.Message}"));
            }
        }

        private SimpleType BuildSimpleFromPrim(string name, SourceLocation location, CddlPrim prim)
        {
            var facets = new List<Facet>();
            var baseKind = prim.Base switch
            {
                "int" => PrimitiveKind.Int,
                "uint" => PrimitiveKind.Int,
                "tstr" => PrimitiveKind.Str,
                "bstr" => PrimitiveKind.Bytes,
                "bool" => PrimitiveKind.Bool,
                _ => throw new UnexpressibleException(prim.Base, "unknown primitive base"),
            };
            if (prim.Base == "uint")
                facets.Add(new MinValueFacet(0, location));
            if (prim.Size is var (lo, hi) && prim.Size is not null)
            {
                // Facet values are int; an out-of-range bound is UNEXPRESSIBLE — a per-construct
                // fidelity entry (FR-009), never a crash and never a silent wrap (lift law 2).
                if (lo > int.MaxValue)
                    throw new UnexpressibleException($".size ({lo}..{hi})",
                        ".size bound out of representable range");
                if (lo > 0) facets.Add(new MinLengthFacet((int)lo, location));
                if (hi.ToString() != CddlEmitter.NoUpperSizeBound)
                {
                    if (hi > int.MaxValue)
                        throw new UnexpressibleException($".size ({lo}..{hi})",
                            ".size bound out of representable range");
                    facets.Add(new MaxLengthFacet((int)hi, location));
                }
            }
            if (prim.Regexp is not null)
                facets.Add(new PatternFacet(prim.Regexp, location));
            return new SimpleType(name, location, baseKind, facets);
        }

        private Composition? BuildComposition(CddlRule rule)
        {
            switch (rule.Type)
            {
                case CddlMap map:
                    return new Composition(CompositionKind.Sequence, BuildElements(map, rule.Line));
                case CddlChoice choice:
                {
                    var elements = new List<ElementDecl>();
                    foreach (var branch in choice.Branches)
                    {
                        if (branch.Entries.Count != 1)
                        {
                            _unexpressible.Add(new UnexpressibleConstruct(
                                rule.Name, $"line {rule.Line}",
                                "choice branch is not a one-entry map — no 043 counterpart"));
                            continue;
                        }
                        var element = BuildElement(branch.Entries[0], rule.Line);
                        if (element is not null) elements.Add(element);
                    }
                    return new Composition(CompositionKind.Choice, elements);
                }
                default:
                    _unexpressible.Add(new UnexpressibleConstruct(
                        rule.Name, $"line {rule.Line}",
                        $"rule '{rule.Name}' is not a map or a choice of maps — cannot form a composition"));
                    return null;
            }
        }

        private List<ElementDecl> BuildElements(CddlMap map, int line)
        {
            var elements = new List<ElementDecl>();
            foreach (var entry in map.Entries)
            {
                var element = BuildElement(entry, line);
                if (element is not null) elements.Add(element);
            }
            return elements;
        }

        private ElementDecl? BuildElement(CddlEntry entry, int line)
        {
            var location = Location(line);
            try
            {
                var occurs = entry.Optional ? Occurs.Optional : Occurs.One;
                if (entry.Value is CddlArray array && array.Min is not null)
                {
                    // [a* t] / [a*b t] — the occurs-bounds lowering of a non-list element.
                    if (entry.Optional)
                        throw new UnexpressibleException(entry.Key,
                            "an optional entry with a bounded array has no 043 counterpart");
                    // Occurs bounds are int; an out-of-range bound is UNEXPRESSIBLE — a
                    // per-construct fidelity entry (FR-009), never a crash and never a silent
                    // wrap (lift law 2 — the exact sibling of the .size range check).
                    if (array.Min.Value > int.MaxValue || array.Max > int.MaxValue)
                        throw new UnexpressibleException($"[{array.Min.Value}*{array.Max} …]",
                            "occurs bound out of representable range");
                    var inner = TypeRefOf(array.Element, location);
                    return new ElementDecl(entry.Key, inner,
                        new Occurs((int)array.Min.Value, (int?)array.Max), location);
                }
                return new ElementDecl(entry.Key, TypeRefOf(entry.Value, location), occurs, location);
            }
            catch (UnexpressibleException ex)
            {
                _unexpressible.Add(new UnexpressibleConstruct(
                    ex.Construct, $"line {line}",
                    $"entry '{entry.Key}': {ex.Message}"));
                return null;
            }
        }

        private TypeRef TypeRefOf(CddlType type, SourceLocation location)
        {
            switch (type)
            {
                case CddlRef reference:
                    if (!_ruleNames.Contains(reference.Name))
                        throw new UnexpressibleException(reference.Name,
                            $"reference to a rule '{reference.Name}' not present in the artifact");
                    return new NamedRef(CanonicalTypeName(reference.Name), location);
                case CddlPrim prim when prim.Size is null && prim.Regexp is null:
                    return prim.Base switch
                    {
                        "int" => new PrimitiveRef(PrimitiveKind.Int, location),
                        "tstr" => new PrimitiveRef(PrimitiveKind.Str, location),
                        "bstr" => new PrimitiveRef(PrimitiveKind.Bytes, location),
                        "bool" => new PrimitiveRef(PrimitiveKind.Bool, location),
                        "uint" => new NamedRef(EnsureUintType(location), location),
                        _ => throw new UnexpressibleException(prim.Base, "unknown primitive base"),
                    };
                case CddlPrim prim:
                    throw new UnexpressibleException(prim.Base,
                        "inline faceted primitive — facets live on named types in the 043 language");
                case CddlArray array when array.Min is null:
                    return new ListRef(TypeRefOf(array.Element, location), location);
                case CddlArray:
                    throw new UnexpressibleException("[a*b …]",
                        "a bounded array nested inside a type position has no 043 counterpart");
                default:
                    throw new UnexpressibleException(type.GetType().Name.Replace("Cddl", string.Empty),
                        "inline construct has no 043 counterpart at a type position");
            }
        }

        /// <summary>Synthesize the canonical `Uint: int {{ min 0 }}` simple type on first use.
        /// When the artifact itself carries a `uint` rule (a re-lowered lift does — the
        /// emitter names the synthesized type's rule `uint`), reference that rule instead.</summary>
        private string EnsureUintType(SourceLocation location)
        {
            if (_ruleNames.Contains("uint"))
                return CanonicalTypeName("uint");
            if (!_uintSynthesized)
            {
                _uintSynthesized = true;
                _types.Add(new SimpleType("Uint", location, PrimitiveKind.Int,
                    new Facet[] { new MinValueFacet(0, location) }));
            }
            return "Uint";
        }

        private static SourceLocation Location(int line) => new(line, 1);

        /// <summary>Canonical naming: 'crdt-model' → 'CrdtModel', 'username' → 'Username'.</summary>
        private static string CanonicalTypeName(string ruleName) =>
            string.Concat(ruleName
                .Split('-', '_')
                .Where(part => part.Length > 0)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
