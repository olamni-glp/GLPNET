// Compatibility checker (feature 043-xsd-schema-language, T030; research R8).
//
// Contract: specs/043-xsd-schema-language/contracts/compat-evolution.md — the construct-level
// rule table under the CLOSED-WORLD law (043 validation rejects undeclared elements, so
// removing ANY element is backward-breaking; Avro-style ignore-unknown does NOT apply).
// Directions: backward = new readers understand old writers (the new schema must accept all
// old-valid data); forward = old readers tolerate new writers. Full = backward ∧ forward.
// Pattern widen/narrow is decided by NFA language inclusion on the R6 subset; inclusion that
// cannot be established is conservatively breaking in BOTH directions, and the verdict says
// so. Every breaking construct is named with its rule-table row (US4 AS-2, SC-005).
//
// Types are compared STRUCTURALLY through their resolved shapes, so renaming a type is not a
// break; changing what it resolves to is.

using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang;

public static class CompatChecker
{
    public static CompatVerdict Check(SchemaDocument oldDoc, SchemaDocument newDoc, CompatMode mode)
    {
        var context = new Context(oldDoc, newDoc, mode);

        var oldMessages = oldDoc.Messages.ToDictionary(m => m.Functor);
        var newMessages = newDoc.Messages.ToDictionary(m => m.Functor);
        foreach (var (functor, oldMessage) in oldMessages)
        {
            if (newMessages.TryGetValue(functor, out var newMessage))
                context.CompareComposition(functor, oldMessage.Body, newMessage.Body,
                    oldMessage.Location, newMessage.Location);
            else
                context.BreakBoth(functor, oldMessage.Location, "remove message kind");
        }
        // Message kinds present only in the new document are additive — compatible.

        return new CompatVerdict(
            mode,
            context.Breaks.Count == 0 ? CompatOutcome.Compatible : CompatOutcome.Incompatible,
            context.Breaks);
    }

    private sealed class Context
    {
        private readonly Dictionary<string, NamedType> _oldTypes;
        private readonly Dictionary<string, NamedType> _newTypes;
        private readonly bool _checkBackward;
        private readonly bool _checkForward;
        private readonly HashSet<(string Old, string New)> _visitedTypePairs = new();

        public Context(SchemaDocument oldDoc, SchemaDocument newDoc, CompatMode mode)
        {
            _oldTypes = oldDoc.Types.ToDictionary(t => t.Name);
            _newTypes = newDoc.Types.ToDictionary(t => t.Name);
            _checkBackward = mode is CompatMode.Backward or CompatMode.Full or CompatMode.Transitive;
            _checkForward = mode is CompatMode.Forward or CompatMode.Full or CompatMode.Transitive;
        }

        public List<BreakingConstruct> Breaks { get; } = new();

        public void Break(CompatDirection direction, string construct, SourceLocation location, string rule)
        {
            var check = direction == CompatDirection.Backward ? _checkBackward : _checkForward;
            if (check)
                Breaks.Add(new BreakingConstruct(construct, location.ToString(), direction, rule));
        }

        public void BreakBoth(string construct, SourceLocation location, string rule)
        {
            Break(CompatDirection.Backward, construct, location, rule);
            Break(CompatDirection.Forward, construct, location, rule);
        }

        // --------------------------------------------------------------
        // Compositions
        // --------------------------------------------------------------

        public void CompareComposition(
            string path,
            Composition oldComp,
            Composition newComp,
            SourceLocation oldLocation,
            SourceLocation newLocation)
        {
            if (oldComp.Kind != newComp.Kind)
            {
                BreakBoth(path, newLocation, "type change (composition kind changed)");
                return;
            }
            if (oldComp.Kind == CompositionKind.Sequence)
                CompareSequence(path, oldComp, newComp, newLocation);
            else
                CompareChoice(path, oldComp, newComp);
        }

        private void CompareSequence(string path, Composition oldComp, Composition newComp, SourceLocation newLocation)
        {
            var oldByName = oldComp.Elements.ToDictionary(e => e.Name);
            var newByName = newComp.Elements.ToDictionary(e => e.Name);

            foreach (var oldElement in oldComp.Elements)
                if (!newByName.ContainsKey(oldElement.Name))
                {
                    var construct = $"{path}.{oldElement.Name}";
                    if (oldElement.Occurs.Min == 0)
                        Break(CompatDirection.Backward, construct, oldElement.Location,
                            "remove optional element (closed-world)");
                    else
                        BreakBoth(construct, oldElement.Location, "remove mandatory element");
                }

            foreach (var newElement in newComp.Elements)
                if (!oldByName.ContainsKey(newElement.Name) && newElement.Occurs.Min >= 1)
                    Break(CompatDirection.Backward, $"{path}.{newElement.Name}", newElement.Location,
                        "add mandatory element");
            // Adding an element with occurs min = 0 (optional) is compatible in both directions.

            // Reorder: the relative order of the common elements must be preserved.
            var commonOldOrder = oldComp.Elements.Select(e => e.Name).Where(newByName.ContainsKey).ToList();
            var commonNewOrder = newComp.Elements.Select(e => e.Name).Where(oldByName.ContainsKey).ToList();
            if (!commonOldOrder.SequenceEqual(commonNewOrder))
                BreakBoth(path, newLocation, "sequence reorder");

            foreach (var name in commonOldOrder)
                CompareElement($"{path}.{name}", oldByName[name], newByName[name]);
        }

