<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 57a8b9fc-2569-4312-9cb0-17b9be75ff62
-->

# Implementation Plan: QUIC federation transport for the ynet oracle

**Branch**: `102-quic-federation-transport` | **Date**: 2026-09-04 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/102-quic-federation-transport/spec.md`

## Summary

Four hosts each run a local oracle over a grow-only, per-actor JSONL board; none of them can see
another's ops, so lanes claim overlapping work undetected. This feature adds the missing inter-host
leg: a **federation service** that binds a QUIC listener on a peer-reachable address, admits peers by
**mutually verified node identity** (never by address), ships board operations **push-on-append with a
60-second reconciliation pull backstop**, and folds them **union-by-id** so redelivery cannot
double-count.

The ordering rule that makes the first merge safe ships **with** the transport, not after it: a
leadership-bearing term is the namespaced triple `(space_id, era_counter, host_id)`, comparable
**only within a space**; `space_id` is minted per federation epoch by a recorded operator action; a
pre-existing operation with no recognised space belongs to a named **legacy** space and is therefore
retained-but-unordered; and the live fossil `leader_claim` is neutralised by **appending** a
retirement operation, never by deleting it.

Technically this is **additive C# inside `csharp/glp_crdtmsg`**, reusing four things that already
exist and are measured working on this host: `QuicLinkTransport` (mTLS + SPKI pinning, 491 lines,
binds `0.0.0.0:47890`), `NodeIdentity` (`nodeId = SHA-256(SPKI)`, Ed25519-primary), `Dot` /
`VersionVector` (dotted-version-vector identity and idempotent already-seen test), and the board's
existing per-actor JSONL op-logs. No second oracle is introduced and no existing fold is redesigned.

## Technical Context

**Language/Version**: C# / .NET 11.0 (`net11.0`), matching the existing `csharp/` solution.
**Primary Dependencies**: `System.Net.Quic` (BCL, `IsSupported=True` measured on this host);
existing in-repo `GlpRuntime.CrdtMsg.Route.QuicLinkTransport`; existing
`Ynet.Transport.Capability.NodeIdentity`; existing `GlpRuntime.CrdtMsg.Crdt.Dot`/`VersionVector`.
No new third-party package.
**Storage**: The board's existing grow-only per-actor JSONL op-logs under a resolvable board root
(`D:\coop\buildkit\sched\<kind>\<actor>\<actor>-<kind>-NNNNNN.jsonl`). Federation config and the
persisted node key live in a per-host config directory outside the repo. **No PGLite cluster is
created or touched** (Constitution VI-b).
**Testing**: `dotnet test` (xUnit) in `csharp/glp_crdtmsg.tests`, extended with a
`federation/` acceptance suite. Every one of SC-001..SC-013 gets a named test, and every
state-reporting criterion gets a **negative control** (SC-007).
**Target Platform**: Windows 11 (Gavriella, .NET 11.0.0) as the primary host; the code is
BCL-only and platform-neutral, so a Linux peer is a configuration difference, not a port.
**Project Type**: Library capability (`csharp/glp_crdtmsg/federation/`) plus one small operator
console (`csharp/ynet_federation/`) invoked through the signed `dotnet` host.
**Performance Goals**: An operation appended on one host is present in an admitted peer's fold
within **5 s** steady-state (FR-028 push leg) and within **120 s** of a restored link (FR-028 pull
backstop). These are the clarified numbers from ruling `Q-GLPNETG28-03`; they are asserted, not
aspired to.
**Constraints**:
- Smart App Control is ON and ENFORCING on this host (`VerifiedAndReputablePolicyState=1`) and has
  been measured blocking a freshly-built **unsigned apphost** with `0x800711C7`. FR-023 therefore
  requires that refusal to be caught and reported by name. Acceptance evidence is carried by the
  **test host**, which is already admitted; the operator console is invoked via `dotnet run`, which
  runs under the signed `dotnet` host.
- Inbound UDP/47890 needs a firewall rule authorised by ruling `Q-GLPNETG27-04`; creating it needs
  an elevated shell, which this lane cannot self-grant.
- Hostnames on this estate resolve to `fe80::` link-local only, so configuration carries **literal
  IPv4 addresses** (FR-003) and a name-resolution failure must not be reported as a transport
  failure.
**Scale/Scope**: 4 hosts, ~15 lanes, one board. First delivery federates **two** hosts end-to-end;
the requirements are written to hold for four and extending is configuration (spec Out of Scope).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — result at the end.*

| Principle | Gate-ability | Assessment |
|---|---|---|
| **I. Spec-First** | judgement | PASS. `spec.md` exists, is clarified (29 FRs / 13 SCs / 0 unresolved markers), and every planned artifact below cites the FR it satisfies. No code is planned that no FR asks for. |
| **II. Bug-Protocol / No-Workarounds** | judgement | PASS. The Smart-App-Control refusal is **not** worked around: FR-023 makes it a named, reported failure. The fossil op is **not** deleted: FR-029 supersedes it additively. Neither is a try/catch that masks a caller. |
| **III. SRSW inviolable** | machine | PASS (vacuous). No `.glp` source is touched; the token `skipSRSW` appears in no artifact of this feature. |
| **IV-a. Language Authority** | judgement | PASS. No guard, system predicate, body kernel, directive, type-system feature, or primitive type is added. This feature is C# transport, not GLP language. |
| **IV-b. Preserve Working Internals** | judgement | PASS. Additive only. `QuicLinkTransport`, `Dot`, `VersionVector`, `OpWal` and the existing fold are **consumed unchanged**; nothing is removed or rewritten. |
| **V. Claude-Only LM / No External API** | machine | PASS (vacuous). This feature has no LM path; `OPENAI_API_KEY` / `litellm` / `openai` appear in no artifact. |
| **VI-a. Additive-only, idempotent, single-head persistence** | machine | PASS (vacuous). No Alembic migration is added; the head stays `0010`. |
| **VI-b. Single PGLite cluster** | judgement | PASS. No PGLite cluster is created, moved, or consulted. The board substrate is the existing file-CRDT JSONL tree; federation config and the node key are per-host files outside the repo. |
| **VII. Test-Gated, Commit-Scoped Shipping** | advisory | PASS by construction. Baseline is the measured C# suite (190/190 on this host, 2026-09-04); every task re-runs it; commits are staged by name; shipping goes through the buildkit GitFlow. |
| **VIII. Single Source of Truth & Traceability** | judgement | PASS. One authoritative spec (`spec.md`); this plan references it rather than restating requirements; roadmap feature `quic-federation-transport` is linked to `specs/102-quic-federation-transport`. **No second oracle** is introduced — the spec's own assumption, honoured by extending `glp_crdtmsg` rather than forking it. |

**Result: no violations. Complexity Tracking is therefore empty.**

## Project Structure

### Documentation (this feature)

```text
specs/102-quic-federation-transport/
├── plan.md              # This file
├── research.md          # Phase 0 — measured ground truth + resolved unknowns
├── data-model.md        # Phase 1 — entities, invariants, state transitions
├── quickstart.md        # Phase 1 — the operator path, end to end
├── contracts/
│   ├── federation-config.md    # FR-002/003/024/025 — the configuration contract
│   ├── term-ordering.md        # FR-013..018, FR-026/027, FR-029 — the ordering contract
│   ├── federation-status.md    # FR-019..023 — the four-state observability contract
│   └── federation-wire.md      # FR-005..012, FR-028 — admission, framing, push/pull
├── checklists/
└── tasks.md             # Phase 2 output (/bk-tasks — NOT created by /bk-plan)
```

### Source Code (repository root)

```text
csharp/glp_crdtmsg/                     # EXISTING project — extended additively
├── route/QuicLinkTransport.cs          #   REUSED UNCHANGED (mTLS + SPKI pin, binds 0.0.0.0)
├── crdt/Dot.cs                         #   REUSED UNCHANGED (Dot, VersionVector.Contains ⇒ dedup)
└── federation/                         #   NEW — the whole feature
    ├── FederationTerm.cs               #   FR-013..016, FR-026/027  term + space-scoped compare
    ├── TermSpace.cs                    #   FR-026/027               epoch minting + legacy space
    ├── FederationOp.cs                 #   FR-009, key entity       op envelope: id, origin, term
    ├── FederationFold.cs               #   FR-010/011/012           union-by-id, order-independent
    ├── RetirementOp.cs                 #   FR-017/029               superseding retirement op
    ├── MergeGate.cs                    #   FR-018                   refuse non-space-aware merges
    ├── NodeIdentityStore.cs            #   FR-007                   PERSISTED key ⇒ stable pin
    ├── PeerSet.cs                      #   FR-006/007/008           pins by nodeId, empty=admit none
    ├── FederationConfig.cs             #   FR-002/003               load, validate, read back
    ├── FederationService.cs            #   FR-001/003/004, FR-028   listen, dial, push, pull backstop
    ├── FederationStatus.cs             #   FR-019..022              four states, explicit Unknown
    └── PolicyRefusal.cs                #   FR-023                   name 0x800711C7, don't generalise

