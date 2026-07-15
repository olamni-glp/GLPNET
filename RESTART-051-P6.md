# RESTART — 051-ynet-transport · P6 (US5 sealed routes + anonymity, T034–T039)

**Safe-restart handoff. Read this first, then run the "How to resume" block.**
*(Supersedes `RESTART-051-P5.md`, whose phase is now done — that file can be deleted.)*

## Where

- Repo (git worktree): `D:/bstdev/glp/GLPNET.worktrees/051-ynet-transport/`
- Branch: `051-ynet-transport`
- C# solution root: `csharp/` (net10.0, `dotnet test` runner, xUnit)
- Feature spec: `specs/051-ynet-transport/` (spec.md, tasks.md, contracts/transport-capability.md, BUILD-ROADMAP-051.md, data-model.md)

> **Phase numbering is off-by-one between the two docs — don't get confused.**
> BUILD-ROADMAP `P5` == tasks.md `Phase 6` == **US4 relay forward (done)**.
> BUILD-ROADMAP `P6` == tasks.md `Phase 7` == **US5 sealed routes (next)**.

## State at handoff (2026-07-15)

- **Clean baseline**: HEAD = `21effaf5` "P6 US4: real relay forward — circuit-relay-v2 + Tor-cell, ciphertext-only (T028-T033)". `dotnet test` = **89/89 green** (70 baseline + 19 new).
- Working tree clean except **pre-existing untracked** files under `.specify/roadmap-sync/` (NOT ours — leave them, never commit them in a scoped checkpoint) and the RESTART notes.
- **P5 / US4 DONE** — T028, T029, T030, T033 landed real + tested. RESUME marker in BUILD-ROADMAP-051.md now points at **P6**.
- Marathon run: `mrun-4056c02754bd` [open], steps **2/2 complete**, **1 outstanding item** (T031a, parked — see below). Stays open until `pipeline:ship` (correct — don't try to discharge).

### What US4 landed (build ON these — do not rewrite)

- **`Relay/RelayCapability.cs`** — the US4 slice; one node plays both roles:
  - *as a client*: `Offer` (wired to `OfferRelay`) enforces the 056 `AdmissionProof`; a revoking proof REMOVES the relay and tears live paths down at the next frame boundary → `authorized_but_unreachable` (R3).
  - *as a relay*: `AcceptTransit` gates third-party transit on the leaf hook (FR-016); `SetMode` now drives `LeafMode`.
  - `RelayTransit` = the bidirectional pump; never opens a payload.
- **`Relay/CircuitRelayV2.cs`** (T029) — voucher-gated mesh forward; reservation HMAC-bound to (relay, peer, traffic class, expiry): unforgeable, untransplantable, expires.
- **`Relay/TorCellRelay.cs`** + `CellChannel` (T030) — fixed-512B cells for internet/critical; pads the payload LENGTH away; fragments + reassembles at the endpoints.
- **`InProcessFabric`** now implements `IRelayChannelResolver` — a co-hosted relay really forwards.
- **`YnetSession`** threads `PathType` (additive, default `Direct`) so a relayed link reports `RelayHops: 1` (FR-023).
- Tests: `contract/RelayAdmissionTests.cs` (11), `integration/RelayForwardTests.cs` (8).

### ⚠️ OPEN ENGINEER DECISION — T031a (do not resolve autonomously)

**T031a (DSDV internet route, FR-021) is parked/blocked — raised as `co #220`.** It is the routing
substrate; the relay mechanism above does not depend on it. It needs a ruling on:

1. **Harvest vs reference** — the substrate is cross-repo at `D:/bstdev/research/olamnit/Olamnit/Olamnit.Kernel/Mesh/` (`DistanceVectorRouter.cs` 280L, `MeshRelayRoute.cs` 86L). No project reference exists from GLPNET.
2. **Identity mapping** — olamnit DSDV keys nodes by `ushort` LAN mesh ids; YNET `NodeId` is a self-certified 64-hex SHA-256(pubkey) (FR-002). The mapping must be *declared*, not improvised.
3. **olamnit's own open joint call** — `MeshRelayRoute.cs` says: *"R1 (a) OPEN JOINT CALL: this Route binding is the **proposed** shape … if the radio-back joint call rebinds onto `MeshRouter`, only this registration changes."*
4. **Wire** — the NAT-piercing leg needs real UDP/QUIC/STUN → an injected honest seam (Constitution II).

Deliberately **no seam file was written**: any API shape would prejudge (1)–(3). A from-scratch DSDV
named "extended olamnit DSDV" would fabricate the reuse FR-021 specifies.

## What P6 is (US5 — Waylet-seal + selectable anonymity, P2)

Goal: sealed routes with metadata protection + `SafetySelection`; `normal|sealed` choice; **fail-closed**.
Independent test: no single relay learns both endpoints on a sealed path; level changes path props.

### Already landed (build ON these)
- **T037 [X]** `Seal/RoutingAndMixTrust.cs` — mix-trust selection: stake-weighted via 057 (seam, dep not shipped) + **Loopix semi-trusted fallback**; fabricates nothing when neither is available.
- **T038 [X]** routing-mode choice (`normal` | `sealed`) fail-closed, no silent downgrade.
- `RoutingSelection` (`Capability/IYnetTransport.cs`) — `SafeDefault` = sealed + anonymity 1 + internal reach (FR-011).
- `RefusalReason.SealUnavailable` already exists for the fail-closed path.

### Remaining P6 tasks
- **T034 [ ] [P]** Contract test → `csharp/ynet_transport.tests/contract/SealedRouteTests.cs`: `sealed` never downgraded to clear (`seal_unavailable` on fail); unspecified → safe default (FR-011, SC-005/SC-006).
- **T035 [ ]** Sealed routes + I2P-style **garlic bundling** (no fixed 3-hop); no single hop learns both endpoints → `csharp/ynet_transport/Seal/SealedRoute.cs` (FR-009, SC-005).
- **T036 [ ]** Veilid **`SafetySelection`** (hop_count/stability/sequencing; Safe|Unsafe) mapping level → concrete path props → `csharp/ynet_transport/Seal/SafetySelection.cs` (FR-010, SC-006).
- **T039 [ ] [P]** Integration test → `csharp/ynet_transport.tests/integration/AnonymityTests.cs`: sealed vs normal metadata visibility; level→path-property mapping; zero silent downgrades (SC-005/SC-006).

Suggested order: **T034 (contract, red) → T036 SafetySelection (pure mapping, easiest real win) → T035 garlic/sealed route → T039 integration.** The US4 relay substrate is the layer to build the multi-hop sealed path over (`ConnectViaRelay` + `RelayCapability`).

## Design guardrails (carry these — enforced through P1–P5)

1. **Constitution II — honest seams, never fake robustness.** Real network I/O → implement the deterministic/verifiable logic REAL + tested and leave the wire as an injected seam that throws a clear `NotSupportedException`. Never claim SC-pass for unexercised code. Mark `[~]` in tasks.md **only when a compiling seam file actually exists**; a blocked/not-started task stays `[ ]` with a note (see T031a).
2. **This tier enforces, never decides** policy. Consume 056's decision; don't mint it (FR-024).
3. **`Result<T>` + distinct `RefusalReason`, no silent drops** (invariant 1). If P6 needs a refusal reason the enum lacks, **raise a `co issue` (schema_gap) FIRST**, then add the additive value to `RefusalReason` in `IYnetTransport.cs` and document it in the `contracts/transport-capability.md` refusal table. (P4 added `RecordNotFound`/`RecordRejected` this way — co #218; P5 needed none.)
4. **Metadata protection is a REAL testable property** (T034/T039), like ciphertext-only was in P5: assert on what a hop can actually observe. The P5 `TappedChannel` in `integration/RelayForwardTests.cs` is the pattern for tapping a wire.
5. **Wiring pattern**: real engine in `Seal/*.cs`, wire the capability to delegate via an optional-dependency ctor param (like `Dht.DhtCapability? dht` and `Relay.RelayCapability? relay`), honest throw only for an unconfigured node.
6. Tests: xUnit `[Fact]`, deterministic clock `() => DateTimeOffset.UnixEpoch`, in-process fabric (`InProcessFabric`); namespaces `Ynet.Transport.Tests.Contract` / `.Integration`. Internals ARE visible to the test assembly (`InternalsVisibleTo` in `YnetTransport.csproj`).

## co (continuous observation)

`python D:/bstdev/lang/hatzinor/src/co.py init` (idempotent). Log errors/issues per hatzinor CLAUDE.md.
Raise a `co issue` before any lossy fallback. Open from this session: **#220** (T031a premise — needs
an engineer ruling), **#219** (the `co issue --category` enum lacks values for 3 of the 6 raise-triggers
CLAUDE.md mandates, and `co issue` has no `--detail` flag).

