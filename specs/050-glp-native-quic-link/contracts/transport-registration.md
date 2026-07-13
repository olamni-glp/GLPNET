# Contract: `"quic"` Transport Registration (FR-001, FR-002, FR-010, FR-011)

## Where
`out/csharp/glp_repl/Program.cs` — the composition root (`AfterEngineCreated`, lines 30-35). The ONE place allowed to reference both `glp_runtime_net` and `GlpLink`.

## Contract

Given the shipped `csharp/glp_link/transports/QuicTransport.cs`, registration adds the quic leaf alongside tcp/loopback:

```
GlpRuntime.Repl.Program.AfterEngineCreated = engine =>
{
    var link = LinkKernels.Install(engine.Runtime);
    link.Transports.Register(new TcpTransport());
    link.Transports.Register(new LoopbackTransport());
    // NEW (050): load the permanent shared trust material and register the genuine QUIC leaf.
    var cert  = LoadSharedCert("glpquick-cert");          // X509CertificateLoader.LoadPkcs12(glpquick.pfx)
    var pin   = LoadSpkiPin("glpquick-cert");             // read glpquick.fingerprint
    link.Transports.Register(new QuicTransport(cert, pin));
};
```

## Guarantees

- **G1 (FR-001)**: after registration, `link.Transports.Select(LinkScheme.Quic)` returns the `QuicTransport`; a GLP goal `server_listener(link_id("quic", ep(Host,Port), N), …)` / `client_connector(...)` reaches it through the unchanged kernels.
- **G2 (FR-002)**: no TCP/loopback fallback. If `QuicTransport.IsSupported` is false, listen/connect throws `PlatformNotSupportedException` (a loud fault surfaced on the `Faults` stream), never a silent downgrade.
- **G3 (FR-010)**: the cert is loaded from `glpquick-cert/` as a permanent credential — no time-boxed carve-out, no expiry-window shortcut in the registration path.
- **G4 (FR-011)**: trust is the mutual SPKI pin (`QuicTransport.PinValidationCallback`); no domain-name/public-CA/hostname shortcut. A non-pinned peer fails the handshake.
- **G5 (scope)**: registration is additive; tcp/loopback behaviour is unchanged; no kernel or GLP wrapper is modified (FR-019).

## Cert/pin loader

A small host-side helper (composition-root scope, `glp_link` or the repl shim) reads `glpquick.pfx` → `X509Certificate2` (with private key, for mutual presentation) and `glpquick.fingerprint` → the expected `base64(SHA256(SPKI))` string. Missing/unreadable cert material ⇒ loud startup failure (fail-closed), never a degraded no-pin mode.

## Tests
- xUnit `csharp/glp_link.tests/`: registering `QuicTransport` makes `Select(Quic)` succeed; `Select` on an unregistered scheme throws; `IsSupported=false` path yields a loud fault, not a fallback.
