## Issue 10: `/glptutorial-run` — deferred run-shapes + corpus-golden issues (feature 023)

**Status**: By design (3-Hybrid scope, Gabi-approved 2026-06-04). Tracked/surfaced, never silently mis-run.
**Discovered**: 2026-06-04 building `codeconv tutorials {preview,run,explain,propose}`.
**Affects**: A small minority of tutorial examples; the common shapes (section single/multi-compose, use-case project) run fully on the mandated C# backend.

### Deferred run-shapes (classified-and-flagged, not executed by the run layer yet)
The resolver classifies every example into a precise `Shape`; these three are recognised
but deferred (`supported=False` with a reason, or degrade to a clear backend error):
- **two-session** (e.g. ch04/03 — two files defining the same predicate that cannot
  co-load): multi-script exercises load in one session; a genuine collision surfaces as a
  backend load error (FR-017) and `propose` flags it.
- **bytecode-dump golden** (ch05/06 Phase B `=== BYTECODE FOR …===`): outcome-only
  comparison does not diff disassembly.
- **Flutter-only golden** (ch07/06, ch07/12 `*-flutter-trace.md`, no REPL trace): reported
  "not runnable via the REPL backend".

### Corpus-golden issues (route via `codeconv tutorials propose`, not runtime fixes)
- **ch04/07**: multi-clause `natural_number/1` used as a guard — spec-invalid (manual §8:
  defined guards must be single-unit-clause). The runtime correctly rejects the load; the
  golden's `✓ Loaded` is from a stale build → `propose` emits `LAYOUT_NORMALISE`.
- **ch04/08**: flatten golden predates the C# `is_list` fix; live Dart/C# now yield
  `F=[5,4,3,2,1]` → `propose` emits `STALE_ARTEFACT` (re-capture).
- **ch07 `programs/cssg_modules` drift gap**: the live use-case substrate is not vendored,
  so `sync --check` cannot guard it → `propose` emits `DRIFT_GAP`.

### C# runtime convergence (prerequisite, done)
The mandated C# REPL was repaired (7 Dart→C# conversion regressions) before this feature —
final sweep 33/38 MATCH, 0 runtime bugs, 0 regressions. Full record + codegen/convspec
follow-up: `docs/research/csharp-repl-convergence-fixes.md`.

---

## Issue 11: HTTP/3-QUIC-WS link (036) — three acceptance items deferred to a follow-up feature

**Status**: By design. Carved out (2026-07-02) into roadmap feature
`http3-quic-ws-link-full-acceptance` (epic `distributed-glp-connectivity`, promoted); brief at
`specs/036-http3-quic-ws-link/followup-full-acceptance-brief.md`. 036 tasks T003/T032/T036/T040
are marked `[>]` deferred (not done, not faked).
**Discovered**: 2026-06-27..07-02 while completing 036 on the primary dev host (Olamnit).
**Affects**: Only the final environment-dependent acceptance items — the core link is fully verified.

### What IS verified (single host, T037 quickstart validation 2026-07-02)
`glp-quick demo` passes every runnable criterion on this host, both stacks:
- **C# stack**: SC-001 (real on-wire QUIC/HTTP-3 handshake), SC-002 (full-duplex), SC-002b
  (peer-to-peer duplex mesh, to-routing + broadcast), SC-003 (≥4 isolated clients), SC-004
  (single-failure resilience), SC-005 (SPKI-pin-only trust) — all **PASS**.
- **Gleam Profile A** (`--stack gleam --profile a`): the same SC-001..SC-005 **plus SC-006**
  (cross-stack `csharp ≡ gleam`) — all **PASS**. Backed by `glp_quick` 18 pytest + `glp_link`
  104 xUnit green; REPL suite 524/525 (lone fail is a pre-existing, unrelated AOT-exe smoke case).

### The three deferred items (environment-blocked, not code-blocked)
- **Profile C — in-process QUIC on full BEAM** (`quicer`/MsQuic NIF): needs a `quicer` build with
  **MSVC + msquic**; this host has msys64/MinGW only. `--profile c` returns a clear
  `profile_c_not_built` (never a silent failure). See `gleam_quic/profile_c/README.md`.
- **True two-host LAN acceptance** (T040): needs the **gavri** host as the second endpoint. Same-host
  and cross-NIC runs exercise the identical real-QUIC path; the on-wire cross-host run is the only
  thing pending.
- **Marathon durability verify** (T003/T036): the planning-time run `mrun-15d7dd0ffbc2` was named but
  **never persisted** in any marathon store — there is nothing to resume. Per-stage commits served as
  the durable checkpoints for 036; a real persisted run is needed to verify SC-008.

## Feature 041 (crdtmsg-mvp) — MVP deferrals & escalations

All 13 success criteria are covered by the C# xUnit suites (`csharp/glp_crdtmsg.tests`,
`csharp/glp_wire_registry.tests`). The following are **scoped deferrals**, not defects — each is a
deliberate MVP boundary recorded here (Constitution VIII traceability):

