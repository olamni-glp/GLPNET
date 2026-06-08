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

using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.Repl.Host;

internal static class EntryPoint
{
    // Task-returning async entry point; forwards argv to the converted REPL.
    private static Task Main(string[] args)
    {
        // feature 025 — composition root: wire the hand-authored link layer into the REPL
        // engine the converted Program builds. This is the ONLY place that may reference both
        // glp_runtime_net and GlpLink (the library can't, without a reference cycle). The hook
        // runs once, right after engine construction (out/csharp/bin/glp_repl.cs).
        GlpRuntime.Repl.Program.AfterEngineCreated = engine =>
        {
            var link = LinkKernels.Install(engine.Runtime);
            link.Transports.Register(new TcpTransport());        // first real cross-process leaf (127.0.0.1)
            link.Transports.Register(new LoopbackTransport());   // in-process hermetic substrate
        };
        return GlpRuntime.Repl.Program.Main(args);
    }
}
