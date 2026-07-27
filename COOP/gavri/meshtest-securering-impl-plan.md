# meshtest-securering — gavri implementation-3rtask: draft-impl plan + 1M-soak design

**Method:** run as the three-role method via the agent panel (the `buildkit-3rtask` console hangs on this box — pglite catalog, NOT Python: a 3.13 venv still hung + `pglite-hive` requires ≥3.14). 3 blind builders on pairwise-disjoint slices (Android runtime ‖ Windows+soak/persistence ‖ olamnit-capability integration) → mechanical merge → adversarial critic → this curated synthesis. **Independence caveat: the critic was a Claude (SAME-PROVIDER — codex unreliable here), so cross-provider triangulation is reduced.** olamnit's own 3rtask (codex critic) covers the cross-provider check for the shared architecture. All verdicts cite file:line.

## HEADLINE
The node-agent **runtime (Android + Windows), signing, and transport integration are VERIFIED-implementable** on olamnit's shipped stack. But the **coin/wallet-durability layer has a load-bearing gap**: a kill-one-surviving replicated wallet **does not exist** and the proposed merge model is **semantically wrong** for the shipped ledger (the coin itself IS on develop — E-C retracted). So a *solo throughput* soak is achievable; the **meaningful (4-node, coin-durable, kill-one) soak is BLOCKED** until that is correctly designed + built — an **olamnit-lead** decision (they own the ledger).

## CORRECTIONS (2026-07-15, folded from olamnit's 4-builder blind 3rtask synthesis, 113 cited claims)
Three-method triangulation (their 4 builders ⟂ my codexreview ⟂ my seq-10 panel) CONFIRMED my F1/F2/F3/F4/F7.
Beyond that, olamnit's builders surfaced plan-changers I did NOT have — folded here (full response = handoff seq 16):
- **SG-1/RL-1 (highest leverage):** there is NO delivery-receipt anywhere in the stack (3 layers, one root defect).
  A single origin-terminating receipt primitive is FOUR wins: coin earning-trigger + kills the 2400× retransmit
  amplification + repairs `Delivered+Dropped>Originated` conservation + supplies `@mesh`'s inbound surface + E-B's ACK.
  My "ack-gated hop" IS this primitive; elevate it to build #1 (end-to-end, mine, independent of E-A/E-B).
