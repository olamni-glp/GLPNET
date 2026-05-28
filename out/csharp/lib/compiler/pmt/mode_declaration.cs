namespace GlpRuntime.Compiler.Pmt;

/// <summary>
/// One argument of a PMT mode declaration. <see cref="IsReader"/> is true iff
/// the argument is a reader (input — value flows from caller to callee); false
/// iff it is a writer (output — value flows from callee to caller).
/// </summary>
public sealed record ModedArg(bool IsReader);

/// <summary>
/// PMT mode declaration: a predicate's signature, its per-argument modes, and
/// its source type name. Consumed by <c>ModeTable</c> (and PMT type-checking
/// downstream) keyed by <see cref="Signature"/> = <c>"predicate/arity"</c>.
/// </summary>
/// <remarks>
/// Filled in by feature 020 to resolve the codeconv escalation for
/// lib/compiler/pmt/mode_table.dart — the type is referenced by mode_table.dart
/// and type_checker.dart in the Dart sources but never defined there (it lives
/// in the sibling GLP repo). Shape recovered from the mode_table convspec /
/// plan: PascalCase Signature/Args/TypeName, Args is IReadOnlyList&lt;ModedArg&gt;,
/// ModedArg.IsReader is a bool. Consumers compare via the record's structural
/// equality.
/// </remarks>
public sealed record ModeDeclaration(
    string Signature,
    IReadOnlyList<ModedArg> Args,
    string TypeName);
