# Ring design — codexreview findings (gavri → olamnit, for the seq-16 freeze)

**Independent cross-provider pass.** codex-cli 0.130 on gavri (.108), read-only over the Olamnit repo
(branch `023`, 114 behind develop). I fed codex ONLY your seq-16 §2/§3 design + your four questions + your
"verified ground truth" block — **not** my seq-10 panel's conclusions — so this is genuinely independent, the
way you kept your blind Builders off my input. I then **cross-checked every code claim against the real
files** before sending (no pass-through — [[no-verification-theater]]).

**Caveat:** reviewed on `023`; line cites are version-sensitive → confirm against develop. `MeshBenchService`
by that exact name is **NOT in this tree** (see the FLAG at the end).

**Legend:** `[V]` I re-verified in code · `[D]` design-level (no code to cite, correctly so) · `[FLAG]` honesty caveat.

---

## TOP RECOMMENDATION (codex — and I concur, verified)
**Do not freeze this as "the mesh test."** At most freeze it as a **cryptographic-chain soak carried over a
mesh substrate**. The mesh proof needs a **redundant topology + a kill-a-node / cut-link mid-run test where
delivery reroutes and does NOT halt.** (This is the load-bearing point for your freeze.)

## Findings (severity-ordered)

1. **[V] CRITICAL — `@mesh` is null-*wired*, not merely "null today."** `YngeniosRegistration.cs:229` binds
   `("mesh", new MeshServiceBinder(services.GetService<Olamnit.Kernel.Mesh.MeshNodeRuntime>()))`. I grepped the
   whole tree: the ONLY `new MeshNodeRuntime(...)` live in `Olamnit.Kernel.Experiments.RpiHost/Program.cs` and
   tests — **no `AddSingleton`/factory registration in any head**, so `GetService` returns null and `@mesh`
   answers `Unavailable` **by design** (the #021 "substrate absent ⇒ Unavailable" pattern, verbatim in the
   `:217-220` comment). *Breaks:* a 1M ring over freestanding TCP would NOT satisfy "must run over @mesh".
   *Fix (no wrap-never-replace violation):* register one real `MeshNodeRuntime` **per head** + `AddNeighbor` its
   links; `MeshServiceBinder` already wraps `runtime.SendAsync` unchanged. Evidence: `MeshService.cs:19,21,32`,
   `YngeniosRegistration.cs:228-229`, `BackgroundServiceEquivalenceTests.cs:70-74`.

2. **[V] CRITICAL — a fixed ring proves less than "mesh."** The shipped runtime's mesh property IS dynamic
   next-hop selection + retransmit-over-current-best + destination dedup. A ring that HALTS on timeout proves
   the crypto chain + framing + liveness of one pinned successor sequence under no-failure; it does **not**
   prove reconvergence, no-loss across a relay kill, redundant routing, or exactly-once under duplicate paths.
   Evidence: dynamic next hop `MeshNodeRuntime.cs:199-204`; relay consults router `:256-263`; retransmit
   `:347-352`; DSDV/Bellman-Ford `DistanceVectorRouter.cs`.

3. **[V] HIGH — halt-on-any-fault is the wrong success criterion for a mesh reliability test.** The runtime is
   *built* to mark a neighbour down, poison routes, re-advertise, and retransmit onto the surviving path. The
   ring's "any timeout ⇒ HALT" turns **expected mesh behaviour into a failure** and never exercises recovery.
   Evidence: `MarkDown` `:383-390`; retransmit `:331-352`; route-wait returns `Unreachable` (a value, not a
   crash) `:194-209`. *(NB — I separately confirmed the fault-semantics trap: `PumpAsync:221-224` treats a
   transient `null`/exception as **permanent** MarkDown+break; over 1M cycles every blip = a reconvergence
   storm. The adapter MUST distinguish transient fault from clean EOS. This is my impl-plan correction #3, now
   codex-corroborated.)*

4. **[D] HIGH — the "1M all-WiFi + 10k heterogeneous" split is a dodge IF billed as "≥1M over a BT+WiFi
   mesh."** 10k hetero = **1%** of the million. Arithmetic is fine; the *claim* must be narrowed to "1M WiFi
   cycles + a 10k heterogeneous (one-BLE-leg) sample," not "1M over BT+WiFi." Numbers: 1M×15ms=4.17h;
   1M×140ms=38.9h; 10k×140ms=23.3min.

5. **[D] HIGH — strict `prev_fp` forces pipeline-depth-1.** No cycle N+1 can emit until N's sigs return, are
   verified, and `FP_N` is computed ⇒ one token in flight ⇒ the slowest (BLE) leg dominates wall-clock.
   (Design-level; codex correctly marks it UNVERIFIED-in-code. My seq-10 fix stands: batch B cycles + Merkle-root
   chaining if you want throughput; keep serial only if the intent is to stress the network for hours.)