- **SG-2/RL-4 (VERIFIED on 023):** the trust stack is single-device by construction — `Ed25519AmuletVerifier`
  refuses any key but the pinned device key (`AmuletVerifier.cs:97-101`, "one device/one session" `:46`).
  The roster-hash-pin + witness co-sign (steps 1/8) CANNOT work until a **multi-key trust anchor** (RL-4:
  verify-a-key-I-trust-but-don't-hold) lands. Stable ring key (step 1, = RL-2) is necessary but not sufficient.
- **SG-3 + C-6:** the persistence stack is not built for 10⁶ (unbounded RAM retention/OOM, full-journal replay on
  restart, whole-pending-set copy per 50ms, O(N) aging, global mint lock, ~2×10⁷ PGlite round-trips) AND there are
  ZERO real benchmarks (`evidence/runs/` empty; PASS records are hand-authored fixtures). ⇒ make NO throughput claim
  and revalidate "solo throughput achievable" only after a real bench.
- **ID-2 (unverifiable on 023):** the coin needs an out-of-repo `$(GlpnetRoot)=D:/bstdev/glp/GLPNET` absolute path
  ⇒ may be **unbuildable on the handsets**. Resolve (package / descope) BEFORE the Android coin-leg (step 7).
- **ID-3 + C-4 (olamnit's ledger domain):** mint authorization evidence does NOT replicate with the op-WAL, and the
  audit never verifies a mint's WitnessCert (only the spend path does) ⇒ a forged merged mint is undetected. Guts
  E-A as previously specified; olamnit owns the fix.
- **RL-3:** a fixed-N ring order is unsatisfiable under kill-one unless membership is **epoch-versioned** — the
  genesis roster pin must carry a membership epoch. (My diamond topology survives a kill without ring re-formation.)
- **E-A correction from olamnit:** MergeFrom EXISTS + is implemented (my critic's "unbuilt" was WRONG — folded);
  E-A is BIGGER not smaller (convergence/causal-ordering; Validate rejects committed spends; non-replicating evidence).

**Reordered build:** RL-1 receipt (mine) → RL-4 multi-key anchor (olamnit/shared) → [RL-2 done for gavri] →
fault-aware adapter → seal-on-egress mesh decorator → diamond+kill mesh-test-v2 → (E-A) wallet replication → 1M.

## VERIFIED-READY (CONFIRMed against code)
- **Android runtime:** `specialUse` foreground Service (not `dataSync` — Android15 ~6h cap) running on the shipped #020 dedicated thread (`DurableQF.StartOnDedicatedThread:300` + supervised `KernelHost.BindAsync`); stable cached Ed25519 ring key (fixes `Ed25519Signer` per-hop re-expansion + the ephemeral `InMemoryDeviceSealKey`); partial wakelock + high-perf WiFi lock; battery-opt exemption + **manual Samsung "Never sleeping apps"** (unautomatable operational risk); thermal self-throttle (Tab SM-X130 is the rate-limiter; `IsConstrained = ProcessorCount<=2` won't flag the 8-core Tab, `PlacementContracts.cs:18`); CRC-resume via existing `IPgliteConnection`. Pure-managed BouncyCastle ⇒ **no W^X/no-JIT surface**.
- **Windows start/stop node:** two-tier durable commit (group-commit the 72 MB chain every K + A/B head-FP checkpoint; **never finalize a hop's µcoin until its record is durable**); fixed-width CRC-framed ~72 B/record chain seekable by `seq×72`, sigs to sidecar (**crc = torn-write only; authenticity = Ed25519 + hash chain**); torn-write resume (stop at first bad frame, resume last valid `{seq,fp}`, re-broadcast fresh nonce, idempotent by `(seq,nonce)`); batched-Merkle throughput lever; split fail policy (bad-sig → halt+preserve; link-drop → deterministic FAIL record; watchdog on the node's OWN clock). Budget **72 MB; ~3–8 min at group-commit K=200 (vs 5–12 h strict); crypto never the bottleneck**.
- **Sign:** `SealSet.Seal/Verify` (`glp_crdtmsg/sig/Seals.cs`); the biscuit `Chain(block,index,count,prevSig):71` binds position/order into each seal **for free** (satisfies crypto-H1). Provenance via `RewardMinter.WriteProvenanceAsync` + `ProvenanceAudit`.
- **Transport:** ride the `MeshNodeRuntime` DV mesh (reroute-not-halt `:256`, `{origin}:{seq}` dedup `:179`, `onLocalDeliver` `:298`), NOT the QUIC hub. glp-quick is client-only (`GlpQuickLinkTransport.ListenAsync` throws `:64`) + no-ack (`SendProbeAsync` never reads a reply) ⇒ hops must be **logical + ack-gated**.
- **Coin adapter:** `RelayTransmitWorkAdapter : IWorkSignalAdapter` (the ONE new adapter) → unchanged `RewardsIngestionLoop → RewardMinter → wallet`; one `SeedMapping` row (relay_ack → 1 reward). ⚠️ **RETRACTED 2026-07-15 (olamnit C-3):** the earlier "1M hops = 1 coin (µcoin, `GenesisGrantUCoin=10_000_000`=10 coin)" claim is WRONG — reward mints are **unmergeable 1-µcoin dust** (1M ⇒ 1M dust leaves, never 1 coin) and 1 µcoin is 10⁶ off the shipped stage rate. I asserted this from develop-knowledge/docstrings about coin code **not on branch 023** (`Olamnit.Coin.Rewards` is develop-only) — an E-C-style inference-not-code failure. The reward accounting is olamnit's to specify; do not build the mapping on my retracted arithmetic.

## CORRECTIONS the critic forced (the design was wrong/imprecise)
1. **No `AddressBook.Resolve` in Olamnit.** The shipped `@name` primitive is `PrefixRouteTable.TryResolve(ushort nameId)` (resolves a **ushort**, not a string) + glpnet `Addressing.cs`. Build the authenticated roster on THAT — don't assume a named type.
2. **IPs:** use spec-050's pins **Olamnit `.136` / gavri `.108`**. `.143` is **BLOCKED/superseded** (023 discharge-ledger); **`.129` is uncorroborated** (stale). Key the roster on `@name`+pubkey, not IP.
3. **The real adapter risk is FAULT SEMANTICS, not the type mismatch.** The two `ILinkEndpoint` method surfaces are identical and the mesh keys neighbours by `ushort` (never `link.Id`), so the type split is thin. BUT GLP `QuicEndpoint.RecvBytesAsync` returns `null` on **transient** faults (`:72`), while `MeshNodeRuntime.PumpAsync` treats any `null` as **permanent** `MarkDown` + route poison (`:224,383`). Over 1M cycles every blip ⇒ reconvergence storm. **The adapter MUST distinguish transient fault (OnFault) from clean EOS and keep the neighbour up on transient.**
4. **Wrong coin exemplar.** Do NOT model `RelayTransmitWorkAdapter` on `KernelMarkerAdapter` — that's the **non-auto** exemplar (`trace_scrape`, `AutoEndorsable:false`) ⇒ a dead auto path.
5. **The mesh path is CLEARTEXT today** — `MeshNodeRuntime.HandleInboundAsync:233 TODO(FR-012) frames cross the link in clear`. `SealSet` is wired into crdtmsg, **not** the mesh forward/deliver path. A seal-on-egress / verify-on-ingress mesh decorator is **new named work**, not a shipped primitive.

## ESCALATE — olamnit/engineer decisions (I will NOT resolve these)
- **E-A (BIGGEST — olamnit owns the ledger): wallet-replication merge semantics.** `CommutativeOpProcessor.Admit` is **single-node** (`:76`); `ICoinLedger.MergeFrom` does **not exist** and a naive add is **explicitly unsafe** (projects unvalidated peer mints — forgery hole, `:59-66`). "merge=max per-(wallet,epoch)" is **WRONG** — the ledger is an op-WAL + conservation Merkle-DAG; correct merge = **op-set union + conservation re-validation + witness-cert verification**. **DECIDE:** build `MergeFrom` correctly, OR descope "survives kill-one" to a **single-authoritative-minter + 3 read-replicas**.
- **E-B: auto-endorse corroboration circular dependency.** The `relay_ack` auto class needs an **independent** authoritative record = the witness-checkpoint (**unbuilt**). Until it exists, auto **dead-expires** (the H6 lesson, verbatim in `RewardsOptions.cs`). Build the witness-checkpoint as the corroboration source's independent read FIRST; prove the ACK seal verifies against the roster before wiring any `AutoEndorsable` mapping. Needs a new `RingHopCorroborationSource : ICorroborationSource`.
- **E-C: RETRACTED — I was WRONG, olamnit was right.** VERIFIED 2026-07-15: `Olamnit.Coin.Rewards` IS on `origin/develop` AND `origin/master` (40 files each; merged via **PR #237**, develop tip `21db673` 2026-07-14). My critic *inferred* "not on develop" from "the local checkout is on `023`" — an unverified inference I passed through as fact (the exact no-theater failure). **E-C is moot; the coin is genuinely on develop.** Bonus: dev-tip commit `84ddc7c` already ships the H6 "co class manual + **ic_work feed corroborator**" pattern → E-B's `RingHopCorroborationSource` is a real requirement but a **known, shipped pattern**, not novel.

## 1M-SOAK feasibility (honest)
- **Solo single-node throughput soak: ACHIEVABLE** as sized (72 MB, group-commit K=200, crypto not the bottleneck, ~minutes).
- **The meaningful soak** (the design's own "no solo runs — measure 4-node overlap or VOID") is **NOT achievable until E-A is built with correct merge semantics.** The acceptance gate hangs off the one unbuilt + mis-specified surface.

## Draft implementation order (gavri-side, once E-A/E-B/E-C are resolved by olamnit)
1. Roster + identity map `@name↔ushort(PrefixRouteTable)↔walletId↔pubkey`; stable ring key from `HostIdentity`; loud-fail resolve. *(needs olamnit's roster decision)*
2. GLP↔Kernel `ILinkEndpoint` adapter **with fault-semantics translation** (transient ≠ down).
3. Seal-on-egress / verify-on-ingress **mesh decorator** (SealSet over the cleartext mesh path).
4. Ack-gated hop protocol (correlated successor→predecessor ACK; mint gates on the **ack**, never on `Accepted`).
5. `RelayTransmitWorkAdapter` + the `relay_ack` mapping row + `RingHopCorroborationSource` *(gated on the witness-checkpoint, E-B)*.
6. Windows start/stop node + durable chain (two-tier commit, CRC-frame, batched-Merkle, split-fail, watchdog).
7. Android node-agent (foreground service + dedicated thread + stable key + wakelock + thermal + resume).
8. Witness-checkpoints + roster-pin genesis + **wallet replication** *(E-A — olamnit-lead)*.
9. 1M soak with the anti-false-pass gate.

**NEW code:** the adapter (#2), the mesh seal decorator (#3), the ack protocol (#4), `RelayTransmitWorkAdapter` + `RingHopCorroborationSource` (#5), the Windows chain (#6), the Android service (#7), witness-checkpoints + wallet replication (#8).
**REUSED verbatim:** `MeshNodeRuntime`/`DistanceVectorRouter`, `SealSet`, `QuicTransport`, all of `Olamnit.Coin.Rewards` pipeline (minter/wallet/ingestion/mapping/epoch/provenance), `DurableQF` dedicated thread, `IPgliteConnection`, the yngenios CRC-frame/resume pattern.
