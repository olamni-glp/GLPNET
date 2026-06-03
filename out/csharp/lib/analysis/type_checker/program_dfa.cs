// lib/analysis/type_checker/program_dfa.cs
//
// ProgramDFA implementation per spec: docs/modules/type-dfa.md v0.9
// Paper Reference: Section 4.1 (lines 19-24, 47-53), Definition 4.3 (lines 283-298)
// Converted from Dart source: lib/analysis/type_checker/program_dfa.dart
// source_sha256: bf0151e2d78f26961d8153beede8211ba2f823b127de7ec7fd673299658a6057

using System.Collections.Frozen;

namespace GlpRuntime.Analysis.TypeChecker;

// ---------------------------------------------------------------------------
// DFAState
// ---------------------------------------------------------------------------

/// <summary>
/// A state in the program DFA.
/// <para>States represent:</para>
/// <list type="bullet">
///   <item><description>Defined types in complement pairs (e.g., Stream, Stream?)</description></item>
///   <item><description>System types: Integer/Integer?, String/String?</description></item>
///   <item><description>Primitive finals: _ (produced), _? (consumed)</description></item>
///   <item><description>Anonymous final: _FINAL_ (for constant/literal matches)</description></item>
///   <item><description>Procedure states: merge/3 (no complement)</description></item>
/// </list>
/// <para>
/// Equality is intentionally PARTIAL: only BaseName + IsDual define identity.
/// IsFinal and IsProcedure are excluded from Equals/GetHashCode — two states
/// with the same name and dual flag are considered identical regardless of
/// how they were constructed. See convspec rf-csharp-record-uses-all-members-equality.
/// </para>
/// </summary>
public sealed class DFAState : IEquatable<DFAState>
{
    public string BaseName { get; }
    public bool IsDual { get; }
    public bool IsFinal { get; }
    /// <summary>Distinguishes procedure states (Fix 3.1).</summary>
    public bool IsProcedure { get; }

    public DFAState(string baseName, bool isDual, bool isFinal, bool isProcedure = false)
    {
        BaseName    = baseName;
        IsDual      = isDual;
        IsFinal     = isFinal;
        IsProcedure = isProcedure;
    }

    /// <summary>Full name: BaseName? if dual, else BaseName.</summary>
    public string Name => IsDual ? $"{BaseName}?" : BaseName;

    /// <summary>
    /// Returns the dual state with IsDual flipped.
    /// NOTE: allocates a new instance on each call — callers must not rely on
    /// reference identity (state.Dual == state.Dual is value-equal via Equals,
    /// but the two references are distinct objects).
    /// </summary>
    public DFAState Dual => new DFAState(BaseName, !IsDual, IsFinal, IsProcedure);

    // --- Classifier properties (Fix 3.2 / 3.3) ---

    /// <summary>True for _ or _?</summary>
    public bool IsWildcard => BaseName == "_";

    /// <summary>True for _ (produced wildcard, non-complement)</summary>
    public bool IsProducedWildcard => BaseName == "_" && !IsDual;

    /// <summary>True for _? (consumed wildcard, complement)</summary>
    public bool IsConsumedWildcard => BaseName == "_" && IsDual;

    /// <summary>True for Integer type state (either complement or not)</summary>
    public bool IsIntegerType => BaseName == "Integer";

    /// <summary>True for Real type state (either complement or not)</summary>
    public bool IsRealType => BaseName == "Real";

    /// <summary>True for Number type state (either complement or not)</summary>
    public bool IsNumberType => BaseName == "Number";

    /// <summary>True for String type state (either complement or not)</summary>
    public bool IsStringType => BaseName == "String";

    /// <summary>True for _FINAL_ (anonymous final for constant/literal matches)</summary>
    public bool IsAnonymousFinal => BaseName == "_FINAL_";

    /// <summary>True for numeric types: Integer, Real, Number</summary>
    public bool IsNumericType => IsIntegerType || IsRealType || IsNumberType;

    /// <summary>True for primitive types: _, Integer, Real, Number, String</summary>
    public bool IsPrimitiveType =>
        IsWildcard || IsIntegerType || IsRealType || IsNumberType || IsStringType;

    /// <summary>True for user-defined types (not primitive, not procedure, not anonymous final)</summary>
    public bool IsUserDefinedType => !IsPrimitiveType && !IsProcedure && !IsAnonymousFinal;

    public override string ToString() => IsDual ? $"{BaseName}?" : BaseName;

    // --- IEquatable<DFAState>: equality on BaseName + IsDual ONLY ---

