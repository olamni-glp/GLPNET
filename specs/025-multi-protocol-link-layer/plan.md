# Implementation Plan: Multi-Protocol Peer-to-Peer Link Layer for Distributed GLP

**Branch**: `025-multi-protocol-link-layer` | **Date**: 2026-06-06 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/025-multi-protocol-link-layer/spec.md`

**Gate status**: plan-approval gate COMPLETE (Gabi, 2026-06-06) — see [contracts/rulings-log.md](contracts/rulings-log.md). The 9 base link primitives, the approved guard set, and the three core fixes are approved-to-implement under language authority with the signatures recorded there.

## Summary

Add a strictly peer-to-peer, multi-protocol **link layer** that splits a single-instance GLP program at a shared writer/reader variable across two REPL instances over a real transport, preserving GLP semantics exactly (SRSW, writer-MGU, three-valued unification, suspend/reactivate, bind-once, per-link FIFO). Delivered **C#-first** (the mandated-default REPL) as the reference, Dart mirror after, proven by a cross-runtime Dart↔C# round trip. The base discipline is **ground-relay** (only ground terms cross; FR-010/040); `glink` (full variable-distribution transparency) is a later layer, out of MVP scope.

Approach (detail in [DESIGN-DOSSIER.md](DESIGN-DOSSIER.md) and `contracts/`):
- **9 base primitives** (approved): `link_setup/4`, `server_listener/3`, `client_connector/3`, `request_link/4`, `accept_link/4`, `link_send/3`(+`out_relay/3`), `link_recv/3`, `link_monitor/2`, `link_close/1`(+`/2`); host kernels `'_link_setup'/5`, `'_link_request'/5`, `'_link_accept'/5`, `'_link_send'/3`, `'_link_monitor'/2`, `'_link_close'/2`. Wrappers are composable GLP; the kernels are the language-authority surface.
- **Guard set**: add `@< @> @=< @>=` (non-negatable; total order over ground terms); fix `atom/1` (= runtime `string/1`), the compound-operand-suspend bug (FR-034), and the imported-reader reactivation gap (FR-035) by **wiring `handleMadAssignment`→`bindImportedReader`** (OQ-2 option 1); decline `== \== \= reader/1`; leave `=\=` untouched.
- **Reliability sublayer** (load-bearing net-new): per-link sequence/dedup, FIFO + reorder buffer, idempotent redelivery (fixes the live FR-021 duplicate-delivery crash), serializer cycle-guard + version byte + length/CRC + fragmentation, epoch/fencing (split-brain), distributed GC, bounded backpressure (default window **N=8**, scheme-overridable, below the seam).
- **Failure model**: faults as ordinary bound terms on a per-link monitor stream — `ok` / `closed(LinkId,Reason)` / `tempFail(LinkId,Reason)` / `permFail(LinkId,Reason)`; never a 4th verdict; disconnect never → Fail.
- **Transport leaves** behind one uniform seam (open/send-bytes/recv-bytes/close+fault) selected by `Scheme`; priority set: file/loopback → WS/WSS → HTTPS+HTTP/2 mTLS → MQTT → CoAP → BLE-L2CAP. Each P2P to the immediate peer (broker out of scope).
- **Tests**: net-new integration-test harness ([tests/integration-harness-design.md](tests/integration-harness-design.md)); per-transport unit + integration specs ([tutorials/](tutorials/)); coverage matrix ([tests/test-matrix.md](tests/test-matrix.md)); baseline regression gate.

## Technical Context

**Language/Version**: C#/.NET FIRST (the `out/csharp` mandated-default GLP REPL — reference); Dart (`glp_runtime/`) mirror authored AFTER the C# reference passes (FR-055/056). GLP language additions (primitives + guards) under language authority (CLAUDE.md §Language Authority).
**Primary Dependencies**: the GLP runtime — heap (`allocateVariable`, `bindWriter`/`bindVariable`/`bindImportedReader`, `onBind`), `mad_context` (the two seams: outbound `onMessageReady`, inbound `handleMadAssignment`), the runner guard evaluator (`_evaluateGuard`), `body_kernels`, the SRSW analyzer, the parser/lexer, the prelude; the byte-parity `PayloadSerializer`; per-platform native transport libraries (WebSocket, HTTP/2, MQTT client, CoAP, BLE) behind the `LinkTransport` seam; `codeconv` for C# reference ↔ Dart mirror correspondence on the language layer.
**Storage**: N/A — link state lives in-heap + per-instance registries (LinkId→handle, send-registry, reply-table); no database.
**Testing**: `bash test/run_all_tests.sh` (Section A runtime, B/C type-check, + new **Section R** skip-until-implemented); the net-new integration-test harness (multi-instance, fault injection, byte-identical baseline diff, Dart↔C# parity rig); per-transport feasibility tests (FR-016/SC-003); the adversarial corpus run identically on both REPLs (FR-031/SC-007); GEPA round-trip fidelity via Claude Agent seams only (FR-065/066).
**Target Platform**: Windows + Android (T4: each leaf accepted on at least one — FR-063/064); cross-runtime Dart↔C#.
**Project Type**: language runtime + host transport layer (runtime/compiler edits + native I/O leaves behind a uniform seam).
**Performance Goals**: per-link FIFO preserved; bounded backpressure (window N=8 default, producer suspends); fragmentation/reassembly for constrained MTU (CoAP/BLE); no OOM/crash on adversarial/oversized/cyclic input (bounded memory + stack).
**Constraints**: GLP invariants preserved EXACTLY (SRSW never relaxed by a flag); baseline REPL suite green before AND after every core-touching change (FR-067/SC-017); hand-authored C# link code in a **clobber-safe home OUTSIDE `out/csharp` and the gitignored `glp_runtime_net`** (FR-057); GEPA/optimizer LM work in the Claude harness only — never OpenAI/litellm/`OPENAI_API_KEY` (FR-066).
**Scale/Scope**: 9 base primitives + the guard set + the reliability sublayer + 6 priority transport leaves (of the ~14 enumerated; the rest are follow-on) + the Dart↔C# parity gate.

## Constitution Check

*GATE: Must pass before Phase 0. Re-checked after design.*

The project constitution (`.specify/memory/constitution.md`) is an **unfilled template**; the governing principles for this repo are **CLAUDE.md + docs/DISCIPLINE.md**: spec-first; GLP-first (logic in GLP, host = thin I/O); the GLP invariants (SRSW / writer-MGU / three-valued / suspend-reactivate / bind-once / per-link FIFO); **language authority** (no new primitive/guard without express approval — satisfied: gate complete); the **baseline-test gate**; and **preserve-working-code** (the `VariableEntry` imported-reader path is KEPT).

**Result: PASS.** No violations. One justified complexity (below).

## Project Structure

### Documentation (this feature)

```text
specs/025-multi-protocol-link-layer/
├── spec.md                         # requirements + 17 success criteria (committed)
├── plan.md                         # this file
├── tasks.md                        # dependency-ordered tasks (this block)
├── analyze.md                      # cross-artifact consistency check (this block)
├── DESIGN-DOSSIER.md               # consolidated design (+ build/ docx/pdf)
├── contracts/                      # link-primitives, guards, architecture-context,
│                                   #   codesign-proposal, rulings-log, example-http-link,
│                                   #   glp-correctness-review (from the review run)
├── tutorials/                      # per-transport tutorial + test specs + README
└── tests/                          # integration-harness-design, test-matrix
```

### Source Code (repository root)

```text
# C#-first reference (the mandated-default REPL):
csharp/glp_link/                    # NEW hand-authored, CLOBBER-SAFE home (FR-057) — no Dart
│                                   #   preimage, so codeconv mirror/scaffold never overwrites it
│   ├── primitives/                 # '_link_setup' '_link_request' '_link_accept'
│   │                               #   '_link_send' '_link_monitor' '_link_close'
│   ├── reliability/                # seq/dedup, FIFO+reorder, framing+version+CRC+fragment,
│   │                               #   epoch/fence, distributed GC, backpressure (window N=8)
│   ├── seam/                       # ILinkTransport / ILinkEndpoint (open/send/recv/close+fault)
│   └── transports/                 # per-scheme leaves: file/loopback, ws/wss, http2, mqtt, coap, ble-l2cap
out/csharp/                         # generated reference REPL (language-layer guard/heap edits land here via codeconv)

