// glp_repl placeholder entrypoint — feature 020 (T017 target).
//
// While bulk Dart→C# codegen for the runtime is in flight (the
// `/codeconv-codegen` drive seeded by feature 019), this stub keeps the
// `glp_repl` executable project building so the build-gate plumbing is
// real before any runtime code lands. T017 replaces this file with the
// converted REPL + the structured `:trace` instrumentation the
// differential equivalence oracle (feature 020) consumes — see
// `specs/020-trace-equivalence-fidelity/contracts/trace_normalization.md`.

namespace GlpRuntime.Repl;

internal static class Program
{
    private static int Main(string[] args)
    {
        System.Console.Error.WriteLine(
            "glp_repl: placeholder — converted runtime not yet built. " +
            "See specs/020-trace-equivalence-fidelity/ (T017 replaces this entrypoint).");
        return 0;
    }
}