    public bool Equals(DFAState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(BaseName, other.BaseName, StringComparison.Ordinal)
            && IsDual == other.IsDual;
    }

    public override bool Equals(object? obj) => Equals(obj as DFAState);

    public override int GetHashCode() => HashCode.Combine(BaseName, IsDual);

    public static bool operator ==(DFAState? left, DFAState? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(DFAState? left, DFAState? right) => !(left == right);
}

// ---------------------------------------------------------------------------
// TransitionLabel
// ---------------------------------------------------------------------------

/// <summary>
/// A transition label in the program DFA.
/// <para>Labels encode:</para>
/// <list type="bullet">
///   <item><description>Symbol: functor name or constant value</description></item>
///   <item><description>Arity: number of arguments (0 for constants)</description></item>
///   <item><description>ArgIndex: 1-based argument position (0 for constants)</description></item>
///   <item><description>Mode: mode at this position (null for procedure arg transitions and constants)</description></item>
/// </list>
/// <para>
/// Note: the property <c>Mode</c> shares its name with the enum type <c>Mode</c>;
/// C# member-access rules disambiguate: <c>label.Mode</c> is the property,
/// <c>Mode.Output</c> is the enum-qualified value.
/// </para>
/// </summary>
public sealed class TransitionLabel : IEquatable<TransitionLabel>
{
    public string Symbol { get; }
    public int Arity { get; }
    public int ArgIndex { get; }
    public Mode? Mode { get; }

    private TransitionLabel(string symbol, int arity, int argIndex, Mode? mode)
    {
        Symbol   = symbol;
        Arity    = arity;
        ArgIndex = argIndex;
        Mode     = mode;
    }

    /// <summary>Create a functor transition label.</summary>
    public static TransitionLabel Functor(string name, int arity, int argIndex, Mode? mode = null)
        => new TransitionLabel(name, arity, argIndex, mode);

    /// <summary>
    /// Create a constant transition label.
    /// <c>value.ToString()</c> maps Dart's total <c>Object.toString()</c>;
    /// defensive <c>?? ""</c> handles C#'s annotated <c>ToString()</c> returning null.
    /// </summary>
    public static TransitionLabel Constant(object value)
        => new TransitionLabel(value?.ToString() ?? "", 0, 0, null);

    /// <summary>
    /// Returns a dual label with flipped mode.
    /// <c>?.</c> is token-and-semantics-identical between Dart and C#
    /// (see convspec rf-dart-csharp-null-aware-call-operator-identical).
    /// NOTE: allocates a new instance on each call.
    /// </summary>
    public TransitionLabel Dual => new TransitionLabel(Symbol, Arity, ArgIndex, Mode?.Flip());

    public override string ToString()
    {
        if (Arity == 0) return Symbol;
        // Mode.produce alias in Dart -> ModeAliases.Produce in C# (per mode.cs convspec)
        string modeStr = Mode is null ? "" : Mode == ModeAliases.Produce ? ":↑" : ":↓";
        return $"{Symbol}({Arity},{ArgIndex}){modeStr}";
    }

    // --- IEquatable<TransitionLabel>: equality on ALL four fields ---

    public bool Equals(TransitionLabel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Symbol, other.Symbol, StringComparison.Ordinal)
            && Arity    == other.Arity
            && ArgIndex == other.ArgIndex
            && Mode     == other.Mode;  // lifted equality on Mode? per rf-csharp-nullable-value-type-lifted-equality
    }

    public override bool Equals(object? obj) => Equals(obj as TransitionLabel);

    public override int GetHashCode() => HashCode.Combine(Symbol, Arity, ArgIndex, Mode);

    public static bool operator ==(TransitionLabel? left, TransitionLabel? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(TransitionLabel? left, TransitionLabel? right) => !(left == right);
}

// ---------------------------------------------------------------------------
// Automaton
// ---------------------------------------------------------------------------

/// <summary>An automaton for a single type (either T or T?).</summary>
public sealed class Automaton
{
    public DFAState StartState { get; }

    private readonly Dictionary<(DFAState From, TransitionLabel Label), DFAState> _transitions;

    /// <summary>
    /// Set of primitive type names this type accepts as alternatives
    /// (e.g., {"Integer", "String"} for Constant ::= Integer ; String.)
    /// </summary>
    private readonly IReadOnlySet<string> _acceptedPrimitives;

