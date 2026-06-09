# Contract: Design Dossier Outline (the FR → section map)

**Feature**: `026-engine-review-dossier`

This is the **content contract** for `docs/research/repl-engine-separation/design-dossier.md`. The dossier is "delivered" when every section below is present and satisfies the cited requirement. This replaces an API/interface contract (there is no code interface — the dossier *is* the interface successor features consume).

## Required sections (13 sections, §0–§12)

| # | Section | Satisfies | Acceptance |
|---|---------|-----------|------------|
| 0 | **Executive summary + how to cite this dossier** | FR-001 | A successor author can find any design area and cite it by anchor |
| 1 | **Seam contract** (front-end/client vs embeddable engine; what crosses each way; components computed-but-dropped at the result boundary that must be promoted) | FR-002 | Lists the dropped components (var→writer map, suspended-goal detail, captured output) with `file:line`; tagged reuse/refactor/net-new |
| 2 | **Binary wire shapes** — client→engine payload **and** the net-new engine→client **result envelope** (incl. how a suspended result with unbound vars is encoded) | FR-003 | Complete field set for the result envelope: status, bindings, var-name→writer map, suspended-goal detail, captured/streamed output, errors, unbound-var encoding (US1 scenario 1) |
| 3 | **Wire reuse decision** — which transport/framing/codec reused as-is, which payload codecs net-new, with rationale | FR-004 | Names `FrameCodec`/`TcpTransport` reuse + dedicated codecs net-new, each with a reason |
| 4 | **Control-program startup + client model** — how the engine accepts clients, front-end as one client, single-engine/multi-client implications + hard prerequisites | FR-005 | States the multi-accept prerequisite for N-clients |
| 5 | **Liveness / crash-signal / restart model** — OS liveness signal, unrecoverable-state signal, supervision/restart | FR-006 | Names the host-layer shape + crash exit-code + restart→resume path |
| 6 | **Persistent-vs-ephemeral state model** — per-component classification, re-establish-from-definition rule, DB-abstraction API shape, bootstrap, restore-and-resume | FR-007 | A table classifying every significant state component; DB-abstraction API; bootstrap + resume behaviour |
| 7 | **Mailbox decision** — OS-level vs GLP-language, for MVP and long-term, with rationale | FR-008 | Both substrates compared; MVP + target named |
| 8 | **MVP slice(s)** — candidate bounded slice(s), each naming net-new deps it needs and what it defers; one MAY be advisory-recommended | FR-009, SC-007 | Each slice enumerates net-new deps + explicit defers; recommendation marked advisory |
| 9 | **Premise reconciliations** — ≥2 (compiler location; runtime-IL generation): requirement assumption, as-built reality + `file:line`, resolving decision, downstream consequence | FR-010, SC-002 | Both premises reconciled with code locations |
| 10 | **Open-question option sets** — every step-1 open question as 2–5 mutually-exclusive options w/ consequences + evidence; optional advisory recommendation; none recorded as settled | FR-011, FR-018, SC-003, SC-009 | 100% of open questions present as option sets; each option evidence-grounded + concise |
| 11 | **Epic feature breakdown** — ordered entries; each: kind, scope, why, depends-on, dossier-section ref; topologically valid | FR-012, FR-013, SC-004 | No forward dependency; every entry cites a section |
| 12 | **Risk register** — top risks, each with a mitigation reflected in the design or the breakdown ordering | FR-017 | Each risk has a named mitigation |

## Cross-cutting invariants (apply to every section)

- **INV-1 (read-only)**: the dossier and roadmap-seed are the only artifacts produced; no engine/runtime/REPL code changes (FR-015, SC-006).
- **INV-2 (classification + citation)**: every design area tagged `reuse`/`refactor`/`net-new` and cites ≥1 `file:line` (FR-014, SC-008).
- **INV-3 (re-verified reality)**: where as-built code contradicts a step-1 claim, the dossier records current reality (FR-016).
- **INV-4 (present-options)**: no genuine fork recorded as settled; recommendations are advisory; owner decides (FR-011).
- **INV-5 (self-contained)**: a reviewer can locate the design behind any wire-crossing component by reading the dossier alone (SC-005).
