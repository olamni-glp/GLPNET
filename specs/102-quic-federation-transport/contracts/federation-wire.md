<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract — Admission, framing, and convergence

**Satisfies**: FR-001, FR-003, FR-005, FR-006, FR-007, FR-008, FR-009, FR-010, FR-011, FR-012, FR-028
**Verified by**: SC-001, SC-002, SC-003, SC-004, SC-006, SC-011
**Authority**: ruling `Q-GLPNETG28-03` (push + 60 s pull backstop)

---

## W1 — The transport is reused, not rebuilt

`GlpRuntime.CrdtMsg.Route.QuicLinkTransport` is consumed **unchanged**:

```csharp
new QuicLinkTransport(localPeer, cert, peerPins)     // mTLS both ways, SPKI-pinned
await t.ListenAsync(new IPEndPoint(bindAddr, 47890), ct);
await t.ConnectPeerAsync(peerName, new IPEndPoint(literalIPv4, port), ct);
await t.SendAsync(peerName, box: "board", bytes, ct);
await foreach (var inbound in t.Inbound.ReadAllAsync(ct)) { ... }
```

- **FR-001**: bind address comes from config and MUST be peer-reachable. Binding
  `127.0.0.1` is a misconfiguration and is reported (it is the failure that looks like success).
- **FR-003**: `ConnectPeerAsync` takes an `IPEndPoint` built from a **literal IPv4 address**.
  Name resolution is optional and, when attempted and failing, is reported as
  `NameResolutionFailed` — **never** as a transport failure. On this estate names resolve to
  `fe80::` link-local only.

## W2 — Admission

- **FR-006 / SC-004**: `peerPins` empty ⇒ **admit nobody**. This is the default and the safe failure
  state. Negative-control test: dial with an unpinned identity, assert the connection is refused and
  **zero bytes of board data** were transferred. A test that only asserts "connection refused"
  without asserting no data crossed does not test FR-006.
- **FR-005**: verification is **mutual** and completes **before** any board data flows. A one-sided
  check is not admission.
- **FR-007 / SC-006**: pins are keyed by `NodeId`. A peer entry carries a **list** of endpoints.
  Olamnit answering on `.136` and `.129` is **one** participant; the participant count asserts `1`.
- **FR-008**: a presented identity that does not match its pin raises `PinMismatch`, distinct from
  `Unreachable` and from a generic transport error — the two demand opposite operator responses
  (investigate an attack vs. wait for a host).

## W3 — Framing

One board operation per frame, on box `"board"`, canonical UTF-8 JSON:

```json
{
  "op_id":  {"peer": "<nodeId-hex>", "counter": 42},
  "origin": "<nodeId-hex>",
  "kind":   "board_post",
  "term":   {"space": "<epoch-id>", "era_counter": 1, "host": "<nodeId-hex>"},
  "deps":   [{"peer": "...", "counter": 41}],
  "pred_hash": "<hex>",
  "body":   { }
}
```

- `term` is **absent** on operations that are not leadership-bearing. Absent ⇒ the op is never a
  candidate in an ordering decision. An absent term is not term zero.
- `origin` MUST survive the crossing (FR-009). An op arriving with wrong or missing attribution is a
  fault, not a value.

## W4 — Durability ordering

**Append locally, then ship.** A federation that ships an op it has not stored loses data whenever
the link succeeds and the disk does not. Order is fixed and testable:

```
1. append to the local per-actor JSONL log      (durable)
2. push to every admitted peer                  (best effort)
```

Never the reverse. A test kills the process between 1 and 2 and asserts the op is still present
locally on restart and is delivered by the pull backstop.

## W5 — Convergence: push, plus a pull backstop

FR-028, ruling `Q-GLPNETG28-03`. **Both** legs are required; neither alone satisfies the FR.

| Leg | Trigger | Assertion |
|---|---|---|
| Push | an op is appended | present in an admitted peer's fold within **5 s** (SC-001) |
| Pull | every **60 s** | after a deliberate link interruption and restore, present within **120 s** (SC-011) |

The pull exchanges **version vectors first**, then only the ops the peer lacks — `VersionVector.Join`
and `Contains` already exist and are reused. Shipping the whole log every 60 s is not a backstop, it
is a broadcast storm.

**SC-011 test**: append while the link is down, restore the link, assert presence within 120 s. This
test is what makes the backstop load-bearing — delete the pull and it must fail.

## W6 — The fold

FR-010, FR-011, FR-012, reusing `Dot` and `VersionVector` unchanged.

```csharp
if (!seen.Contains(op.OpId)) { Append(op); seen = seen.With(op.OpId); }
```

- **FR-010 / SC-002**: union-by-id. The suite ships the **same op twice** and asserts the fold
  contains it **once**. Redelivery is certain on any link that can drop and retry, so a fold that has
  not been tested against deliberate redelivery is untested, not convergent.
- **FR-011**: the fold only ever appends. There is no removal path in the API at all.
- **FR-012 / SC-003**: two hosts holding the same op set produce **byte-identical** folds regardless
  of arrival order. Test: fold set `S` in order `p` and in reversed order `p'`; assert the two
  serialised folds are byte-equal. Comparing "equivalent" folds by a custom comparer would hide
  exactly the ordering bug being tested.

## W7 — Degradation

Edge case, FR-004. A peer unreachable at connect time ⇒ federation reports
`Degraded(local-only)` **explicitly**, continues serving local lanes unchanged, and never reports
success. Local oracle operation is never on the federation critical path.

## W8 — What this contract does NOT do

Out of scope by the spec, restated so no reviewer expects it: no leader election, no PBFT, no
fleetwide coordinator, no fleetwide signature verifier. They consume this transport and are blocked
by its absence. Nothing here elects anything.