    public Automaton(
        DFAState startState,
        Dictionary<(DFAState, TransitionLabel), DFAState> transitions,
        IReadOnlySet<string>? acceptedPrimitives = null)
    {
        StartState          = startState;
        _transitions        = transitions;
        // Dart `const {}` default -> FrozenSet<string>.Empty (immutable, shared-but-cannot-mutate)
        // per rf-dart-const-set-to-csharp-frozenset-ordinal
        _acceptedPrimitives = acceptedPrimitives ?? FrozenSet<string>.Empty;
    }

    /// <summary>Set of primitive type names accepted (for leaf-consistency checks).</summary>
    public IReadOnlySet<string> AcceptedPrimitives => _acceptedPrimitives;

    /// <summary>
    /// Get the target state for a transition, or null if no such transition.
    /// Dart Map[k] returns null on miss; C# Dictionary[k] throws KeyNotFoundException
    /// — use TryGetValue per rf-csharp-dictionary-indexer-throws-vs-trygetvalue.
    /// </summary>
    public DFAState? Transition(DFAState from, TransitionLabel label)
        => _transitions.TryGetValue((from, label), out var to) ? to : null;

    /// <summary>
    /// Get all transitions (for iteration by coverage checker).
    /// Exposed as IReadOnlyDictionary (read-only narrowing) per
    /// rf-csharp-ireadonlydictionary-narrowed-public-view — same instance,
    /// narrower interface type. No defensive copy.
    /// </summary>
    public IReadOnlyDictionary<(DFAState, TransitionLabel), DFAState> Transitions => _transitions;

    /// <summary>
    /// Create dual automaton by flipping all states and modes.
    /// Imperative foreach (lower allocation, matches Dart shape) over LINQ ToDictionary.
    /// Pre-sized to avoid resizing per rf-csharp-dictionary-foreach-iteration-keyvaluepair.
    /// </summary>
    public Automaton Dual
    {
        get
        {
            var newTransitions = new Dictionary<(DFAState, TransitionLabel), DFAState>(_transitions.Count);
            foreach (var entry in _transitions)
            {
                var (fromState, label) = entry.Key;
                newTransitions[(fromState.Dual, label.Dual)] = entry.Value.Dual;
            }
            return new Automaton(StartState.Dual, newTransitions, AcceptedPrimitives);
        }
    }
}

// ---------------------------------------------------------------------------
// ProgramDFA
// ---------------------------------------------------------------------------

/// <summary>The complete DFA for a typed GLP program.</summary>
public sealed class ProgramDFA
{
    private readonly Dictionary<string, DFAState> _states;
    private readonly Dictionary<string, Automaton> _automata;

    public ProgramDFA(Dictionary<string, DFAState> states, Dictionary<string, Automaton> automata)
    {
        _states   = states;
        _automata = automata;
    }

    /// <summary>
    /// Get a state by name (e.g., "Stream" or "Stream?").
    /// Throws InvalidOperationException if not found (Dart StateError -> C# InvalidOperationException).
    /// Uses TryGetValue to preserve Dart null-on-miss semantics and add explicit message.
    /// </summary>
    public DFAState LookupState(string name)
    {
        if (!_states.TryGetValue(name, out var s))
            throw new InvalidOperationException($"State not found: {name}");
        return s;
    }

    /// <summary>
    /// Get an automaton by type name (e.g., "Stream" or "Stream?").
    /// Throws InvalidOperationException if not found.
    /// </summary>
    public Automaton LookupAutomaton(string typeName)
    {
        if (!_automata.TryGetValue(typeName, out var a))
            throw new InvalidOperationException($"Automaton not found: {typeName}");
        return a;
    }

    /// <summary>Direct access to states dictionary (for CheckLeafConsistency state lookups).</summary>
    public IReadOnlyDictionary<string, DFAState> States => _states;

    /// <summary>Direct access to automata dictionary.</summary>
    public IReadOnlyDictionary<string, Automaton> Automata => _automata;
}

// ---------------------------------------------------------------------------
// UnknownTypeException
// ---------------------------------------------------------------------------

/// <summary>
/// Exception thrown when a type name is not found in the environment.
/// Dart UnknownTypeError (recoverable signal carrying the missing type name)
/// -> C# Exception subclass per rf-dart-error-vs-exception-to-csharp-exception.
/// Class name uses the Exception suffix per BCL naming convention.
/// ex.Message returns "UnknownTypeError: {typeName}" verbatim (preserved via base ctor).
/// </summary>
public sealed class UnknownTypeException : Exception
{
    public string TypeName { get; }