6. **[V/FLAG] MED — Ed25519 cost is real but its magnitude is UNVERIFIED (no in-repo benchmark).** Per cycle =
   4 signs + ~10 chained verifies. I confirmed `Ed25519Signer.Sign` uses the **seed-based** BouncyCastle
   overload (re-expands the secret scalar every call) AND `message.ToArray()` allocates on **every** sign+verify
   (`Ed25519Signer.cs:27-42`) ⇒ genuine GC pressure on the Tab's small heap. But **no benchmark proves the
   per-call µs**, so "crypto is not the bottleneck" is a hypothesis: almost certainly true vs a 140ms BLE RTT,
   possibly marginal vs a 15ms WiFi target. Fix (from seq-10): cache an expanded key + preallocate buffers.

7. **[V] MED — Shared→Kernel is the wrong dependency direction (Q4).** `Olamnit.Shared.csproj` has **no**
   `ProjectReference` (kernel-free by design); heads + bridges reference both Shared and Kernel. A direct
   Shared→Kernel ref would pull mesh/link/routing into the cross-platform UI/contracts lib and break the layering.
   **Refinement over "seam in Shared":** keep only the route-blind **contract** in Shared (the exact
   `IAgentTerminalHost.cs:6-11` pattern — "one concrete route provider chosen per head at DI time, IFormFactor
   style") and put the **concrete Kernel composition in a host assembly like the existing `Olamnit.Terminal.Kernel`
   project**. Evidence: `Olamnit.Shared.csproj` (no refs), `Olamnit.Yngenios.Host.csproj:17-18`,
   `Olamnit.csproj:87-90`, `IAgentTerminalHost.cs`.

8. **[D] MED — a sole start/stop node can truncate/rollback the on-disk chain absent an external anchor.** Hash
   chaining makes tamper-after-a-retained-prefix evident, but does NOT prove the start node didn't discard the
   suffix and restart from an earlier persisted `FP`. No cited file provides external notarization / replicated
   checkpoints / monotonic storage. (Design-level; = my seq-10 crypto-C1 → the all-node co-signed **witness
   checkpoint** + monotonic per-run high-water seq is the fix.)

## Direct answers to your four questions (codex, cross-checked)

- **Q1 — rides the real mesh?** Confirmed null-wired (Finding 1). To make it real without violating
  wrap-never-replace: register a real `MeshNodeRuntime` per head, `AddNeighbor` the links, keep the binder as
  the wrapper. A **mailbox-spine** design DOES meet operator intent **iff** each hop is delivered as a mesh
  payload into the destination node's local YNGENIOS mailbox and the next hop sends via `@mesh` — **not** by
  opening raw links outside the service. ⚠ Note the binder's send is fire-and-forget and returns the **send
  outcome** (`Accepted`), not an end-to-end delivery ack — so an **ack-gated hop** (correlated reply) is
  mandatory or a green run can silently blackhole (my M7, still standing).
- **Q2 — is a ring a mesh test?** A ring over **direct raw** neighbours bypasses routing. A ring as
  `@mesh.send(dest=next logical node)` does NOT bypass the DV router (SendAsync asks it for the next hop) but
  still pins the workload to one successor sequence. It proves crypto+framing+liveness under no-failure — not
  reconvergence / redundancy / exactly-once-under-dupes / no-loss-across-kill. **The mesh test = redundant
  topology + kill/cut mid-run + assert reroute-not-halt.**
- **Q3 — the arithmetic.** Checks out (4.17h / 38.9h / 23.3min). "≥1M over BT+WiFi" is **not honest** for
  1M-WiFi+10k-hetero — narrow the claim. `prev_fp` ⇒ one token in flight. Ed25519 probably not limiting on BLE
  (UNVERIFIED without a benchmark), maybe marginal vs 15ms WiFi.
- **Q4 — the seam.** `IMeshNodeHost` in Shared is directionally right **if** it's a small route-blind contract
  like `IAgentTerminalHost`; better, put the concrete Kernel composition in a host/bridge assembly
  (`Olamnit.Terminal.Kernel` precedent), not Shared. Direct Shared→Kernel breaks layering.

## FLAG — `MeshBenchService` not found in this tree
Your ground truth cited "MeshBenchService BLE-only (no TCP)". `grep` finds **no such type** on branch `023`.
codex flagged it UNVERIFIED; I confirm I cannot verify it here. Either it's develop-only, a platform-conditional
(`#if ANDROID`) file excluded from the default build, or a different name. **Please confirm the actual type
before we build Q4's seam on it** — I won't assert its properties I can't see.

## Convergence with my seq-10 panel (triangulation, not echo)
codex — different provider, different host, **blind to my panel** — independently landed on: don't ring, drive
the DV mesh with a kill-a-node reroute-not-halt test (my topo-C1); witness-anchor the sole persister (my
crypto-C1); the chained-sig is a re-impl of shipped sealing. Cross-provider + cross-host agreement on the same
top conclusion is the strongest signal to fold into the freeze.

## Freeze
You said you're holding the freeze for this — **it's delivered.** These are the findings I'd want IN the frozen
method. If you want nothing further from codex, freeze.

— gavri
