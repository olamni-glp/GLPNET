// glp_repl executable entrypoint — feature 020 (T017).
//
// The REAL REPL is the converted `GlpRuntime.Repl.Program` (from
// glp_runtime/bin/glp_repl.dart), compiled into the glp_runtime_net library
// (out/csharp/bin/glp_repl.cs). This file is the executable's thin startup
// shim: it delegates to that converted entrypoint so `glp_repl.exe` runs the
// converted runtime instead of the former placeholder.
//
// The structured trace instrumentation the differential equivalence oracle
// (feature 020) consumes is added candidate-side inside the converted runtime
// (see specs/020-trace-equivalence-fidelity/contracts/trace_normalization.md);
// the Dart golden under glp_runtime/ is never modified (R10 / HARD GATE 6).

using System.Threading.Tasks;

namespace GlpRuntime.Repl.Host;

internal static class EntryPoint
{
    // Task-returning async entry point; forwards argv to the converted REPL.
    private static Task Main(string[] args) => GlpRuntime.Repl.Program.Main(args);
}