    public UnknownTypeException(string typeName)
        : base($"UnknownTypeError: {typeName}")
    {
        TypeName = typeName;
    }
}

// ---------------------------------------------------------------------------
// LeafTerm
// ---------------------------------------------------------------------------

/// <summary>
/// Represents a leaf term in a term path.
/// Immutable data carrier with reference identity (no IEquatable — Dart source
/// has no == override, so C# keeps reference identity matching Dart exactly).
/// </summary>
public sealed class LeafTerm
{
    /// <summary>Variable name, or null for constants.</summary>
    public string? Name { get; }
    public bool IsVariable { get; }
    /// <summary>Only meaningful if IsVariable.</summary>
    public bool IsReader { get; }
    /// <summary>Mode at this position.</summary>
    public Mode? Mode { get; }
    /// <summary>Constant value, or null for variables.</summary>
    public object? Value { get; }
    /// <summary>True if value is an integer.</summary>
    public bool IsInteger { get; }
    /// <summary>True if value is a real (floating-point).</summary>
    public bool IsReal { get; }
    /// <summary>True if value is a string.</summary>
    public bool IsString { get; }

    private LeafTerm(
        string? name,
        bool isVariable,
        bool isReader = false,
        Mode? mode = null,
        object? value = null,
        bool isInteger = false,
        bool isReal = false,
        bool isString = false)
    {
        Name       = name;
        IsVariable = isVariable;
        IsReader   = isReader;
        Mode       = mode;
        Value      = value;
        IsInteger  = isInteger;
        IsReal     = isReal;
        IsString   = isString;
    }

    /// <summary>Create a writer variable leaf.</summary>
    public static LeafTerm Writer(string name, Mode mode)
        => new LeafTerm(name: name, isVariable: true, isReader: false, mode: mode);

    /// <summary>Create a reader variable leaf.</summary>
    public static LeafTerm Reader(string name, Mode mode)
        => new LeafTerm(name: name, isVariable: true, isReader: true, mode: mode);

    /// <summary>Create an integer constant leaf.</summary>
    public static LeafTerm IntegerConstant(int value, Mode? mode = null)
        => new LeafTerm(name: null, isVariable: false, value: value, isInteger: true, mode: mode);

    /// <summary>Create a real constant leaf.</summary>
    public static LeafTerm RealConstant(double value, Mode? mode = null)
        => new LeafTerm(name: null, isVariable: false, value: value, isReal: true, mode: mode);

    /// <summary>Create a string constant leaf.</summary>
    public static LeafTerm StringConstant(string value, Mode? mode = null)
        => new LeafTerm(name: null, isVariable: false, value: value, isString: true, mode: mode);

    /// <summary>Create a constant leaf (atom or other).</summary>
    public static LeafTerm Constant(object value, Mode? mode = null)
        => new LeafTerm(name: null, isVariable: false, value: value, mode: mode);
}

// ---------------------------------------------------------------------------
// LeafConsistencyResult
// ---------------------------------------------------------------------------

/// <summary>
/// Result of checking leaf consistency.
/// Immutable data carrier with two static factory methods (Consistent / Inconsistent).
/// </summary>
public sealed class LeafConsistencyResult
{
    public bool IsConsistent { get; }
    public DFAState? Type { get; }
    public string? Reason { get; }

    private LeafConsistencyResult(bool isConsistent, DFAState? type, string? reason)
    {
        IsConsistent = isConsistent;
        Type         = type;
        Reason       = reason;
    }

    public static LeafConsistencyResult Consistent(DFAState? type)
        => new LeafConsistencyResult(true, type, null);

