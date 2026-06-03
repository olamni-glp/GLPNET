using GlpRuntime.Compiler;

namespace GlpRuntime.Compiler.Pmt;

/// <summary>
/// Extension surface exposing PMT mode declarations on a <see cref="Module"/>.
/// </summary>
/// <remarks>
/// Both Dart (`ast.dart`) and the converted C# (`ast.cs`) lack a `modeDeclarations`/`ModeDeclarations`
/// member on `Module`, but `pmt/validator.dart` reads `ast.modeDeclarations`. The canonical
/// definition lives in the sibling GLP repo. This stub returns an empty list so the PMT
/// validation pipeline links — a real implementation should derive declarations from
/// `Module.ProcDeclarations` / `Module.ParamProcDecls` once the convspec for that derivation is ratified.
/// </remarks>
public static class ModuleModeDeclarationsExtensions
{
    public static IReadOnlyList<ModeDeclaration> ModeDeclarations(this Module module)
        => System.Array.Empty<ModeDeclaration>();
}

/// <summary>
/// One argument of a PMT mode declaration. <see cref="IsReader"/> is true iff
/// the argument is a reader (input — value flows from caller to callee); false
/// iff it is a writer (output — value flows from callee to caller).
/// <see cref="TypeName"/> is the declared type name for this argument (e.g.
/// <c>"List"</c>, <c>"Integer"</c>); <see cref="TypeParams"/> is its type-parameter
/// list (empty for monomorphic args).
/// </summary>
public sealed record ModedArg(
    bool IsReader,
    string TypeName,
    IReadOnlyList<string> TypeParams);

/// <summary>
/// PMT mode declaration: a predicate's signature, its per-argument modes, and
/// its source type name. Consumed by <c>ModeTable</c> (and PMT type-checking
/// downstream) keyed by <see cref="Signature"/> = <c>"predicate/arity"</c>.
/// <see cref="Predicate"/> is the predicate name half of <see cref="Signature"/>.
/// </summary>
/// <remarks>
/// Filled in by feature 020 to resolve the codeconv escalation for
/// lib/compiler/pmt/mode_table.dart — the type is referenced by mode_table.dart
/// and type_checker.dart in the Dart sources but never defined there (it lives
/// in the sibling GLP repo). Shape recovered from the mode_table convspec /
/// plan: PascalCase Signature/Args/TypeName, Args is IReadOnlyList&lt;ModedArg&gt;,
/// ModedArg.IsReader is a bool. Consumers compare via the record's structural
/// equality. Extended 2026-05-28 with TypeName/TypeParams (on ModedArg) and
/// Predicate (computed on ModeDeclaration) to satisfy pmt/type_checker's
/// E1/E2 escalations.
/// </remarks>
public sealed record ModeDeclaration(
    string Signature,
    IReadOnlyList<ModedArg> Args,
    string TypeName)
{
    /// <summary>Predicate name (the half of <see cref="Signature"/> before
    /// <c>"/arity"</c>). For <c>"merge/3"</c> this is <c>"merge"</c>.</summary>
    public string Predicate
    {
        get
        {
            var slash = Signature.IndexOf('/');
            return slash < 0 ? Signature : Signature[..slash];
        }
    }
}