# GLP language layer (source-of-truth, then mirrored):
programs/lib/link.glp               # the GLP wrappers + types (Link, LinkId, Fault, ...)
programs/tests/typed/               # guard + primitive Section-B/C type-check tests

# Dart mirror (AFTER the C# reference works):
glp_runtime/lib/{runtime,compiler,multiagent}/   # the guard/fix edits + the Dart link mirror
glp_runtime/lib/link/               # Dart mirror of csharp/glp_link

# Tests:
test/run_all_tests.sh               # + Section R (skip-until-implemented) + the integration harness driver
```

**Structure Decision**: the language layer (primitives as host kernels + guards) is edited in the runtime (C# reference first via the generated `out/csharp`, Dart mirror after); the GLP wrappers/types live in `programs/lib/link.glp`; the **non-regenerable** transport + reliability + seam C# code lives in the new `csharp/glp_link/` package with **no Dart preimage** so a codeconv regen cannot clobber it (FR-057). The Dart mirror under `glp_runtime/lib/link/` follows once the C# reference passes.

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
|---|---|---|
| A net-new reliability sublayer straddling the language/leaf boundary | Every primitive's FR-017/021/051/052 guarantee depends on per-link seq/dedup/FIFO/reorder/epoch/GC/backpressure; the live runtime CRASHES on a duplicate frame today (FR-021 verified) | "Assume the transport is reliable" is false for every leaf (UDP/CoAP, MQTT QoS, brokered paths reorder/dup); the sublayer is load-bearing, not optional polish |
| Hand-authored C# outside the generated tree (`csharp/glp_link/`) | Transport leaves are codeconv's escalate-don't-guess worst case (async/native), and a regen of `out/csharp` would clobber hand-authored code with a Dart preimage (FR-057) | Authoring leaves inside `out/csharp`/`glp_runtime_net` gets overwritten by the next scaffold/mirror; a no-preimage package is the only safe home |