    public static LeafConsistencyResult Inconsistent(string reason)
        => new LeafConsistencyResult(false, null, reason);
}

// ---------------------------------------------------------------------------
// ProgramDfaBuilder — static class hosting Build + private helpers
// ---------------------------------------------------------------------------

/// <summary>
/// Static builder for ProgramDFA from a TypeEnvironment.
/// Dart top-level functions -> C# static methods per rf-csharp-static-class-no-toplevel-members.
/// </summary>
public static class ProgramDfaBuilder
{
    /// <summary>
    /// Build the complete program DFA from the type environment.
    /// Implements spec algorithm: buildProgramDFA(env).
    /// <para>Four-phase imperative structure (load-bearing order):</para>
    /// <list type="number">
    ///   <item><description>Seed system states and automata</description></item>
    ///   <item><description>FIRST PASS: create T and T? states for all defined types</description></item>
    ///   <item><description>SECOND PASS: build automata for all defined types (forward refs require all states to exist first)</description></item>
    ///   <item><description>Procedure states and automata</description></item>
    /// </list>
    /// Dictionaries use StringComparer.Ordinal (project-wide convention per
    /// rf-csharp-dictionary-stringcomparer-ordinal-discipline).
    /// </summary>
    public static ProgramDFA Build(TypeEnvironment env)
    {
        var states   = new Dictionary<string, DFAState>(StringComparer.Ordinal);
        var automata = new Dictionary<string, Automaton>(StringComparer.Ordinal);

        // Phase 1: Create system states (complement pairs)
        states["_"]        = new DFAState("_",       isDual: false, isFinal: true);
        states["_?"]       = new DFAState("_",       isDual: true,  isFinal: true);
        states["Integer"]  = new DFAState("Integer", isDual: false, isFinal: false);
        states["Integer?"] = new DFAState("Integer", isDual: true,  isFinal: false);
        states["Real"]     = new DFAState("Real",    isDual: false, isFinal: false);
        states["Real?"]    = new DFAState("Real",    isDual: true,  isFinal: false);
        states["Number"]   = new DFAState("Number",  isDual: false, isFinal: false);
        states["Number?"]  = new DFAState("Number",  isDual: true,  isFinal: false);
        states["String"]   = new DFAState("String",  isDual: false, isFinal: false);
        states["String?"]  = new DFAState("String",  isDual: true,  isFinal: false);
        states["_FINAL_"]  = new DFAState("_FINAL_", isDual: false, isFinal: true);

        // Phase 1 continued: Create automata for system types
        automata["_"]        = FinalAutomaton(states["_"]);
        automata["_?"]       = FinalAutomaton(states["_?"]);
        automata["Integer"]  = PrimitiveTypeAutomaton(states["Integer"],  states["_FINAL_"]);
        automata["Integer?"] = PrimitiveTypeAutomaton(states["Integer?"], states["_FINAL_"]);
        automata["Real"]     = PrimitiveTypeAutomaton(states["Real"],     states["_FINAL_"]);
        automata["Real?"]    = PrimitiveTypeAutomaton(states["Real?"],    states["_FINAL_"]);
        automata["Number"]   = PrimitiveTypeAutomaton(states["Number"],   states["_FINAL_"]);
        automata["Number?"]  = PrimitiveTypeAutomaton(states["Number?"],  states["_FINAL_"]);
        automata["String"]   = PrimitiveTypeAutomaton(states["String"],   states["_FINAL_"]);
        automata["String?"]  = PrimitiveTypeAutomaton(states["String?"],  states["_FINAL_"]);

        // Phase 2: Create states for ALL defined types FIRST
        // (Automata may reference other types, so all states must exist before building automata)
        foreach (var (typeName, _) in env.Types)
        {
            states[typeName]       = new DFAState(typeName, isDual: false, isFinal: false);
            states[$"{typeName}?"] = new DFAState(typeName, isDual: true,  isFinal: false);
        }

        // Phase 3: THEN build automata for all defined types
        foreach (var (typeName, typeDef) in env.Types)
        {
            // T automaton: modes as declared
            // T? automaton: all modes flipped, all target states dualized
            automata[typeName]       = BuildTypeAutomaton(typeDef, states, isDual: false);
            automata[$"{typeName}?"] = BuildTypeAutomaton(typeDef, states, isDual: true);
        }

        // Phase 4: Create procedure states (no complement)
        foreach (var (procKey, _) in env.Procedures)
        {
            states[procKey] = new DFAState(procKey, isDual: false, isFinal: false, isProcedure: true);
        }

        // Phase 4 continued: Build procedure automata
        foreach (var (procKey, procDecl) in env.Procedures)
        {
            automata[procKey] = BuildProcedureAutomaton(procDecl, states);
        }

        return new ProgramDFA(states, automata);
    }

    // --- Private helpers ---

    /// <summary>Create a final automaton (no transitions).</summary>
    private static Automaton FinalAutomaton(DFAState state)
        => new Automaton(state, new Dictionary<(DFAState, TransitionLabel), DFAState>());

    /// <summary>
    /// Create a primitive type automaton (conceptual infinite transitions to _FINAL_).
    /// No explicit transitions — handled specially in leaf consistency.
    /// </summary>
    private static Automaton PrimitiveTypeAutomaton(DFAState state, DFAState finalState)
        => new Automaton(state, new Dictionary<(DFAState, TransitionLabel), DFAState>());

