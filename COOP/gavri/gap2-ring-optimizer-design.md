# GAP 2 — RING OPTIMIZER: DESIGN (not a build)

**gavri, 2026-07-19.** Design only. Nothing here is implemented. Written under the M-26/M-27
reframing, with M-24/gap-1 taken **verbatim** from olamnit's close (not re-derived).

**Written while seq 24 was in flight**, at operator instruction. §G2-7 names what would refute it.

Facts marked **[V]** are verified by me from code at pin `02bcc20` this session. Everything else is
design judgement and labelled as such.

---

## G2-0. The frame constraint, verified

**[V]** `MeshRelayRoute.cs:42` decodes exactly five fields:

```csharp
MeshFrameCodec.TryDecode(frame, out var dest, out var src, out var hop, out var flags, out var inner, out _)
```

**There is no path field.** Forwarding is `_router.TryNextHop(dest, out var next)` — a pure
distance-vector lookup on `dest` alone (`MeshRelayRoute.cs:48`).

M-26 is therefore confirmed *from the code*, not merely accepted: **an elected tour cannot be
expressed in the frame, and forwarding consults only the DV table.** Any optimizer that "elects a
ring" and expects frames to follow it is electing something the transport structurally cannot obey.

**The election must steer the route table. It must never pin a path into the frame** — that would
mean widening a frozen wire surface *and* adding a source-routing primitive, which hands an attacker
the ability to choose the path a frame takes. Both are refused, permanently, not just for v1.

---

## G2-1. Output contract — STEER, DON'T ROUTE

The election's only output is a **signed per-epoch set of link cost biases**, applied locally by each
node to its own DV cost function:

```
EpochDecision {
  genesis_hash, epoch, run_id,
  biases: [ { link_id, bias_bucket } ],     // ordinal, small closed set
  quorum_sigs: [...]                        // threshold over the epoch's FIXED N
}
```

Each node applies **only the slice naming links it is an endpoint of**. It never learns or acts on a
global tour.

**Why this shape is the safe one (design judgement, but the reasoning is structural):** DV forwarding
already picks next-hop by lowest cost. If the election can only *bias costs*, a fully malicious
election degrades routing (a liveness harm, bounded and observable) but can never *direct* a specific
frame down an attacker-chosen path (an integrity harm). Source routing inverts that. The weaker
primitive is the correct one precisely because it cannot express the attack.

**Degradation is explicit:** if the elected bias is stale, inconsistent, or absent, DV converges to
some path on unbiased costs. **The optimizer's failure mode is "plain DV", never "no route".** Nothing
downstream may treat the absence of an epoch decision as an error.

---

## G2-2. The must-state consequence (M-26) — AND IT INVALIDATES THE CURRENT BENCH

**[V]** `MeshNodeRuntime.cs:260` — `if (dest == _self)` ⇒ deliver locally (deduped).

On a topology where every node is every other node's **direct neighbour** — which is what a 4-node
LAN mesh is by default — `TryNextHop(dest)` returns `dest` itself. One link-send, the frame arrives,
`dest == _self`, deliver. **The intermediate-relay forward branch in `MeshRelayRoute` never
executes.**

**Consequence, stated plainly as M-26 requires:**

> A soak on a fully-adjacent topology proves **link transport + dedup**. It does **not** prove mesh
> routing, and it cannot exercise the optimizer at all — because the optimizer's only output is a
> bias on a next-hop choice that is never contested when every destination is one hop away.

**This is a bench-construction requirement, and it is load-bearing:** to validate gap 2 at all, the
bench must contain at least one `(src, dst)` pair with **no direct link**, so that forwarding is
genuinely chosen rather than trivial. Concretely: suppress adjacency (a roster/allowlist constraint at
the link layer, not a physical one) to force a line or ring rather than a clique.

**I am flagging this as affecting the 4-node kill-one plan as currently framed.** If nodes are
fully adjacent, kill-one demonstrates link failover, not reroute. That is still worth measuring — but
it must not be *reported* as a mesh-routing proof. This is the same class of error as my seq-18
attribution mistake: a real structural finding attached to a test that does not exercise it.

---

## G2-3. Input attestation (M-27) — I BOUND SELF-REPORTING, I DO NOT SOLVE IT

M-27 requires either bounding the contribution of self-reported `LinkCostInputs` or stating plainly
that I have not. **I bound it. I do not solve it.** Explicitly:

**(a) Two-sided attestation.** A link cost is admissible only when signed by **both endpoints** over
the identical payload `(genesis_hash, epoch, link_id, cost_bucket)`. A node cannot report a link it is
not on.
> Reuse the shipped shape, do not reinvent: this is exactly `signed_receipt_v1` /
> `RelayReceipt` (`MeasuringPeerPub` + `Sig` + anti-self-report), which **[V]** already ships in
> `ProofVerifier.VerifyRelay`. Same Ed25519 primitive, same anti-self-report check.

**(b) Bucketed, not continuous.** Costs report into a small ordinal set (≈4 buckets), never a real
number. This caps the *resolution* of a lie: a liar can shift a link a bucket or two; it cannot claim
`cost = 0.0001` and vacuum all traffic.

**(c) Bounded influence per identity.** Each identity's total bias contribution per epoch is clamped
to a constant. N colluding identities get N×clamp — linear in identities, never unbounded.

**(d) What this does NOT buy — stated plainly.** Two endpoints under **one operator** attest each
other's lie perfectly; (a) is satisfied by construction. (a)+(b)+(c) bound a *single* liar and cap the
*magnitude* of collusion. They do **not** make the optimizer Byzantine-safe.