- **Gleam/Dart codec parity (T055)** — the golden corpus + parity-vector definitions live in
  `test/parity/`; C# is the truth runtime and all four C# surfaces agree (SC-001, 48 conformance
  cells green). The cross-runtime Gleam/Dart decode-against-goldens run is **host-blocked** (same
  environment constraint as 036 Profile C / two-host), not code-blocked.
- **Two-host / real-QUIC e2e (SC-009)** — the demonstrator runs single-host two-client over the
  in-memory `InMemoryLinkFabric` (behind `ILinkTransport`). The `glp_quick_host` QUIC/WS adapter is a
  drop-in replacement for the seam (launched as a side-process per contract C20); the two-host on-wire
  run is host-blocked (the gavri second endpoint), consistent with 036.
- **`glp_quick_host` compile-ref (T002)** — intentionally NOT a compile-time ProjectReference of
  `glp_crdtmsg` (it is an `OutputType=Exe` stdio host with MsQuic native deps); wired as a side-process
  behind the seam to keep unit-test builds MsQuic-free.
- **Active CycleGuard (FR-031)** — op payloads are ground Terms that are acyclic by construction
  (immutable C# terms); `TermGuards.EnsureAcyclic` is enforced at op-apply as the spec names, and is
  defensive-by-construction (a real cycle is unreachable through the current builders).
- **COSE/JWS framing (T040)** — the cryptographic core (Ed25519 whole-sig + Biscuit-style chained
  sub-seals over the canonical binary) is complete and tamper/transcode-tested; wrapping the seal bytes
  in a full COSE_Sign1 CBOR structure is a thin presentation wrapper left for post-MVP.
- **Experimental GLP policy guard (T053, FR-014)** — **propose-first, NOT implemented.** The proposal
  artifact is `programs/crdtmsg/policy-guard-proposal.glp` (do NOT load/run). Implementation is gated on
  Gabi's DISCIPLINE §1.14 approval of the concrete guard signature. The shipped MVP routing uses the
  fixed C# `PolicyMatcher` (contract C23) regardless.
- **Aggregating solution (T001)** — the `csharp/` feature projects are built by direct `dotnet build`/
  `test` csproj path (as `quickstart.md` prescribes); there is no aggregating `.sln` to add them to.
- **GLP REPL baseline on Windows (T056)** — `bash test/run_all_tests.sh` is **environment-blocked on the
  glpnet Windows host**: the script hard-invokes `/home/user/dart-sdk/bin/dart` (the sibling Linux/Mac
  path from CLAUDE.md's appendix), which is absent here, so every case errors with
  "No such file or directory". This is a **pre-existing harness/env mismatch, independent of feature
  041** (which touches zero GLP runtime or test-program files). Feature 041's validation is the C#
  xUnit gates (253 tests green). The GLP suite needs the Windows runner (`glp_runtime/glp_repl.exe` or
  `dart run bin/glp_repl.dart`) wired into `run_all_tests.sh` — an escalation for Gabi, out of 041 scope.
- **C# REPL rejects a bare `_` in a top-level goal (feature 050)** — a query argument that is the
  anonymous writer `_` fails with `System.InvalidOperationException: Unsupported argument type:
  UnderscoreTerm` (`out/csharp/.../glp_engine.cs` `_SetupArgument`), before any goal work runs. Use a
  named variable instead: `main(producer, R).`, not `main(producer, _).` (the clause head still binds
  `R = []`). Affects only the interactive REPL goal parser — `_` inside a loaded `.glp` clause is fine.
  Relevant to the 050 two-host acceptance run (T043): drive the producer/consumer goals with named
  vars. The Dart REPL is not affected in the same way.
- **QUIC-unsupported host fails a `"quic"` link loud, never downgrades (feature 050)** — the genuine
  `QuicTransport` gates every path on `QuicTransport.IsSupported` (`QuicListener.IsSupported &&
  QuicConnection.IsSupported`, i.e. MsQuic present in the .NET runtime). On a host without it, a GLP
  goal opening a `"quic"` link aborts with `PlatformNotSupportedException` — by design (FR-002: real
  QUIC only, no TCP/loopback fallback). This is not a bug; it is the fail-closed contract. The xUnit
  suites skip-guard on `IsSupported` so CI on a QUIC-less runner reports the quic tests as passed-by-skip
  rather than failing. Both demo hosts (Olamnit/gavri) have QUIC; a third-party joiner without in-process
  QUIC reaches the mesh via the 036 Profile-A WS-to-QUIC side-process (FR-013a interop), still genuine QUIC
  on the wire.
- **Capability surface on the `"quic"` wire uses 041's TLV section `0x20`, not the binary header field
  (feature 050 US3)** — the macaroon rides as the 041 `CapabilitySlot` even/ignorable TLV *section*
  (`0x20`, envelope v2), which the binary canonical surface carries verbatim; the `Header.CapabilitySlot`
  *field* stays `null` on the binary wire (that field is JSON/DTO/CBOR-only and `BinaryTermCodec` loud-fails
  on a non-null one). This resolved research D-2 without any change inside 041's codec and without a JSON
  stopgap. Anyone reading the on-wire envelope must extract the capability from section `0x20`, not the
  header field.
