# Data Model — 101-gleam-capability-delivery

## Contract
The runtime-free seam both rings satisfy.
- `operations` — the GLP capability surface offered to a host.
- **Invariant:** zero third-party runtime dependencies. Enforced at build time (SC-004), not review.

## Realization
One per runtime, held to exactly one Contract.
- `runtime` — `beam` | `atomvm`
- `ring` — `L1b` (workstation) | `L1a` (app; binding at `L2`)
- `unsupported_constructs` — enumerated, named, refused at build time (FR-004)
- **Invariant:** realizations are never peers and never share an artifact across sibling rings.

## ParityResult
- `attempted`, `agreed`, `diverged`, `excused` — and **`attempted = agreed + diverged + excused`** must hold (SC-007)
- `denominator` — mandatory; a result without one is unparseable (SC-002)
- `excused[].reason` — mandatory; a reasonless exclusion is indistinguishable from a case nobody ran (FR-007)
- `ring` — mandatory; results are per-ring, never aggregated over an unbuilt ring (FR-008)

## RingPlacement
- `subtree` → `ring`, plus `evidence` = measured contract consumption
- **Invariant:** never admitted on a name (FR-002). Positive control: `glp_gleam` is *not* the
  polyglot-L0 `kv`/`mailbox`/`network` service set and must be refused with the name quoted (SC-005).
