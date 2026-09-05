<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract — Term ordering and the merge gate

**Satisfies**: FR-013, FR-014, FR-015, FR-016, FR-017, FR-018, FR-026, FR-027, FR-029
**Verified by**: SC-005, SC-012, SC-013
**Authority**: rulings `Q-GLPNETG27-03` (STOP ORDER), `Q-GLPNETG28-01`, `Q-GLPNETG28-04`

This is the **precondition of the first merge**, not a follow-up to it. Term ordering is monotone:
once two boards fold, no later operation can lower a winning term. Getting this wrong is the only
irreversible part of the feature.

---

## C1 — Term shape

```csharp
public readonly record struct Term(string SpaceId, long EraCounter, NodeId HostId);
```

A term is **always** the full triple. There is no constructor that takes a bare number, because a
bare number is exactly what the fossil is.

## C2 — Comparison is three-valued

```csharp
public enum TermOrder { Less, Equal, Greater, Incomparable }

TermOrder Compare(Term a, Term b);
```

| Condition | Result |
|---|---|
| `a.SpaceId != b.SpaceId` | **`Incomparable`** — always, unconditionally, regardless of counters |
| same space, `a.EraCounter < b.EraCounter` | `Less` |
| same space, counters equal | ordinal compare on `HostId.Text` — a deterministic tiebreak only |

**`Incomparable` MUST NOT be collapsed to a boolean.** Any API returning `bool` for "does a beat b"
is a defect: `false` reads as "b wins", and a foreign-space term then wins by accident. The only
permitted boolean is `Wins(a, b) => Compare(a,b) == Greater`, whose `false` is safe because
`Incomparable` also yields `false` — never a win.

**Negative control (SC-005)**: a synthetic op in space `"foreign"` carrying `long.MaxValue` MUST NOT
win against a live-space op carrying `1`. A test that only checks the positive direction would pass
with the comparison deleted entirely.

## C3 — The counter never moves with the clock

```
EraCounter advances  <=>  a leadership event occurred
```

There MUST be no code path in which elapsed time, a timestamp, a tick, or a scheduler interval
advances `EraCounter` (FR-015). A host offline for a week returns with the counter it left with.

**Test**: advance a fake clock by 7 days with no leadership event; assert the counter is unchanged.

## C4 — Space kinds

```csharp
public enum SpaceKind { Live, Legacy, Unknown }
```

| Input | Kind | Behaviour |
|---|---|---|
| space id equals the configured live epoch | `Live` | ordered normally |
| space id absent from the operation | `Legacy` | retained; **incomparable** to live (FR-027) |
| space id present but not the live epoch and not legacy | `Unknown` | retained; **reported unordered** (FR-016) |

`Legacy` and `Unknown` are **different** observable states, and both are different from "dropped".
Coercing any of them into the live space is forbidden.

## C5 — Epoch minting

```csharp
TermSpace MintEpoch(string operatorRationale);   // records who, when and why
```

- `SpaceId` MUST NOT be derived from a host identity (that yields per-host spaces — foreclosed by
  ruling `Q-GLPNETG28-01`) and MUST NOT be derived from wall-clock time (that reproduces the fossil).
- Minting is **additive**: prior-epoch operations stay readable and attributed.
- **SC-013 test**: mint a new epoch; read every prior-epoch op back; assert content and attribution
  are unchanged.

## C6 — Retirement is the only correction

```csharp
FederationOp Retire(Dot targetOpId, string reason);   // Kind = "retire", IntoSpace = Legacy
```

- The API surface exposes **no delete**, **no tombstone**, **no rewrite**. Absence of the capability,
  not a guard against calling it.
- **SC-012 test**, both halves in one test so neither can be dropped:
  1. after retirement the target op is **still present** in the log;
  2. the target op is **excluded from the ordering decision** and reported as unordered.
- Idempotent: retiring an already-retired op is a no-op, not an error.

**🔴 Operational note**: op `628016928ab854ae` is retired by this mechanism and **MUST NOT be deleted
by any other means**. On an append-only board suppression is undetectable, which is why FR-017 makes
appending the only route.

## C7 — The merge gate

```csharp
MergeVerdict CanMerge(PeerCapabilities theirs);   // Allow | Refuse(reason)
```

FR-018: **refuse** the merge when **either** side is not term-space aware. Refusing is the correct
outcome, not a degraded one — merging under the older ordering rule is the irreversible mistake.

| Condition | Verdict |
|---|---|
| both sides advertise term-space awareness | `Allow` |
| peer does not advertise it | `Refuse("peer is not term-space aware")` |
| local side cannot confirm its own awareness | `Refuse("local term-space support unconfirmed")` |

The refusal reason MUST be specific. "Merge failed" is not a reason and is indistinguishable from a
transport error.

**Negative control**: a peer advertising no term-space capability MUST be refused. A test in which
the gate is deleted must FAIL — if it still passes, the gate was never load-bearing.