    /// <summary>
    /// Build an automaton for a type definition.
    /// Implements spec algorithm: buildTypeAutomaton.
    /// The named-arg <c>isDual</c> has no default (C# enforces call-site naming to
    /// mirror Dart's <c>required</c> named parameter).
    /// </summary>
    private static Automaton BuildTypeAutomaton(
        TypeDef typeDef,
        Dictionary<string, DFAState> states,
        bool isDual)
    {
        var typeName       = typeDef.Name;
        var startStateName = isDual ? $"{typeName}?" : typeName;
        var startState     = states[startStateName];

        var transitions        = new Dictionary<(DFAState, TransitionLabel), DFAState>();
        var acceptedPrimitives = new HashSet<string>(StringComparer.Ordinal);

        foreach (var alt in typeDef.Alternatives)
        {
            // Collect primitive type alternatives (Integer, Real, Number, String)
            if (alt is TypeRef tr && TypeRef.Builtins.Contains(tr.Name))
            {
                acceptedPrimitives.Add(tr.Name);
            }
            AddTypeTransitions(startState, alt, ModeAliases.Produce, states, transitions, isDual);
        }

        IReadOnlySet<string> acceptedSet = acceptedPrimitives.Count > 0
            ? acceptedPrimitives.ToFrozenSet(StringComparer.Ordinal)
            : FrozenSet<string>.Empty;

        return new Automaton(startState, transitions, acceptedSet);
    }

    /// <summary>
    /// Add transitions from a type alternative.
    /// Implements spec algorithm: addTypeTransitions.
    /// Type-pattern switch over the closed AST sum from type_ast.cs.
    /// PrimitiveModeAlt is intentionally OMITTED here — it's a leaf, not a constructor;
    /// it is handled in ResolveTypeExpr. The explicit default: arm preserves this asymmetry.
    /// </summary>
    private static void AddTypeTransitions(
        DFAState fromState,
        TypeExpr alt,
        Mode contextMode,
        Dictionary<string, DFAState> states,
        Dictionary<(DFAState, TransitionLabel), DFAState> transitions,
        bool isDual)
    {
        switch (alt)
        {
            case ConstantAlt c:
            {
                var label = TransitionLabel.Constant(c.Value);
                transitions[(fromState, label)] = states["_FINAL_"];
                break;
            }
            case ListNilAlt:
            {
                var label = TransitionLabel.Constant("[]");
                transitions[(fromState, label)] = states["_FINAL_"];
                break;
            }
            case ListConsAlt l:
            {
                var headMode = ModeOf(l.Head, contextMode);
                var tailMode = ModeOf(l.Tail, contextMode);

                // Apply complement to modes if building complement automaton
                if (isDual)
                {
                    headMode = headMode.Flip();
                    tailMode = tailMode.Flip();
                }

                var headLabel = TransitionLabel.Functor("[|]", 2, 1, mode: headMode);
                var tailLabel = TransitionLabel.Functor("[|]", 2, 2, mode: tailMode);

                transitions[(fromState, headLabel)] = ResolveTypeExpr(l.Head, states, isDual);
                transitions[(fromState, tailLabel)] = ResolveTypeExpr(l.Tail, states, isDual);
                break;
            }
            case StructAlt s:
            {
                for (int i = 0; i < s.Args.Count; i++)
                {
                    var argType = s.Args[i];
                    var argMode = ModeOf(argType, contextMode);
                    if (isDual)
                    {
                        argMode = argMode.Flip();
                    }
                    var label = TransitionLabel.Functor(s.Functor, s.Args.Count, i + 1, mode: argMode);
                    transitions[(fromState, label)] = ResolveTypeExpr(argType, states, isDual);
                }
                break;
            }
            case DiffListAlt d:
            {
                var contentMode = ModeOf(d.Content, contextMode);
                var holeMode    = ModeOf(d.Hole, contextMode);
                if (isDual)
                {
                    contentMode = contentMode.Flip();
                    holeMode    = holeMode.Flip();
                }

                var contentLabel = TransitionLabel.Functor("\\", 2, 1, mode: contentMode);
                var holeLabel    = TransitionLabel.Functor("\\", 2, 2, mode: holeMode);

                transitions[(fromState, contentLabel)] = ResolveTypeExpr(d.Content, states, isDual);
                transitions[(fromState, holeLabel)]    = ResolveTypeExpr(d.Hole,    states, isDual);
                break;
            }
            default:
                // PrimitiveModeAlt is handled in ResolveTypeExpr - it's a leaf, not a constructor
                break;
        }
    }