csharp/ynet_federation/                 # NEW — operator console (run via the signed `dotnet` host)
├── YnetFederation.csproj
└── Program.cs                          #   verbs: status | config show | serve | dial | retire

csharp/glp_crdtmsg.tests/
├── YnetFederationTests.cs              #   EXISTING mechanism proof — kept, NOT the SC-001 evidence
└── federation/                         #   NEW acceptance suite, one test per SC + negative controls
    ├── TermOrderingTests.cs            #   SC-005, SC-012, SC-013
    ├── FoldConvergenceTests.cs         #   SC-002, SC-003, SC-011
    ├── AdmissionTests.cs               #   SC-004, SC-006
    ├── StatusSurfaceTests.cs           #   SC-007, SC-010  (positive AND negative control each)
    └── CrossHostAcceptanceTests.cs     #   SC-001, SC-008, SC-009 — SKIPS LOUDLY without a peer

docs/runbooks/
└── ynet-federation.md                  #   FR-024/025, SC-008/009 — enable, verify, and REVERSE
```

**Structure Decision**: the feature is additive C# inside the **existing** `csharp/glp_crdtmsg`
project, because that project already owns the transport (`route/`), the CRDT identity primitives
(`crdt/`), and the op store (`store/`) this feature composes. Creating a parallel project would
duplicate the transport seam and violate Constitution VIII. Exactly one new project is added —
`csharp/ynet_federation`, the operator console — because FR-002 and FR-019 require operator-facing
surfaces that a class library cannot provide, and because keeping the console separate keeps the
library free of `Main`-shaped concerns.

## Phase 0 — Research

Output: [`research.md`](./research.md). Every unknown that could have been marked NEEDS
CLARIFICATION was either measured on this host or resolved by a recorded ruling; `research.md`
records the measurement or cites the ruling. Nothing is carried forward unresolved.

## Phase 1 — Design & Contracts

Outputs: [`data-model.md`](./data-model.md), [`contracts/`](./contracts/),
[`quickstart.md`](./quickstart.md).

## Post-Design Constitution Re-check

Re-evaluated after Phase 1. **Still no violations.** The design added no project beyond the single
operator console justified above, introduced no migration, no PGLite cluster, no LM path, no GLP
language change, and removed nothing. The one judgement call worth naming explicitly — putting the
acceptance evidence in the test host rather than a standalone binary — is a **consequence of
Principle II**, not an exception to it: the host policy refusal is reported (FR-023) rather than
routed around.

## Complexity Tracking

*Empty — the Constitution Check recorded no violations, so there is nothing to justify.*
