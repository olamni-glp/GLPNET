// lib/analysis/type_checker/mode.cs
//
// Mode system for moded type checking.
// Modes represent data flow direction: input (caller writes, callee reads)
// vs output (callee writes, caller reads).

namespace GlpRuntime.Analysis.TypeChecker;

/// <summary>
/// Data flow mode for procedure arguments.
/// <para>
/// Two naming conventions are supported:
/// <list type="bullet">
///   <item><description>input/output: Traditional GLP naming</description></item>
///   <item><description>consume/produce: Paper Definition 4.2 naming (↓/↑)</description></item>
/// </list>
/// </para>
/// <para>
/// Mapping:
/// <list type="bullet">
///   <item><description>consume (↓) = input = caller writes, callee reads</description></item>
///   <item><description>produce (↑) = output = callee writes, caller reads</description></item>
/// </list>
/// </para>
/// </summary>
public enum Mode
{
    /// <summary>Callee writes, caller reads (default, no ? marker) = produce (↑)</summary>
    Output,  // (Mode)0 — must remain first to preserve GLP default
    /// <summary>Caller writes, callee reads (Type? syntax) = consume (↓)</summary>
    Input,
}

/// <summary>
/// Static alias bindings per paper Definition 4.2 naming (↓/↑).
/// These are NOT additional enum members to avoid polluting Enum.GetNames / round-trip.
/// </summary>
public static class ModeAliases
{
    /// <summary>Alias for <see cref="Mode.Input"/> mode (↓) per paper Definition 4.2.</summary>
    public static readonly Mode Consume = Mode.Input;

    /// <summary>Alias for <see cref="Mode.Output"/> mode (↑) per paper Definition 4.2.</summary>
    public static readonly Mode Produce = Mode.Output;
}

/// <summary>
/// Extension methods providing the behaviour that Dart's enhanced enum
/// carried directly on the enum body (not permitted on a C# enum).
/// </summary>
public static class ModeExtensions
{
    /// <summary>
    /// Mode dual operator per Definition 5.1.
    /// Inverts mode: Output ↔ Input, Produce ↔ Consume.
    /// Used at call boundaries: caller's output = callee's input.
    /// </summary>
    public static Mode Dual(this Mode value) => value switch
    {
        Mode.Output => Mode.Input,
        Mode.Input  => Mode.Output,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    /// <summary>Alias for <see cref="Dual"/> (per paper notation).</summary>
    public static Mode Flip(this Mode value) => value.Dual();

    /// <summary>
    /// Returns the lowercase string representation of the mode,
    /// mirroring the Dart <c>toString()</c> override: "output" or "input".
    /// Call sites previously relying on Dart <c>toString()</c> should use this helper.
    /// Do NOT use <c>mode.ToString()</c> — that returns PascalCase member names.
    /// </summary>
    public static string AsModeString(this Mode value) => value switch
    {
        Mode.Output => "output",
        Mode.Input  => "input",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}

/// <summary>
/// Host for the library-level <c>combineMode</c> function from the Dart source.
/// Kept as a separate static class because <c>Mode</c> is occupied by the enum type.
/// </summary>
public static class ModeOps
{
    /// <summary>
    /// Combine embedded mode with parent mode (involution property).
    /// <para>
    /// When a type appears inside another type's definition, the mode
    /// is determined by combining the parent's mode with the embedded mode:
    /// <list type="bullet">
    ///   <item><description>Output ⊕ Output = Output (no inversions)</description></item>
    ///   <item><description>Output ⊕ Input  = Input  (one inversion)</description></item>
    ///   <item><description>Input  ⊕ Output = Input  (one inversion)</description></item>
    ///   <item><description>Input  ⊕ Input  = Output (two inversions cancel)</description></item>
    /// </list>
    /// </para>
    /// <para>This is XOR on the boolean representation (Output=false, Input=true).</para>
    /// </summary>
    /// <param name="parent">The parent's mode.</param>
    /// <param name="embedded">The embedded type's mode.</param>
    /// <returns>The combined mode.</returns>
    public static Mode CombineMode(Mode parent, Mode embedded)
        => parent == embedded ? Mode.Output : Mode.Input;
}
