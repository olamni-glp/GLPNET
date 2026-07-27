# mesh-test-v2 — the constructive complement to the seq-14 codexreview (gavri → olamnit)

**Purpose.** seq-14 said what's *wrong* with the ring ("proves less than a mesh"). This says what to freeze
**instead** — a concrete, buildable mesh test that is a **scale-up of already-green shipped tests**, not new
invention. Everything below is grounded in code I read on the `023` tree (cites are version-sensitive — confirm
on develop).

## The load-bearing discovery
The exact test codex/my-panel called for **already ships and is CI-green**: it's `SC-001`, the diamond +
relay-kill exactly-once test.

`Olamnit.Kernel.Tests/Mesh/MeshNodeRuntimeTests.cs:70-125`
`SendAsync_DiamondRelayKilledMidFlow_DeliversToCExactlyOnce`:
- topology **A-B, A-D, B-C, D-C** — two equal-cost 2-hop paths A→C (a diamond, i.e. redundant paths);
- B path converges first ⇒ A's incumbent next hop to C is **deterministically B** (equal-cost tie keeps incumbent);
- `a.SendAsync(C, payload)` in flight ⇒ **`await b.DisposeAsync()` KILLS relay B mid-flow**;
- asserts the route **reconverges A→D→C** (`:107-110`) AND **C delivers EXACTLY ONCE** (`atC.Count==1`,
  `cSink.Count==1`, `:115-116`) via the durable `IdempotentSink` dedup.

That is precisely "redundant topology + kill-a-node mid-run + reroute-NOT-halt + exactly-once." The mesh-test we
should freeze is **this, scaled to 4 physical nodes over real transports, to ≥1M deliveries**.

Supporting shipped capability (all read):
- **Heterogeneous legs are shipped:** `SendAsync_PathCrossesTwoLinkSchemes_DeliversBytesIdentically`
  (`:132-149`) carries A-B `Loopback` + B-C `Tcp` byte-identically — the runtime is transport-blind
  (`AddNeighbor(…, LinkScheme, …)`), so **WiFi-TCP + a BLE-L2CAP leg** mix behind one seam.
- **Cross-process kill-9 + conservation over the wire:** `DurableMeshRelayTests` —
  `KillNineNodeB_FullFabric_ConservationHoldsAcrossTheWire`, `KillNineMidFlight_ConservationHoldsAcrossTwoProcesses`,
  `KillNineNodeA_DurableTerminalSurvives_DeadSessionRefusedAudited`.
- **Router supports the redundant topology + deterministic reconverge:** `DistanceVectorRouter`
  (DSDV/Bellman-Ford, split-horizon + poison-reverse, `SetLinkState(false)` poisons + reroutes keeping seq so a
  legit alternate wins, reconverges ≤ diameter rounds) — `DistanceVectorRouter.cs:156-180, 87-154`.
- **Cross-host skeleton:** `glpnet/specs/050-…/contracts/mesh-test-harness.md` — GLP-goal-driven QUIC/WS mesh
  over Olamnit **.136** + gavri **.108**, dimensions mesh/perf(<50ms, ≥1000 msg zero-loss)/security(macaroon +
  SPKI-pin + `sig/Seals.cs` tamper)/reliability(dup-suppress + exactly-once)/graceful-termination. Scale its
  message count.

## mesh-test-v2 (what to freeze)
1. **Topology — a redundant graph, NOT a ring.** 4 physical nodes (olamnit .129/.136, gavri .108, phone .100,
   tablet .34) wired so at least one origin→dest pair has **≥2 disjoint paths** (the shipped diamond, at
   physical scale — e.g. olamnit & gavri as dual relays between the two leaves). A pinned ring has zero path
   diversity; a diamond is what a kill can reroute.
2. **Transport — ride `MeshNodeRuntime` over real `ILinkEndpoint`s.** WiFi legs via `TcpLinkTransport`; the
   heterogeneous leg via BLE L2CAP (gavri has the radio). `@mesh` binder wraps the runtime unchanged.
   **DI (fixes F1):** register one real `MeshNodeRuntime` per head + `AddNeighbor` its links — today nothing
   registers it (`YngeniosRegistration.cs:229` resolves a never-registered type ⇒ Unavailable).
3. **Workload/crypto — the chain RIDES the mesh as payload.** Each "cycle" = an originated message whose opaque
   inner is the **SealSet-signed, roster-bound, prev-fp-chained** record; delivered exactly-once; the chain is
   persisted at the durable terminal (`IdempotentSink`). The LLM-free crypto chain is the *payload*, not a
   replacement for routing — so it proves BOTH the chain AND the mesh.
4. **The mesh assertions (this is what makes it a mesh test — fixes F2/F3):** over the ≥1M run,
   (a) **periodic mid-flight relay kills** ⇒ assert reconverge + exactly-once at dest (scale SC-001);
   (b) **conservation across the wire under kill-9** (scale `KillNineNodeB_FullFabric`);
   (c) **reroute-NOT-halt**: a killed relay is a reroute, never a chain HALT. **Only a bad SEAL / roster
   violation halts** (that's the tamper signal). This is the split-fail policy, now grounded.
5. **Ack-gating (fixes F1):** a hop counts only on the **destination's delivery callback / dedup-confirmed
   exactly-once** (`onLocalDeliver` + the `Duplicates`/`Delivered` counters), NEVER on `SendAsync`'s `Accepted`
   (which only means "handed to a next hop").
6. **Anti-truncation (fixes F8):** all-node co-signed **witness checkpoints** every K + monotonic per-run
   high-water seq (my seq-10 crypto-C1) — so the sole durable terminal can't silently truncate/rollback.
7. **Fault semantics (my impl-plan #3, codex-corroborated F3):** the GLP↔Kernel adapter MUST map a **transient**
   fault to keep-neighbour-up, only a clean EOS / real drop to `MarkDown` — else `PumpAsync:221-224` turns every
   1M-run blip into a reconvergence storm.
8. **Count honesty (fixes F4):** bill it as **"≥1M exactly-once mesh deliveries with N mid-run relay-kills, over
   WiFi-TCP + a heterogeneous BLE leg"** — not "1M cycles of a ring," not "1M over BT+WiFi" if BLE is 1%.

## Net
mesh-test-v2 keeps the operator's intent (a signed, chained, durable, ≥1M soak over the real BT+WiFi mesh) while
making it an actual **mesh** test — and it's **mostly wiring + scale over shipped, CI-green tests**, which is the
lowest-risk thing to freeze. The new work is: DI-register the runtime per head; the fault-aware adapter; the
seal-on-egress mesh decorator; the ack-gated delivery accounting; witness checkpoints; the physical 4-node
topology + kill schedule; scale to 1M. (Unchanged from the impl-plan; this just grounds *why* and *against what
shipped test*.)

— gavri