        private void CompareChoice(string path, Composition oldComp, Composition newComp)
        {
            var oldByName = oldComp.Elements.ToDictionary(e => e.Name);
            var newByName = newComp.Elements.ToDictionary(e => e.Name);

            foreach (var oldBranch in oldComp.Elements)
                if (!newByName.ContainsKey(oldBranch.Name))
                    Break(CompatDirection.Backward, $"{path}.{oldBranch.Name}", oldBranch.Location,
                        "remove choice branch");
            foreach (var newBranch in newComp.Elements)
                if (!oldByName.ContainsKey(newBranch.Name))
                    Break(CompatDirection.Forward, $"{path}.{newBranch.Name}", newBranch.Location,
                        "add choice branch");
            foreach (var oldBranch in oldComp.Elements)
                if (newByName.TryGetValue(oldBranch.Name, out var newBranch))
                    CompareElement($"{path}.{oldBranch.Name}", oldBranch, newBranch);
        }

        // --------------------------------------------------------------
        // Elements: occurs bounds + type
        // --------------------------------------------------------------

        private void CompareElement(string path, ElementDecl oldElement, ElementDecl newElement)
        {
            var oldOccurs = oldElement.Occurs;
            var newOccurs = newElement.Occurs;
            if (newOccurs.Min < oldOccurs.Min || MaxGreater(newOccurs.Max, oldOccurs.Max))
                Break(CompatDirection.Forward, path, newElement.Location, "occurs widening");
            if (newOccurs.Min > oldOccurs.Min || MaxLess(newOccurs.Max, oldOccurs.Max))
                Break(CompatDirection.Backward, path, newElement.Location, "occurs narrowing");

            CompareTypeRef(path, oldElement.Type, newElement.Type, newElement.Location);
        }

        private static bool MaxGreater(int? a, int? b) => a is null ? b is not null : b is not null && a > b;

        private static bool MaxLess(int? a, int? b) => b is null ? a is not null : a is not null && a < b;

        // --------------------------------------------------------------
        // Type refs: resolved-shape comparison
        // --------------------------------------------------------------

        private void CompareTypeRef(string path, TypeRef oldRef, TypeRef newRef, SourceLocation location)
        {
            var oldShape = Resolve(oldRef, _oldTypes);
            var newShape = Resolve(newRef, _newTypes);

            switch (oldShape, newShape)
            {
                case (ListShape ol, ListShape nl):
                    CompareTypeRef($"{path}[]", ol.Element, nl.Element, location);
                    break;
                case (SimpleShape os, SimpleShape ns) when os.Base == ns.Base:
                    CompareFacets(path, os, ns, location);
                    break;
                case (ComplexShape oc, ComplexShape nc):
                    if (_visitedTypePairs.Add((oc.Name, nc.Name)))
                        CompareComposition(path, oc.Composition, nc.Composition, oc.Location, nc.Location);
                    break;
                default:
                    BreakBoth(path, location, "type change");
                    break;
            }
        }

        private abstract record Shape;

        private sealed record SimpleShape(PrimitiveKind Base, IReadOnlyList<Facet> Facets) : Shape;

        private sealed record ComplexShape(string Name, Composition Composition, SourceLocation Location) : Shape;

        private sealed record ListShape(TypeRef Element) : Shape;

        private static Shape Resolve(TypeRef typeRef, Dictionary<string, NamedType> types) => typeRef switch
        {
            PrimitiveRef prim => new SimpleShape(prim.Kind, Array.Empty<Facet>()),
            ListRef list => new ListShape(list.Element),
            NamedRef named => types[named.Name] switch
            {
                SimpleType simple => new SimpleShape(simple.Base, simple.Facets),
                ComplexType complex => new ComplexShape(complex.Name, complex.Composition, complex.Location),
                var other => throw new InvalidOperationException($"unknown named type {other.GetType().Name}"),
            },
            _ => throw new InvalidOperationException($"unknown type-ref {typeRef.GetType().Name}"),
        };

        // --------------------------------------------------------------
        // Facets: widen → forward breaks; narrow → backward breaks
        // --------------------------------------------------------------