    /// <summary>
    /// Resolve a type expression to a DFA state.
    /// Implements spec algorithm: resolveTypeExpr.
    /// Uses XOR logic for nested complements.
    /// Handles PrimitiveModeAlt and TypeRef; default arm throws (true programmer error).
    /// </summary>
    private static DFAState ResolveTypeExpr(
        TypeExpr typeExpr,
        Dictionary<string, DFAState> states,
        bool isDual)
    {
        switch (typeExpr)
        {
            case PrimitiveModeAlt p:
            {
                // Determine base state: isInput means _? base
                bool baseIsComplement  = p.IsInput;
                bool finalIsComplement = baseIsComplement != isDual; // XOR
                return finalIsComplement ? states["_?"] : states["_"];
            }
            case TypeRef r:
            {
                // Determine base state
                bool baseIsComplement  = r.IsInput;
                bool finalIsComplement = baseIsComplement != isDual; // XOR

                if (r.Name == "Integer")
                    return finalIsComplement ? states["Integer?"] : states["Integer"];
                if (r.Name == "Real")
                    return finalIsComplement ? states["Real?"] : states["Real"];
                if (r.Name == "Number")
                    return finalIsComplement ? states["Number?"] : states["Number"];
                if (r.Name == "String")
                    return finalIsComplement ? states["String?"] : states["String"];

                var targetName = r.Name;
                var targetKey  = finalIsComplement ? $"{targetName}?" : targetName;
                if (!states.TryGetValue(targetKey, out var targetState))
                    throw new UnknownTypeException(targetName);
                return targetState;
            }
            default:
                throw new InvalidOperationException($"Cannot resolve type expression: {typeExpr}");
        }
    }

    /// <summary>
    /// Compute mode for a type expression at a given context mode.
    /// Implements spec algorithm: modeOf.
    /// T? flips mode, T keeps mode (before complement is applied).
    /// </summary>
    private static Mode ModeOf(TypeExpr typeExpr, Mode contextMode)
    {
        if (typeExpr is TypeRef r && r.IsInput)
            return contextMode.Flip();
        if (typeExpr is PrimitiveModeAlt p && p.IsInput)
            return contextMode.Flip();
        return contextMode;
    }

    /// <summary>
    /// Build an automaton for a procedure declaration.
    /// Implements spec algorithm: buildProcedureAutomaton.
    /// </summary>
    private static Automaton BuildProcedureAutomaton(
        ProcDecl procDecl,
        Dictionary<string, DFAState> states)
    {
        var procState   = states[procDecl.QualifiedKey];
        var transitions = new Dictionary<(DFAState, TransitionLabel), DFAState>();

        for (int i = 0; i < procDecl.Arity; i++)
        {
            var argType = procDecl.ArgTypes[i];
            var label   = TransitionLabel.Functor(procDecl.Name, procDecl.Arity, i + 1, mode: null);

            // Target is the declared type directly (Stream? or Stream)
            var targetStateName = GetFullTypeName(argType);
            if (!states.TryGetValue(targetStateName, out var targetState))
                throw new UnknownTypeException(targetStateName);
            transitions[(procState, label)] = targetState;
        }

        return new Automaton(procState, transitions);
    }

    /// <summary>Get the full type name including ? if input mode.</summary>
    private static string GetFullTypeName(TypeExpr typeExpr)
    {
        switch (typeExpr)
        {
            case PrimitiveModeAlt p:
                return p.IsInput ? "_?" : "_";
            case TypeRef r:
                return r.IsInput ? $"{r.Name}?" : r.Name;
            default:
                throw new InvalidOperationException($"Cannot get type name from: {typeExpr}");
        }
    }

    // =========================================================================
    // Leaf Consistency Checking (Definition 4.3)
    // =========================================================================

