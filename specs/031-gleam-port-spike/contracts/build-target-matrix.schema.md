# Contract: Build-Target Matrix Schema

**Artifact**: a table embedded in `dossier.md` (entity **E2**). FR-002: the dossier MUST include it.

## Shape

Exactly **three rows** (targets) × these columns:

| Column | Required content | Enum / form |
|---|---|---|
| `target` | The runtime target. | `Erlang/BEAM` \| `AtomVM` \| `JavaScript` |
| `verdict` | Per-target feasibility. | `viable` \| `partially viable` \| `not viable` |
| `evidence` | What backs the verdict. | command + observed output, **or** authoritative citation, **or** (AtomVM only) a named BEAM/OTP-subset limitation or a recorded bring-up blocker |
| `constraints` | Known limits / costs. | free text (BEAM-subset limits for AtomVM; cost-vs-BEAM for JS) |
| `host_vs_hardware` | Which was actually tested. | `host build` \| `target hardware` \| `N/A` |

## Per-row requirements

- **Erlang/BEAM** — verdict backed by the smoke's observed compile+run output. This is the **test runtime**; expected `viable`. *(FR-004, US2)*
- **AtomVM** — verdict backed by one of: an observed smoke result on an AtomVM host build; the **named** BEAM/OTP-subset limitation that blocked it; or the recorded **bring-up blocker** (if no host build could be stood up within the effort budget). `host_vs_hardware = host build` (no embedded hardware in scope). *(FR-005, US3; Assumptions)*
- **JavaScript** — verdict + evidence (command+output or citation); the row MUST state whether JS is a viable fallback for GLP and its cost relative to the BEAM path. *(US4 acceptance #2)*

## Invariants

- **No "unknown" cell** without a recorded reason. *(SC-003)*
- Every row has a verdict **and** ≥1 evidence item. *(SC-003, FR-002)*
- `host_vs_hardware` distinguishes "viable on host build" from "viable on target hardware". *(Edge case)*
