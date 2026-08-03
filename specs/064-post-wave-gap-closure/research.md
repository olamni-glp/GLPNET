# Phase 0 Research — 064 post-wave gap closure

## D1 — Distributed unification: port the C# protocol 1:1

- **Decision**: implement `dist_unify` in glp_gleam as a 1:1 port of the C# link's remote-binding protocol: the same message kinds, the same ordering guarantees over the reliability layer, the same writer-MGU checks (only writers bind; never writer-to-writer; reader suspension propagates as remote suspension entries). The FCP Savannah emulator (`unify.c`, `emulate.c`) is the tie-breaker when the C# code needs interpretation.
- **Rationale**: clarify Q1 ruled full C# parity; the C# link is the declared frozen reference; a fresh design would re-open parity debt and violate DISCIPLINE §1.13 (FCP reference architecture).
- **Alternatives considered**: single-hop writer-forwarding subset (rejected by Q1 ruling); fresh Dijkstra-style design (rejected — reinvention against §1.13).

## D2 — Quiescence oracle: same algorithm as C#

- **Decision**: port the C# quiescence oracle: a census over local goal states (running/suspended) combined with in-flight message accounting at the link seam; a link fault surfaces as a fault-lattice event and forces the oracle to `faulted`, never `quiescent`.
- **Rationale**: same-parity argument as D1; the fault interaction is spec-mandated (edge case list).
- **Alternatives considered**: full Dijkstra–Scholten termination detection (rejected — more general than the reference; parity is the goal).

## D3 — QUIC-WS by bridge (clarify Q2)

- **Decision**: `glp_quick_host` (C#) gains a Gleam-facing plain-TCP acceptor that relays frames to/from the QUIC-WS mesh; glp_gleam gains `bridge_client.gleam`, a dial helper that connects to the bridge over the existing Gleam TCP transport. Wire format on the Gleam↔bridge hop is the existing FrameCodec (already byte-parity across runtimes).
- **Rationale**: delivers mesh access inside this feature's budget; the native BEAM QUIC (quicer NIF) route is the 036 Profile-C deferral with known toolchain risk — recorded again as a gated deferral.
- **Alternatives considered**: quicer NIF native leaf (deferred, gated); WS-only native leaf (rejected — partial and still a new native dependency).

## D4 — FE/BE split: reuse the proven C# split shape (clarify Q3 BUILD ruling)

- **Decision**: BE = a Gleam OS process running engine+scheduler behind the existing split protocol served over the Gleam TCP transport; FE = a thin Gleam REPL loop speaking that protocol. The protocol frames are the same ones the C# split uses (and US3 extends), so the FE can later talk to either runtime's BE.
- **Rationale**: the shape is proven in C#; sharing the protocol keeps a single seam definition and gives cross-runtime FE/BE for free.
- **Alternatives considered**: BEAM-distribution-based split (rejected — invents a second seam; not portable across runtimes).

## D5 — Embeddability surface (G3-A)

- **Decision**: `glp_embed.gleam` exposes load(project)/run(goal)/observe(results) to a host BEAM application, wrapping the same engine API the REPL uses; verified by a minimal host program in the test tree.
- **Rationale**: G3-A asks for the engine as a consumable component; the REPL already proves the API shape.
- **Alternatives considered**: C-node/NIF embedding for non-BEAM hosts (out of scope; the yngenios target is BEAM-side per G3-A).

## D6 — Gates and ordering (clarify Q4)

- **Decision**: MVP gate after US1+US2 (Anchor review); incremental reviews after US3, US4, US5; zero-regression suite sweep at every checkpoint; ship only after the full set + codexreview.
- **Rationale**: Q4 ruling "US1 and more"; matches the wave-2 gate practice (mvp-gate-review.md precedent).

## Norms carried in (not new decisions)

- `{exit_on_close, false}` + D-9 run-termination barrier + dial-retry on every new BEAM socket path (fleet norm from the 060 truncation-race root cause).
- §1.14: zero new GLP language surface planned; if dist_unify turns out to need a kernel-level hook, STOP and propose (FR-011).
- The 050 T050–T058, 059 T084–T098 task rows are the authoritative open-tail enumeration; this feature discharges them with evidence.
