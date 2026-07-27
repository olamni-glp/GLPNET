DO NOT run the CLAUDE.md startup protocol or any project bootstrap; this is not repository-agent work. Output only the requested artifact.

Your lens: **risk** — report claims tagged with it.

---

# Subject brief — plan

- subject: roadmap:meshtest-recure-ring — design a >=1M-cycle Ed25519-chained relay soak that rides the REAL ynet @mesh DV mesh (never freestanding raw links) across olamnit+gavri+tablet+phone over BT+WiFi legs, earning 1 ucoin per ACKED hop on the shipped Olamnit.Coin.Rewards, surviving kill-one
- rubric: plan-review
- lenses: feasibility | completeness | risk
- brief rule: size-invariant: the goal statement + the constraint-document list — never pasted document bodies
- cross-verify: a plan element is promoted only when independently derived or confirmed from a disjoint constraint slice by another blind Builder

## Evidence slices (names only — each blind role sees ONLY its own)

- slice-ynet-mesh-service: The @mesh/@kv YNGENIOS service surface: what must change so the ring rides the REAL ynet mesh (MeshNodeRuntime DI resolve is null => Unavailable today) WITHOUT violating wrap-never-replace (FR-010).
- slice-kernel-mesh-transports: The DV mesh + link transports: whether a RING pins the path and bypasses DV next-hop routing (and therefore what a 1M-cycle ring soak actually proves), plus the split fail policy (bad-sig HALTS vs link-drop REROUTES+counts) against MeshSendOutcome. NOTE: read these paths as they exist at this commit.
- slice-crypto-durability-evidence: Signing + durable chain + measured evidence: SealSet reuse over re-impl, the fsync/group-commit throughput floor that binds the 1M budget (fsync, not BLE), CRC-frame resume, and whether the recorded bench numbers honestly support the cycle arithmetic.
- slice-coin-ledger-merge: The coin ledger's replication + endorsement surface: whether ICoinLedger.MergeFrom is in fact implemented and dot-keyed, whether folding peer ops bypasses the admission checks where conservation/no-negative/exactly-once are enforced, what a correct re-validation-before-projection actually requires, and whether an auto-endorsed relay-hop class can be corroborated by evidence the claimant cannot forge.

---

## Your evidence slice: slice-crypto-durability-evidence

Signing + durable chain + measured evidence: SealSet reuse over re-impl, the fsync/group-commit throughput floor that binds the 1M budget (fsync, not BLE), CRC-frame resume, and whether the recorded bench numbers honestly support the cycle arithmetic.

Sources (yours ALONE — do not consult anything outside this list):

- Olamnit/Olamnit/Olamnit.Shared/Yngenios/Seal/
- Olamnit/Olamnit.Kernel/DurableExecution/
- specs/059-hw-3device-mesh-failover/
- tools/mesh-bench/