    /// <summary>
    /// Check leaf consistency per Definition 4.3.
    /// Implements the simplified algorithm using Mode Correspondence Property:
    /// <list type="bullet">
    ///   <item><description>For variables: check path's structural mode matches variable's implicit mode</description></item>
    ///   <item><description>For constants: check type accepts this constant</description></item>
    /// </list>
    /// <para>
    /// Dispatch order is load-bearing (encodes check precedence):
    /// variable-first -> anonymous-final -> primitive-type ladder -> wildcard -> user-defined.
    /// </para>
    /// </summary>
    public static LeafConsistencyResult CheckLeafConsistency(
        LeafTerm leaf,
        DFAState state,
        ProgramDFA dfa)
    {
        // Case 1: Variable leaf — check path mode matches variable's implicit mode
        // By Mode Correspondence Property, we don't need to inspect state.IsDual
        if (leaf.IsVariable)
        {
            // Reader X? must be at consume position (mode ↓)
            // Writer X must be at produce position (mode ↑)
            if (leaf.IsReader && leaf.Mode == ModeAliases.Consume)
                return LeafConsistencyResult.Consistent(state);
            if (!leaf.IsReader && leaf.Mode == ModeAliases.Produce)
                return LeafConsistencyResult.Consistent(state);

            string expected = leaf.IsReader ? "↓ (consume)" : "↑ (produce)";
            string actual   = leaf.Mode == ModeAliases.Consume ? "↓ (consume)" : "↑ (produce)";
            return LeafConsistencyResult.Inconsistent(
                $"Variable mode mismatch: {(leaf.IsReader ? "reader" : "writer")} requires {expected}, got {actual}");
        }

        // Case 2: Constant leaf — check type accepts this constant

        // Case 2a: At anonymous final state (already matched a constant transition)
        if (state.IsAnonymousFinal)
            return LeafConsistencyResult.Consistent(state);

        // Case 2b: At primitive type state (Integer, Real, Number, String)
        if (state.IsIntegerType)
        {
            if (leaf.IsInteger)
            {
                dfa.States.TryGetValue("_FINAL_", out var sf);
                return LeafConsistencyResult.Consistent(sf);
            }
            return LeafConsistencyResult.Inconsistent("Integer type requires integer literal");
        }

        if (state.IsRealType)
        {
            if (leaf.IsReal)
            {
                dfa.States.TryGetValue("_FINAL_", out var sf);
                return LeafConsistencyResult.Consistent(sf);
            }
            return LeafConsistencyResult.Inconsistent("Real type requires real literal");
        }

        if (state.IsNumberType)
        {
            if (leaf.IsInteger || leaf.IsReal)
            {
                dfa.States.TryGetValue("_FINAL_", out var sf);
                return LeafConsistencyResult.Consistent(sf);
            }
            return LeafConsistencyResult.Inconsistent("Number type requires numeric literal");
        }

        if (state.IsStringType)
        {
            if (leaf.IsString)
            {
                dfa.States.TryGetValue("_FINAL_", out var sf);
                return LeafConsistencyResult.Consistent(sf);
            }
            return LeafConsistencyResult.Inconsistent("String type requires string literal");
        }

        // Case 2c: At wildcard state
        // Per spec v0.6: _ accepts any produced term (mode ↑), _? accepts any consumed term (mode ↓)
        if (state.IsProducedWildcard)
        {
            if (leaf.Mode == ModeAliases.Produce)
                return LeafConsistencyResult.Consistent(state);
            return LeafConsistencyResult.Inconsistent("_ expects produced term (mode ↑), got consumed term");
        }
        if (state.IsConsumedWildcard)
        {
            if (leaf.Mode == ModeAliases.Consume)
                return LeafConsistencyResult.Consistent(state);
            return LeafConsistencyResult.Inconsistent("_? expects consumed term (mode ↓), got produced term");
        }

        // Case 2d: At user-defined type state — check for matching transition or primitive
        if (leaf.Value != null)
        {
            if (dfa.Automata.TryGetValue(state.Name, out var automaton))
            {
                // First, try explicit constant transition
                var constLabel = TransitionLabel.Constant(leaf.Value);
                var nextState  = automaton.Transition(state, constLabel);
                if (nextState != null)
                    return LeafConsistencyResult.Consistent(nextState);

                // Second, check if type accepts primitive types that match this constant
                if (automaton.AcceptedPrimitives.Count > 0)
                {
                    if (leaf.IsInteger &&
                        (automaton.AcceptedPrimitives.Contains("Integer") ||
                         automaton.AcceptedPrimitives.Contains("Number")))
                    {
                        dfa.States.TryGetValue("_FINAL_", out var sf);
                        return LeafConsistencyResult.Consistent(sf);
                    }
                    if (leaf.IsReal &&
                        (automaton.AcceptedPrimitives.Contains("Real") ||
                         automaton.AcceptedPrimitives.Contains("Number")))
                    {
                        dfa.States.TryGetValue("_FINAL_", out var sf);
                        return LeafConsistencyResult.Consistent(sf);
                    }
                    if (leaf.IsString && automaton.AcceptedPrimitives.Contains("String"))
                    {
                        dfa.States.TryGetValue("_FINAL_", out var sf);
                        return LeafConsistencyResult.Consistent(sf);
                    }
                }
            }
        }

        return LeafConsistencyResult.Inconsistent(
            $"Constant does not match any alternative at type position {state.Name}");
    }
}
