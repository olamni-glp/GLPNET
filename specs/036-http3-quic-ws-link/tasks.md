# Tasks: HTTP/3 (QUIC) + WebSocket Channel-Link Prototype

**Input**: Design documents from `/specs/036-http3-quic-ws-link/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅ (cli, wire, stack-adapter)

**Tests**: Included — the spec's acceptance scenarios + the `glp-quick demo` conformance harness are load-bearing
for the success criteria (SC-001..SC-008), so contract/integration/unit test tasks are part of each story.

**Organization**: Grouped by user story (US1=P1 … US4=P4). Marathon stage order (research → corpus → distill →
skeleton/mock → implement+demo) is honored: research/corpus/distill and skeletons are Foundational (blocking);
the C#/.NET stack is the reference and MUST pass the full real-QUIC demo before the Gleam stack starts (FR-010).

## Format: `[ID] [P?] [Story] Description`
- **[P]**: parallelizable (different files, no dependency)
- **[Story]**: US1/US2/US3/US4 — or none for Setup/Foundational/Polish
- Exact paths from plan.md Project Structure.

---

## Phase 1: Setup (Shared Infrastructure)

- [x] T001 Create the `glp_quick/` Python package skeleton — `pyproject.toml` with `[project.scripts]
      glp-quick = "glp_quick.cli:app"` (Typer), `src/glp_quick/`, `tests/`, `.venv` — per plan.md (FR-007).
- [x] T002 [P] Scaffold the `/GLP-Quick` skill at `.claude/skills/glp-quick/SKILL.md` — a thin front end that
      invokes the `glp-quick` CLI (FR-007).
- [ ] T003 [P] Confirm marathon run `mrun-15d7dd0ffbc2` is resumable; record the research-strategy stage entry (FR-012/013).
      **BLOCKED**: the marathon-harness CLI (`marathon` subcommand) is absent from the installed buildkit
      (deploy-home venv `2026.6.13.1`); needs `/bk-marathon` or a buildkit upgrade. Not fabricated.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No US1–US3 behavioural work begins until this phase is complete (research-before-build FR-015;
skeleton-before-behaviour FR-017).

### Research / corpus / distillation (marathon stages 1–3) — ✅ COMPLETE (committed `10cdc452`/`e519613b`)
- [x] T004 Research-strategy + corpus plan (RFC 9114/9000/9001/9002/9220 + per-stack source plan) (FR-014/FR-012).
- [x] T005 [P] **C# corpus** built — 58 close-read notes (`research/corpus-csharp-01..05`) covering the named RFCs +
      .NET QUIC/MsQuic/Kestrel/RFC-9220 docs + GitHub samples (FR-014).
- [x] T006 [P] **Gleam/AtomVM corpus** built — 48 close-read notes (`research/corpus-gleam-01..04`) covering the
      named RFCs + AtomVM/Gleam/WASM-BEAM docs + repos (FR-014).
- [x] T007 Corpus **distilled** into close-read notes (`research/distillation-2026-06-27.md`); Phase-0 research
      items **closed**: MsQuic cross-platform support (Decision 2), RFC 9220 maturity → genuine WS-over-QUIC with
      the bootstrap seam isolated (Decision 3), SPKI cert-pin recipe (Decision 5), AtomVM genuine-QUIC
      infeasibility → two Gleam deployment profiles (Decision 8) (FR-015, SC-007).

### Skeleton / mock + shared contract (marathon stage 5; FR-017)
- [x] T008 Define the `StackAdapter` ABC in `glp_quick/src/glp_quick/stacks/base.py` per
      `contracts/stack-adapter-contract.md` (FR-009/FR-010).
- [x] T009 [P] Implement shared self-signed cert generation + **SPKI SHA-256 pin** in
      `glp_quick/src/glp_quick/cert.py` per `contracts/cli-contract.md` `cert generate` and research Decision 5:
      `cryptography` profile `subject==issuer`, `BasicConstraints(ca=False)`,
      `KeyUsage(digital_signature,key_encipherment)`, `EKU[serverAuth,clientAuth]`, EC P-256 or RSA-2048; export
      PFX (holder) + PEM (distribution); emit the SPKI-pin value (FR-003).
- [x] T010 [P] Define the GLP-message envelope + routing (from/to/broadcast, ground-relay) in
      `glp_quick/src/glp_quick/repl_link.py` per `contracts/wire-contract.md` (FR-008/008b, FR-018).
- [x] T011 Wire the CLI surface (`cli.py`: `cert` / `--server` / `--client` / `demo`) as a **top-down skeleton +
      mocks, no behaviour** (FR-017, FR-007). [`cert generate` wired to the tested utility; roles/demo are labelled skeletons.]
- [x] T012 Skeleton the C# QUIC+WS transport leaf — `QuicTransport` / `QuicEndpoint` / `WebSocketOverQuic`
      (genuine RFC 6455 over a raw bidi `QuicStream`) / `ConnectBootstrap` (minimal CONNECT-style bootstrap; RFC
      9220 Extended-CONNECT seam, later) as `ILinkTransport`/`ILinkEndpoint` **stubs** in
      `csharp/glp_link/transports/`, reusing spec 025's seam + reliability sublayer + `FrameCodec` (FR-017, FR-018).
- [x] T013 **GATE (Constitution IV-a — Language Authority)**: determine whether bridging GLP REPL messages onto
      the link needs any *new* GLP primitive beyond spec 025's approved set. Default = none. **If one is needed,
      STOP and obtain owner approval before any behavioural implementation.**
      **PASS (none)**: QUIC+WS is a host-level `ILinkTransport` leaf (below GLP, per the seam's own contract);
      mesh `to`/`broadcast` routing is Python control-plane; payload uses 025's approved ground-relay + link
      primitives. No new guard/predicate/kernel/directive/type. Re-STOP if US1/US2 reveals a need.
- [x] T013a **GATE — residual verification probes (research §6; escalate-don't-guess)** before any QUIC code:
      (1) confirm `QuicListener.IsSupported == true` on the actual demo host (one-line C# probe); (2) confirm
      msquic is present in the pinned .NET 9 runtime; (3) if Gleam Profile A is targeted later, confirm AtomVM-WASM
      `open_port` spawn works (build-time). On any probe failure, STOP and report — do not fake or work around.
      **PASS** on Win11 26200 / .NET runtime 10.0.9: `QuicListener.IsSupported=True`, `QuicConnection.IsSupported=True`,
      msquic present. Probe (3) deferred (Gleam Profile A is US3, not yet targeted).

**Checkpoint**: corpus distilled (done), research items closed (done), skeletons + shared contract in place, IV-a gate + residual probes cleared.

---

## Phase 3: User Story 1 — Real QUIC + WS link, running GLP (Priority: P1) 🎯 MVP

**Goal**: One server + one client on two LAN hosts complete a genuine QUIC/HTTP-3 handshake (shared cert only),
bring up a WebSocket link, and exchange a GLP message — full-duplex.
**Independent Test**: On two hosts by IP, observe a real on-wire handshake (not loopback), an established WS link,
and a GLP send→receive round-trip, with both ends sending concurrently.

### Tests for US1
- [x] T014 [P] [US1] xUnit loopback test for the QUIC+WS transport leaf in `csharp/glp_link.tests/QuicTransportTests.cs`
      — 5 tests, **real same-host msquic handshake** + RFC6455 round-trip (both dirs, FIFO, 100KB/64-bit length,
      graceful close, **cert-pin mismatch rejected**). All green within the 104-test glp_link suite.
- [x] T015 [P] [US1] pytest for cert generation + **SPKI SHA-256 pin** trust accept/reject in
      `glp_quick/tests/test_cert.py` (9 tests, research Decision 5).

### Implementation for US1
- [x] T016 [US1] **real QUIC handshake** (`System.Net.Quic`, `IsSupported`-gated, cross-platform) with the
      **shared-cert SPKI SHA-256 pin** in `QuicTransport.cs` — mutual validation, never `return true`, waives only
      no-CA-chain + hostname-mismatch (FR-001/FR-003, SC-001/SC-005). Verified by a real two-process handshake.
- [x] T017 [US1] **genuine WebSocket link = RFC 6455 over one bidi `QuicStream`** (`WebSocketOverQuic.cs`; opcodes
      text/binary/close/ping/pong, FIN/continuation, 7/16/64-bit length, unmasked) via the **minimal CONNECT-style
      bootstrap** (`ConnectBootstrap.cs`); RFC 9220 Extended-CONNECT isolated behind the seam (FR-002, Decision 3).
- [x] T018 [US1] `csharp` `StackAdapter` (`start_server`/`start_client`/`health`/`stop`) in `stacks/csharp.py`,
      launching + supervising the C# endpoint (`csharp/glp_quick_host` exe, run via `dotnet <dll>`) (FR-007).
- [~] T019 [US1] Bridge to the link in `repl_link.py` — **GLP-message envelope bridge done** (Handle send/recv;
      verified two-process full-duplex, T020). **REMAINING**: bridging the live `out/csharp/glp_repl` *process*'s
      message I/O (needs the REPL's spec-025 link-message interface) — follow-up.
- [x] T020 [US1] **Full-duplex** message flow over one link — both ends send/receive concurrently; verified
      (client↔server + 5-frame FIFO) via `test_csharp_adapter.py` (FR-008a, SC-002).
- [x] T021 [US1] Clear, distinct failure reporting — `glp_quick_host` maps `cert_mismatch` / `alpn_version_mismatch`
      (quic_unsupported) / `udp_blocked` / `server_not_ready` to distinct exit tokens; `cert_mismatch` verified by
      test (xUnit + adapter pytest). No silent hang / half-open (FR-019).
- [x] T022 [US1] `demo.py` + CLI `demo` — genuine same-host run reports SC-001/SC-002/SC-005 **PASS** over the real
      link; **honestly NOT-RUN** for SC-003/004 (needs US2), SC-006 (US3), and the true two-host LAN acceptance
      (needs a 2nd host) — no over-claim. Machine-name addressing path implemented (`ResolveHost`), IP path verified.

**Checkpoint**: MVP — a real QUIC+WS link runs **GLP-message exchange** full-duplex between two processes (same-host
verified; cross-host LAN = the same code path pending a 2nd host). Live-`glp_repl`-process bridge + ≥3-client mesh remain.

---

## Phase 4: User Story 2 — One server, several concurrent clients (Priority: P2)

**Goal**: One server serves ≥3 concurrent isolated client links; the REPLs form a peer-to-peer duplex mesh; one
client's failure does not disturb the others.
**Independent Test**: Launch one server + ≥3 clients; each completes an independent round-trip; kill one — others continue.

> **STATUS (DONE & verified)**: `glp_quick_host --role server` is now a **multi-accept mesh router** —
> `QuicTransport.CreateListenerAsync` (one bound `QuicListener` accepting N isolated links) + a `Mesh` router
> (server's own stdio endpoint + per-client links keyed by announced id; `to`/`broadcast` routing). Verified on
> this host with ≥4 concurrent clients (`glp-quick demo --clients 4` → SC-001/002/003/004 + mesh PASS).

### Tests for US2
- [x] T023 [P] [US2] Integration test: ≥3 concurrent clients, each an independent isolated round-trip (SC-003) —
      `glp_quick/tests/test_mesh.py::test_three_client_mesh_routing_and_isolation`.
- [x] T024 [P] [US2] Integration test: single-client failure leaves the rest functional (SC-004) — same test, isolation leg.

### Implementation for US2
- [x] T025 [US2] Server accepts up to `--max-clients` (≥3) concurrent **isolated** links — each its own
      `QuicConnection`+stream (independent 025 epoch/seq/window) via `QuicListenerHandle.AcceptAsync` (FR-005/FR-011).
- [x] T026 [US2] Over-capacity policy: the (N+1)th client gets a clear `over_capacity` notice then close
      (`RejectOverCapacityAsync`) — verified by `test_mesh.py::test_over_capacity_rejected`.
- [x] T027 [US2] Peer-to-peer **duplex mesh** routing — `to`-routing + `broadcast` fan-out in the `Mesh` router
      (FR-008b, SC-002); verified (c0→c1 direct + c0→broadcast to all).
- [x] T028 [US2] Session isolation — a client drop removes only that link (`ClientPumpAsync` cleanup), siblings stay
      `linked` (FR-006, SC-004). [Mid-session path-MTU-degradation drop = the same drop path; explicit MTU test = follow-up.]
- [x] T029 [US2] `glp-quick demo --clients N` automates SC-003/SC-004 in `demo.py` (verified `--clients 4`).

**Checkpoint**: concurrent, isolated, failure-resilient ≥4-node mesh over the real QUIC+WS link. **DONE (same-host).**

---

## Phase 5: User Story 3 — Two interchangeable stacks behind one CLI (Priority: P3)

**Goal**: The same CLI/wire contract drives the second stack (Gleam), shipped as **two deployment profiles**
(A: AtomVM + native QUIC side-process; C: full BEAM + `quicer`/MsQuic in-process); interchangeable **at the
channel-link contract** (not at QUIC termination — genuine QUIC on bare AtomVM/WASM is infeasible, research §F2).
**Independent Test**: Run `glp-quick demo --stack gleam --profile c` (and `--profile a`); each reaches a real
QUIC+WS link + GLP round-trip with the same observable outcomes as `csharp`.

> **BLOCKED until US1 + US2 pass the full real-QUIC LAN demo** — the C#/.NET reference must be complete first (FR-010).
>
> **STATUS (blocked — toolchain absent)**: `gleam`, `erl`, `rebar3`, `escript` are all absent on this machine.
> US3 needs Erlang/OTP + Gleam + (Profile C) the `quicer` NIF over MsQuic + (Profile A) AtomVM/WASM — a major
> toolchain install — AND is hard-gated behind US1+US2 (FR-010). Honestly deferred (constitution II), not faked.

- [ ] T030 [US3] **GATE (FR-010)**: confirm the C# reference passes the full real-QUIC LAN demo (SC-001..SC-006) before starting Gleam.
- [ ] T031 [US3] Scaffold greenfield `gleam_quic/` — `gleam.toml`, `src/` (`quic_link.gleam` channel-link contract,
      `websocket.gleam`, profile dispatch), `profile_a/` (AtomVM logic + Node WASM host + native QUIC side-process
      over length-prefixed local IPC) and `profile_c/` (full BEAM + `quicer`/MsQuic in-process).
- [ ] T032 [US3] Wire **Profile C** first (full BEAM + `quicer`/MsQuic, genuine in-process QUIC) as the Gleam stack's
      genuine-QUIC path; set `capabilities()` = `{real_quic: true, quic_termination: "in_process"}` (Decision 8).
- [ ] T032a [US3] Wire **Profile A** (AtomVM logic + **native genuine-QUIC side-process**); attribute `real_quic`
      truthfully to the side-process — `{real_quic: true, quic_termination: "side_process"}`; never simulate
      in-runtime QUIC (constitution II). Run the §6 AtomVM `open_port` probe first.
- [ ] T033 [US3] Implement the `gleam` `StackAdapter` (with `profile() -> "a"|"c"`) in
      `glp_quick/src/glp_quick/stacks/gleam.py`, built out in stages against the identical wire/CLI contract,
      selecting the profile by `--profile` (FR-009/FR-010).
- [ ] T034 [P] [US3] Cross-stack conformance vector — identical observable outcomes for `csharp` vs `gleam`
      (each profile) at the channel-link contract level (SC-006).

**Checkpoint**: both stacks interchangeable from the operator's view (or Gleam limitation reported honestly).

---

## Phase 6: User Story 4 — Evidence-grounded, durable marathon (Priority: P4)

**Goal**: The corpus is complete + distilled, and the marathon resumes objectively across interrupts.
**Independent Test**: Inspect corpus counts/coverage + distillation depth; interrupt and resume the run.

- [x] T035 [US4] Corpus completeness verified — **106 close-read notes (58 C# + 48 Gleam)** covering RFC
      9114/9000/9001/9002/9220 (+8441/7301/6455), distilled in `distillation-2026-06-27.md` (SC-007).
- [ ] T036 [US4] Verify marathon durability — interrupt + resume of `mrun-15d7dd0ffbc2` reports the objective next step and skips completed stages (FR-013, SC-008).

---

## Phase N: Polish & Cross-Cutting

- [ ] T037 [P] Docs: run `quickstart.md` validation; record any limitation in `docs/known-issues.md`.
- [ ] T038 Run the GLP REPL suite `bash test/run_all_tests.sh` — confirm **no regression** (constitution VII).
- [ ] T039 [P] `glp_quick` pytest + `csharp/glp_link.tests` xUnit suites green.
- [ ] T040 Run `quickstart.md` end-to-end on two LAN hosts (final acceptance).

---

## Dependencies & Story Completion Order

```
Setup (T001-T003)
  └─▶ Foundational (T004-T013a)  [research+corpus+distill ✅ DONE (T004-T007); skeletons FR-017; IV-a gate T013; residual probes T013a]
        └─▶ US1 / P1 MVP (T014-T022)
              └─▶ US2 / P2 (T023-T029)
                    └─▶ [FR-010 GATE T030] ─▶ US3 / P3 (T031-T034, incl. Profile C T032 + Profile A T032a)
                                                    └─▶ US4 / P4 (T035 ✅, T036)
                                                          └─▶ Polish (T037-T040)
```
- US1 is the MVP and is independently shippable. US2 builds on US1's link. US3 (Gleam) is hard-gated behind a
  complete C# reference (FR-010). US4 verifies process/evidence and is last.

## Parallel Execution Examples
- **Foundational**: T005 ‖ T006 (two corpora); then T009 ‖ T010 (cert ‖ envelope) after T008.
- **US1 tests**: T014 ‖ T015 before implementation.
- **US2 tests**: T023 ‖ T024 in parallel.
- **Cross-stack**: T034 [P] once T033 lands.

## Implementation Strategy
- **MVP = User Story 1** (T001–T022): a genuine QUIC+WS link running GLP full-duplex between two LAN hosts.
- Deliver incrementally US1 → US2 → US3 → US4; each phase has its own checkpoint and is independently testable.
- Honor the gates: research-before-build (Phase 2 before US1), Language-Authority IV-a (T013), and
  C#-reference-before-Gleam (T030).
