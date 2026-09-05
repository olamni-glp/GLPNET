<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# Ruling `Q-GLPNETS17-03` — the six unbound pipeline ids are cosmetic-and-unfixable-here

**Decided:** 2026-09-04 · **Engineer ruling:** *record-cosmetic-suppress-noise*
**Question set:** `.specify/decisions/Q-GLPNETS17-20260904T0730Z.json` (BK-QUESTION v2, validated)

## What was measured

`buildkit-roadmap reconcile`, 2026-09-04, on GAVRIELLA/glpnet:

```
pipeline binding: 16/22 pipeline feature ids bound to a roadmap feature; 6 unbound.
  031-gleam-port-spike
  036-glp-gleam-baseline-program
  039-m2-0-verify-erlang-monitor-atomvm
  049-wave1-guard-link-acceptance
  050-full-gleam-combined
  060-wave3-full-gleam-chain
74/124 roadmap features carry no spec_path and can never bind by basename.
```

## The contradiction

`reconcile` prints its own remedy — `buildkit-roadmap link --feature <slug> --spec-path specs/<dir>` —
but **`link` refuses on closed features**: it only moves `promoted → specified`, and all six are
closed. **The tool recommends a fix it then rejects.** No sequence of the commands it names can
resolve what it reports.

## The ruling

These six are **closed-and-unbindable by design**. A reconcile whose *only* complaint is exactly
this set of six ids counts as **in-sync**.

## Why not the alternatives

- **Force the link.** Wave-24 measured that one force target (`wave-3-consolidated-full-gleam-chain`)
  already points at `specs/059-…` and **resolves correctly today**. A blanket `--force` could
  overwrite a binding that is currently right, trading a cosmetic defect for a real one.
- **Backfill all 74 missing `spec_path`s.** Large manual data-entry across mostly-closed historical
  features, and it **does not fix these six** — their blocker is the closed-state refusal in `link`,
  not a missing path.
- **Do nothing / leave it noisy.** The real cost is not the six ids. It is that a permanently-dirty
  reconcile trains every lane to ignore reconcile output, so the next *genuine* desync arrives
  unnoticed. That is the outcome this ruling exists to prevent.

## Carried upstream

Raised to the **buildkit lane** as a defect in `roadmap link`: it should either accept closed
features for a pure binding operation (binding is not a state move), or stop naming itself as the
remedy for a condition it cannot act on. Until that lands, this ruling is the standing
interpretation for glpnet.

## Scope of the suppression

**Exactly these six ids.** The suppression is not a blanket "ignore reconcile" — if reconcile
reports any id outside this list, or any other class of problem, that is a real finding and this
ruling does not cover it.