## Marathon checkpoint ceremony (the CLI needs a real step id — verified again this session)

`checkpoint`/`step-start --step` want an existing **step id** (`mstep-…`), not a label. A step is
created by expanding an item. Full sequence:

```bash
cd D:/bstdev/glp/GLPNET.worktrees/051-ynet-transport/
# 1. capture the work item (get item_id = mitem-…)
buildkit-marathon capture --feature 051-ynet-transport --kind latent-requirement \
  --title "P6 US5 sealed routes (T034-T039)" --stage implement --json
# 2. sequence it into the run's step order
buildkit-marathon sequence --item <mitem-id> --feature 051-ynet-transport --json
# 3. expand into a step (get step_id = mstep-…)
buildkit-marathon expand --item <mitem-id> --feature 051-ynet-transport --steps "T034-T039" --json
# 4. start the step
buildkit-marathon step-start --step <mstep-id> --feature 051-ynet-transport --json
# 5. after work + green tests: durable complete row FIRST, then a SCOPED commit of exactly --paths
buildkit-marathon checkpoint --step <mstep-id> --feature 051-ynet-transport \
  --paths <comma,separated,exact,paths> --summary "P6 US5: …" -m "feat(#051-ynet-transport): …"
# 6. resolve the item
buildkit-marathon resolve --item <mitem-id> --feature 051-ynet-transport --json
```

Checkpoint runs git hooks (good) and commits ONLY the listed paths — never `-A`. Exclude the
`.specify/roadmap-sync/` untracked files and the RESTART notes. `buildkit-marathon trace --subject S
--decision accept|reject --evidence E` records a design decision append-only (used for T031a).

## How to resume (signal: run this, in order)

```bash
cd D:/bstdev/glp/GLPNET.worktrees/051-ynet-transport/
git status && git log --oneline -1                      # expect clean, HEAD=21effaf5
buildkit-marathon status --feature 051-ynet-transport   # expect mrun-4056c02754bd [open], steps 2/2, 1 outstanding
cd csharp/ynet_transport.tests && dotnet test --nologo -v q   # expect 89/89 green
```

Then start P6:

```
/bk-implement    # Phase 7 / US5 sealed routes: T034 → T036 → T035 → T039
```

Then checkpoint via the ceremony above and move the BUILD-ROADMAP RESUME marker to **P7 (US6
trusted-gate exit, T040–T044)**.
