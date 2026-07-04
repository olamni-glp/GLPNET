# Implementation Plan: CRDT Multi-Format Messaging MVP

**Branch**: `041-crdtmsg-mvp` | **Date**: 2026-07-04 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/041-crdtmsg-mvp/spec.md`

## Summary

Prove ONE message end-to-end — multi-format (binary-term/JSON/YAML/CBOR against one abstract model), multi-version (skip-tolerant TLV sections + loud-fail decode), security-first (macaroon capability + verify-before-act; whole + sub-content Ed25519 signatures in COSE/JWS + Biscuit chain), carrying a **CRDT payload whose mandatory demonstrator is a Fugue+Peritext rich-text document** (op-based JSON-CRDT base; store-side delta-CRDT + Merkle anti-entropy over the 040 append-only-WAL, store ships first), routed over the shipped 036 QUIC/WS transport with a unified router-opaque header + fixed routing policy. Technical approach: **extend the existing C# workspace** (`csharp/`), reusing `glp_result_codec` (Section-15 term codec + loud-fail discipline), `glp_link` (frame/WS), and `glp_quick_host` (QUIC/SPKI). All design escalations are ruled (synthesis §6); the three 2026-07-04 clarifications (C# primary runtime; rich-text mandatory; GLP guard gated on §1.14) are binding.

## Technical Context

**Language/Version**: C# / .NET — same target framework as the shipped `csharp/` projects (`glp_result_codec`, `glp_link`, `glp_quick_host`). GLP (typed) for the experimental policy guard **only**, and its implementation is gated on §1.14 owner approval (FR-014).
**Primary Dependencies**: reuse `glp_result_codec` (`TermCodec`, `ResultEnvelopeCodec` loud-fail invariant, golden-vector harness), `glp_link` (FrameCodec / WS framing), `glp_quick_host` (MsQuic QUIC + RFC 6455 WS, SPKI pin). New: `System.Text.Json` (JSON surface), `YamlDotNet` (YAML surface), `System.Formats.Cbor` (CBOR deterministic surface), an Ed25519 provider (NSec/libsodium **or** BouncyCastle — Phase-0 pick), COSE/JWS seal structures, a macaroon implementation (reuse an existing internal design — Phase-0 pick). Gleam/Dart parity vectors consume the same goldens.
**Storage**: append-only **op-WAL** in the 040 responder-store shape (temp → SHA-256 verify → atomic commit → journal) + rebuildable projections; **delta-state CRDT** payload state + a **Merkle tree** for anti-entropy reconciliation; durable provenance records (incl. refusals). No second PGLite working-data cluster (Constitution VI-b).
**Testing**: xUnit (C#). Golden-corpus **conformance matrix** across the 4 surfaces (extends `glp_result_codec` golden discipline); **loud-fail fuzz** (extends `LoudFailFuzzTests`); **convergence property tests** (randomized op-permutation → identical state); **tamper/signature** tests (whole + sub-content); **Fugue no-interleaving** + **Peritext span-preservation** tests; crash-rebuild zero-loss test. Gleam/Dart codec parity harness.
**Target Platform**: Windows + LAN shared-cert mesh; **single-host multi-client** for the end-to-end demonstration (SC-009) — two-host (`gavri`) and Profile-C (quicer) e2e are host-blocked and out of MVP scope.
**Project Type**: protocol/runtime library + host in the C# workspace, integrated with the GLP runtime through the existing 036/040 seams.
**Performance Goals**: correctness- and convergence-first — no hard latency/throughput target for the MVP; the shipped 64 MiB frame guard and reliability-window (N=8) are inherited unchanged.
**Constraints**: ground-terms-only across the wire; acyclic payloads (`CycleGuard` → transport fault); endianness layering law (BE frame ⊃ LE term payload) **frozen**; canonical-for-signing = deterministic binary term encoding (signatures survive lossless transcode); router payload-opacity (bytes forwarded verbatim); **Claude-only** for the E9 agentic qmedit-DSL↔CDDL translation (Constitution V); the E6 GLP guard stays **propose-first** (Constitution IV-a / §1.14).
**Scale/Scope**: one message type through the full stack + one Fugue+Peritext rich-text CRDT document; 2-peer convergence; LAN.

No `NEEDS CLARIFICATION` remain — E1–E9 are ruled (§6) and the three clarifications (spec §Clarifications) close the rest.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Verdict | Note |
|---|---|---|
| I. Spec-First | ✅ PASS | spec.md + clarify complete; plan traces to spec FR/SC + synthesis §6/§7. |
| II. Bug-Protocol / No-Workarounds | ✅ PASS | loud-fail everywhere; no try/catch masking; faults surface as ground terms on the monitor lattice, never a 4th unification verdict. |
| III. SRSW inviolable | ✅ PASS | no `skipSRSW`; the GLP guard (if approved under §1.14) obeys SRSW. |
| IV-a. Language Authority | ✅ PASS (by deferral) | **FR-014's experimental GLP guard is a language extension → design + propose only; implementation gated on §1.14 owner approval.** The MVP ships the fixed policy DATA fields + per-hop matcher regardless. |
| IV-b. Preserve Working Internals | ✅ PASS | reuse `glp_result_codec`/`glp_link`/`glp_quick_host`; remove nothing. |
| V. Claude-Only LM | ✅ PASS | E9 qmedit-DSL↔CDDL agentic translation runs in Claude (Agent-tool/MCP) — no OpenAI/litellm/external API on any LM path. |
| VI-a. Additive/idempotent migrations | ✅ PASS | any store/provenance schema is additive + single-head; op-WAL is append-only by construction. |
| VI-b. Single PGLite cluster | ✅ PASS | store uses the 040 file-WAL shape; no second working-data cluster created inside the repo. |
| VII. Test-gated, commit-scoped shipping | ✅ PASS | baseline green before change; scoped commits (marathon checkpoints); GitFlow ship. |
| VIII. Single source of truth & traceability | ✅ PASS | references synthesis §6/§7 + spec; roadmap→pipeline→marathon traceable. |

**No violations** → Complexity Tracking is empty.

Two standing gates the plan explicitly carries (not violations, obligations):
- **IV-a / §1.14**: the GLP policy guard's concrete signature must be owner-approved before any implementation task runs.
- **V**: the agentic DSL translation is Claude-only.

## Project Structure

### Documentation (this feature)

```text
specs/041-crdtmsg-mvp/
├── plan.md              # This file
├── research.md          # Phase 0 — library picks + ruled-decision consolidation
├── data-model.md        # Phase 1 — entities, envelope layout, CRDT state
├── quickstart.md        # Phase 1 — how to run the e2e demonstrator + conformance suite
├── contracts/           # Phase 1 — envelope/header/registry/CRDT-op/capability/signature/policy-guard contracts
└── tasks.md             # Phase 2 (/bk-tasks — not created here)
```

### Source Code (repository root)

```text
csharp/
├── glp_result_codec/            # REUSE — Section-15 TermCodec, ResultEnvelopeCodec loud-fail, goldens, Lean proofs
├── glp_il_codec/                # REUSE — PayloadHeader (0x10 IL) — source of duplicated payloadType constant #1
├── glp_link/                    # REUSE — FrameCodec / WS framing (BE 22-byte header, CRC-32, 64 MiB guard)
├── glp_quick_host/              # REUSE — MsQuic QUIC + WS, SPKI-pin membership (036 transport of record)
│
├── glp_wire_registry/           # NEW — the ONE payloadType registry (unify the two duplicated constant sites; 0x10 IL, 0x11 RESULT_ENVELOPE, 0x12+ messaging); functor allocation; compat modes
├── glp_wire_registry.tests/     # NEW
├── glp_crdtmsg/                 # NEW — the messaging feature:
│   ├── model/                   #   abstract message model + 4-surface codecs (binary-term/JSON/YAML/CBOR)
│   ├── envelope/                #   TLV skippable sections, criticality ranges, two-tier version tolerance, loud-fail
│   ├── header/                  #   unified header {msg_id,from,to,seq} + policy {targets,waypoints,excludes} + capability slot (v2 additive)
│   ├── cap/                     #   macaroon verify-before-act + amulet slot (Check≥128b) + provenance
│   ├── sig/                     #   whole + sub-content Ed25519 seals in COSE/JWS + Biscuit chain; per-peer key enrol
│   ├── crdt/                    #   op-based JSON-CRDT (ground-term ops, DVV dots, hash-chained op ids), semantic tombstone
│   ├── crdt/richtext/           #   Fugue sequence + Peritext spans (MANDATORY demonstrator)
│   ├── store/                   #   delta-CRDT + Merkle anti-entropy over the 040 op-WAL shape; rebuildable projections (SHIPS FIRST)
│   ├── route/                   #   router-opaque delivery over glp_quick_host; @name loud-fail; dedup (msg_id + per-link seq); fixed policy matcher
│   └── schema/                  #   dual-DSL functor registry (qmedit-DSL ↔ CDDL, Claude-agentic), both forms stored
└── glp_crdtmsg.tests/           # NEW — conformance matrix, loud-fail fuzz, convergence, tamper, Fugue/Peritext, crash-rebuild

programs/                        # GLP side — the experimental policy guard PROPOSAL only (no impl until §1.14 approval)
└── crdtmsg/                     #   guard design note + proposed typed signature (propose-first artifact)

test/parity/                     # Gleam/Dart codec parity vectors (goldens shared with glp_result_codec)
```

**Structure Decision**: Extend the existing C# workspace rather than start a new tree — the transport, frame, and term codec are already shipped there (Constitution IV-b: reuse, don't reinvent). Two new library projects: `glp_wire_registry` (the single payloadType/functor registry that unifies the currently-duplicated constants) and `glp_crdtmsg` (the messaging feature, internally layered store→crdt→envelope/header→cap/sig→route→schema following the §7 dependency order, **store first**). The GLP guard lives under `programs/crdtmsg/` as a **proposal artifact only**.

## Complexity Tracking

> No Constitution violations — this section is intentionally empty.
