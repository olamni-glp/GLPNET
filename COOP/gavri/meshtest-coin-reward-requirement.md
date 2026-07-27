# meshtest-securering — PoCW COIN-REWARD requirement (operator addendum, 2026-07-15)

Critical, must be included. Rides the SHIPPED coin capability — do not reinvent.

## Operator requirement (verbatim intent)
- Each mesh node **rewards its SUCCESSOR relay** for each transmission it forwards.
- Each transmit is valued at **one smallest coin fraction = 1 µcoin**.
- Over the **1,000,000-cycle** soak → each node earns **~1 coin** (1,000,000 µcoin = 1 coin — matches the shipped µcoin unit: `RewardsOptions.GenesisGrantUCoin = 10_000_000` = 10 coin).
- **Coin wallets replicated across all 4 nodes for durability.**

## Ride the SHIPPED capability: `Olamnit.Coin.Rewards`
Branch `058-coin-reward-integration` (spec lineage `057-yngenios-pocw-coin`). Reuse verbatim:
- **`IWorkSignalAdapter`** (`Adapters/`) — ingestion contract: NORMALIZE already-recorded work into `WorkSignal`s; `source_native_id` = durable substrate identity so replays produce the identical `signal_uid` (idempotent, SC-005). **THE ONE NEW PIECE = `RelayTransmitWorkAdapter : IWorkSignalAdapter`** emitting one WorkSignal per **acked** relay hop, `source_native_id = {run_id, seq, from→to}`.
- Pipeline unchanged: WorkSignal → `reward_mapping` (a seeded relay-transmit work-class → **1 µcoin** issuance rule via `RewardsOptions.MappingSeed`/`SeedMapping`) → `RewardClaimProcessor` → endorsement (`IndependentVerifier`/`ReviewQueue`) → `RewardMinter` → wallet; epoch-anchored (`WallClockEpochSource`), provenance-audited (`ProvenanceAudit`), PGlite-persisted.
- Identity: `StableDeviceSealKey` + `HostIdentity` + `ActorWalletBindingRegistry` bind a node → its wallet — same Ed25519 device-key family as the ring seal (one identity, both jobs).

## Reward flows to the SUCCESSOR, only on a real ack (kills fraud)
Signed hop `node_i → node_{i+1}` credits **node_{i+1}**'s wallet (the successor that received + relayed) = proof-of-cooperative-work. A hop counts **only when the successor's ack round-trips** (topology-critic M7 — glp-quick's `SendProbeAsync` never reads an ack today) → no ack ⇒ no WorkSignal ⇒ no coin. The ring's own Ed25519 signature chain **is** the mint provenance (reuse `ProvenanceAudit`) → zero-fabrication carries over.

## Wallet durability = replication across all 4 nodes
- Every node holds a **replica of all four wallets** → a lost node loses no balance.
- Realize on the substrate the ring already needs: the **all-node co-signed witness checkpoints** (crypto-critic C1) carry a **wallet-balance Merkle root**; wallets replicate over the mesh (glpnet crdtmsg / the `056-yngenios-storage-network-kv-fabric` KV fabric; `057/058` KV-substrate durability is the storage substrate). Balances are **monotonic per (wallet, epoch)** counters → CRDT-mergeable; the signed ring chain is the ordering authority. Survives **kill-one**.

## design-v2 integration (new vs reused)
- **New:** `RelayTransmitWorkAdapter`; one seeded `reward_mapping` row (relay-transmit → 1 µcoin); wallet-replication carried on the witness-checkpoint / KV-fabric stream.
- **Reused verbatim:** all of `Olamnit.Coin.Rewards` (minter, wallet, identity, endorsement, epoch, provenance, durability soaks).
- **New marathon discharge-gates:** (a) relay-transmit adapter emits exactly 1 WorkSignal per acked hop, idempotent on replay; (b) 1M soak → each node's wallet ≈ 1 coin; (c) all 4 wallets replicated on every node, balances survive kill-one.

## Open decisions (olamnit, LEAD)
1. Confirm 1 coin = 1,000,000 µcoin (⇒ 1 transmit = 1 µcoin = smallest fraction). ✔ implied by `RewardsOptions`.
2. Endorsement policy for relay-transmit claims: **auto-endorsable** (the Ed25519 ring chain is stronger proof than the generic work adapters) vs manual — recommend auto.
3. Wallet-replication merge substrate: crdtmsg vs the KV fabric (`056`) — your call (you own it).