        private void CompareFacets(string path, SimpleShape oldShape, SimpleShape newShape, SourceLocation location)
        {
            CompareBound(path, "min", location,
                oldLo: ValueOf<MinValueFacet>(oldShape)?.Value ?? long.MinValue,
                newLo: ValueOf<MinValueFacet>(newShape)?.Value ?? long.MinValue,
                lowerBound: true);
            CompareBound(path, "max", location,
                oldLo: ValueOf<MaxValueFacet>(oldShape)?.Value ?? long.MaxValue,
                newLo: ValueOf<MaxValueFacet>(newShape)?.Value ?? long.MaxValue,
                lowerBound: false);
            CompareBound(path, "minLength", location,
                oldLo: ValueOf<MinLengthFacet>(oldShape)?.Value ?? 0,
                newLo: ValueOf<MinLengthFacet>(newShape)?.Value ?? 0,
                lowerBound: true);
            CompareBound(path, "maxLength", location,
                oldLo: ValueOf<MaxLengthFacet>(oldShape)?.Value ?? long.MaxValue,
                newLo: ValueOf<MaxLengthFacet>(newShape)?.Value ?? long.MaxValue,
                lowerBound: false);
            CompareEnums(path, oldShape, newShape, location);
            ComparePatterns(path, oldShape, newShape, location);
        }

        /// <summary>A lower bound moving down (or an upper bound moving up) widens; the
        /// opposite movement narrows.</summary>
        private void CompareBound(string path, string keyword, SourceLocation location, long oldLo, long newLo, bool lowerBound)
        {
            var widened = lowerBound ? newLo < oldLo : newLo > oldLo;
            var narrowed = lowerBound ? newLo > oldLo : newLo < oldLo;
            if (widened)
                Break(CompatDirection.Forward, $"{path}.{keyword}", location, $"facet widening ({keyword})");
            if (narrowed)
                Break(CompatDirection.Backward, $"{path}.{keyword}", location, $"facet narrowing ({keyword})");
        }

        private void CompareEnums(string path, SimpleShape oldShape, SimpleShape newShape, SourceLocation location)
        {
            var oldEnum = oldShape.Facets.OfType<EnumerationFacet>().FirstOrDefault()?.Members;
            var newEnum = newShape.Facets.OfType<EnumerationFacet>().FirstOrDefault()?.Members;
            var construct = $"{path}.enum";
            switch (oldEnum, newEnum)
            {
                case (null, null):
                    return;
                case (null, not null):
                    Break(CompatDirection.Backward, construct, location, "facet narrowing (enum added)");
                    return;
                case (not null, null):
                    Break(CompatDirection.Forward, construct, location, "facet widening (enum removed)");
                    return;
            }
            if (newEnum!.Except(oldEnum!).Any())
                Break(CompatDirection.Forward, construct, location, "facet widening (enum member added)");
            if (oldEnum!.Except(newEnum!).Any())
                Break(CompatDirection.Backward, construct, location, "facet narrowing (enum member removed)");
        }

        private void ComparePatterns(string path, SimpleShape oldShape, SimpleShape newShape, SourceLocation location)
        {
            var oldPattern = oldShape.Facets.OfType<PatternFacet>().FirstOrDefault()?.Pattern;
            var newPattern = newShape.Facets.OfType<PatternFacet>().FirstOrDefault()?.Pattern;
            var construct = $"{path}.pattern";
            switch (oldPattern, newPattern)
            {
                case (null, null):
                    return;
                case (null, not null):
                    Break(CompatDirection.Backward, construct, location, "facet narrowing (pattern added)");
                    return;
                case (not null, null):
                    Break(CompatDirection.Forward, construct, location, "facet widening (pattern removed)");
                    return;
            }
            if (oldPattern == newPattern) return;

            // † Language inclusion on the R6 NFAs; validated documents guarantee both parse.
            var oldNfa = PatternNfa.Parse(oldPattern!).Nfa!;
            var newNfa = PatternNfa.Parse(newPattern!).Nfa!;
            var oldInNew = PatternNfa.IsSubsetOf(oldNfa, newNfa);
            var newInOld = PatternNfa.IsSubsetOf(newNfa, oldNfa);

            if (oldInNew == PatternInclusion.Unknown || newInOld == PatternInclusion.Unknown)
            {
                BreakBoth(construct, location, "pattern inclusion not established (conservatively breaking)");
                return;
            }
            if (oldInNew == PatternInclusion.Subset && newInOld == PatternInclusion.Subset)
                return; // same language
            if (oldInNew == PatternInclusion.NotSubset)
                Break(CompatDirection.Backward, construct, location,
                    "facet narrowing (pattern rejects previously accepted values)");
            if (newInOld == PatternInclusion.NotSubset)
                Break(CompatDirection.Forward, construct, location,
                    "facet widening (pattern accepts previously rejected values)");
        }

        private static TFacet? ValueOf<TFacet>(SimpleShape shape) where TFacet : Facet =>
            shape.Facets.OfType<TFacet>().FirstOrDefault();
    }
}
