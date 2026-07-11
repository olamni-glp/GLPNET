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

using GlpRuntime.CrdtMsg.Bridge;
using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Seam;
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
            // feature 050 — register the genuine QUIC+WS leaf (036) so a GLP goal over a "quic"
            // link_id reaches it through the unchanged 025 kernels. Load the PERMANENT shared trust
            // material (cert + SPKI pin) from glpquick-cert/; fail-closed at startup if absent
            // (FR-010/FR-011). No TCP/loopback fallback — QuicTransport refuses loudly on a host
            // without QUIC (FR-002).
            var (quicCert, quicPin) = SharedCertMaterial.LoadFromRepo();
            link.Transports.Register(new QuicTransport(quicCert, quicPin));
            // feature 050 US3 (T026-T028) — macaroon capability gate, verify-before-act (FR-008/9).
            // Load the static-macaroon root key out-of-band alongside the cert (beacon model,
            // fail-closed if absent) and mint this endpoint's presented static macaroon. The gate
            // runs in LinkEstablish BEFORE any "quic" endpoint is opened; every outbound envelope
            // carries the macaroon in the capability slot (section 0x20, envelope v2); inbound
            // gated actions re-verify it. Refusals are recorded (ProvenanceOutcome.Refused) and
            // fail closed — never a crash, never a silent drop.
            var (macaroonRootKey, staticMacaroon) = StaticMacaroonMaterial.LoadFromRepo();
            var quicGate = new MacaroonLinkGate(macaroonRootKey, staticMacaroon);
            link.CapabilityGates.Register(LinkScheme.Quic, quicGate);
            // feature 050 US2 (T018) — the "quic" link's L5 wire payload is a 041 crdtmsg envelope
            // (FR-005). Inject the CrdtMsgPayloadCodec for the Quic scheme; loopback/tcp keep the
            // default ground-relay blob (byte-for-byte unchanged). The kernels stay codec-agnostic —
            // LinkEstablish selects the per-link codec from this registry at establishment.
            // US3: the codec is capability-gated — it attaches/verifies the macaroon slot.
            link.PayloadCodecs.Register(LinkScheme.Quic, new CrdtMsgPayloadCodec(quicGate));
        };
        return GlpRuntime.Repl.Program.Main(args);
    }
}