**And PBFT does not change this.** PBFT achieves agreement on *which self-reported inputs everyone
saw*. It says nothing about whether those inputs are true. Wrapping a consensus protocol around
garbage inputs yields agreed-upon garbage. **Byzantine-safe link cost requires an external
measurement authority that does not exist and is not in scope.** Anyone who needs that guarantee
should read this section as "not provided."

---

## G2-4. Membership and quorum — M-24 TAKEN VERBATIM (frozen, not re-derived)

Per the frozen close, reproduced without modification:

- Genesis manifest enumerates founder pubkeys = **epoch 0**; **signed by ALL N founders**.
- **Every node rejects a genesis it did not itself sign.**
- `genesis_hash` := hash of the canonical body **EXCLUDING** run id; `run_id := genesis_hash`.
- Every transition/exclusion **binds `genesis_hash`**.
- Two manifests with the same `run_id` = **INTEGRITY FAILURE → HALT**. Never merge, never resolve by
  recency.
- The operator **run NONCE** is covered by signatures ⇒ replay detectable by construction.
- Threshold = the epoch's **FIXED N** — never "who is live now".
- Exclusion is **additive, epoch-scoped, never mutates the anchor**, and lives in the election's
  durable decision log. **ELECTED membership — not anchor membership — is authoritative for quorum.**

The base-case inversion is why this is verbatim: rooting membership in epoch genesis by induction
with no base case lets an attacker declare `genesis = {itself}` and hold `quorum = 1` forever. The
all-founders signature IS the base case. Do not "simplify" it.

---

## G2-5. THE COUPLING FIREWALL — new, and the reason this document matters

**The optimizer's link-attestation graph and the coin's `TrustGraph` must be separate stores with
separate admission rules, and neither may be derived from the other.**

Why this is not fussiness — from code **[V]**:

- `IndependenceChecker` (`Olamnit.Coin/Trust/IndependenceChecker.cs`) reads graph adjacency as
  **social independence**, and mint authorization depends on it.
- The optimizer reads link data as **physical connectivity**.

If one graph serves both, then:
- **an attacker who adds radio links manufactures minting independence**, and
- **an attacker who fabricates trust manufactures routes.**

The compromise of either surface becomes the compromise of both — and the failure is silent, because
each subsystem's own checks still pass.

**I am stating this now specifically because the cheap implementation is obviously to reuse one
graph.** Both are `(node, node, weight)`. It will look like sensible deduplication. It is the single
worst change available in this design.

This is also the point at which M-27 and M-29 turn out to be **the same defect in two locations**:
*a node asserts a fact about its own relationships, and the system treats the assertion as evidence.*
M-27 is that defect in link cost; M-29's real blocker (**[V]** nothing in-repo populates `TrustGraph`
— `CoinServiceBinder.cs:146` constructs it empty) is that same defect waiting to happen in trust.
See `rulings-m29-m34.md` §C4 — the C4 condition and this section are the same requirement.

---

## G2-6. Cost — genuinely NEW, and the largest item on either list

Per M-27's instruction to cost it as genuinely-new. Relative sizing, not hours:

| Component | Size | Note |
|---|---|---|
| Two-sided attestation payload + verify | **S** | Reuses `Ed25519Signer` + the `signed_receipt_v1` shape **[V]** |
| Bucketing + per-identity clamp | **S** | Pure function, trivially testable |
| Bias application into the DV cost function | **M — the risky one** | Touches the **shipped** route table. Must preserve "absent decision ⇒ plain DV" |
| Epoch decision log + genesis binding | **M** | Mostly M-24, which is frozen; mechanical |
| **Bench topology with non-adjacent pairs** | **M** | New harness. **Without it nothing above is testable** (§G2-2) |
| PBFT | **DO NOT BUILD** | One elected value per epoch. Reuse 057's `Kv/Election/` as a **realization, never a peer** |

**Build order is forced by §G2-2:** the bench comes **first**. Building the optimizer against a
fully-adjacent bench produces a component that cannot be shown to work, which is worse than not
building it — it produces confident green on a path that never executed.

---

## G2-7. What would refute this design

- **Any seq-24 Builder finding a path field, or any source-route capability, in the frame.** I read
  the decode call at `MeshRelayRoute.cs:42` and found five fields; scope of that absence claim is that
  file at that pin, vocabulary `TryDecode`/`path`. A second encoder elsewhere would change G2-1
  entirely.
- **Evidence that some (src,dst) pair on the intended bench is already non-adjacent.** That would
  soften §G2-2 from "invalidates the bench" to "verify the bench". I have not seen the intended
  roster — this is a genuine gap, and olamnit owns the bench.
- **A finding that the DV cost function is not the sole input to `TryNextHop`.** I read the call site,
  not `IDistanceVectorRouter`'s implementation. If cost is not the whole story, G2-1's steering
  mechanism needs rework.
- **M-26/M-27 themselves being revised by the 5 blind Builders.** This design is built *on* those
  reframings; if they move, it moves. That is the risk the operator accepted in instructing me to
  proceed before seq 24.

---

## Summary

1. **Steer the route table with signed per-epoch cost biases. Never pin a path into the frame** —
   verified inexpressible, and refused permanently as a source-routing surface.
2. **A fully-adjacent bench cannot test any of this** — the relay forward branch never runs. Bench
   first, optimizer second.
3. **Self-reporting is bounded (two-sided attestation, buckets, per-identity clamp), NOT solved.**
   PBFT does not help. Byzantine-safe link cost needs an external measurement authority; there isn't
   one.
4. **M-24 genesis close taken verbatim.**
5. **Never merge the trust graph and the link graph** — the same defect that blocks M-29, one
   subsystem over.

— gavri, 2026-07-19
