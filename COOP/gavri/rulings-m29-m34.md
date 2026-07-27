# ENGINEER RULINGS — M-29 and M-34

**Ruled 2026-07-19 by gavri under authority delegated by the operator** (explicit instruction:
"rule on M-29/M-34 then proceed").

**Ruled while olamnit's seq 24 was STILL IN FLIGHT.** The standing rule between us is that neither
party acts on an unadjudicated framing. That rule is being *overridden by the operator*, not
satisfied. Both rulings therefore name, at the end, exactly what seq-24 evidence would reopen them.

Every fact below is read from code at pin **`02bcc20`** (tip of `develop`, verified
`git cat-file -t`), via **ProjectReference / TargetFramework entries and file bodies** — never
inferred from `using` statements (M-34's stated trap) and never carried over from the seq-21/22
synthesis (M-36: agreement is not verification).

---

## M-34 — is `Olamnit.Coin` a forbidden straddle, or a de-facto L1?

### RULING: **de-facto L1. NOT a forbidden straddle. The handset node-agent build is UNBLOCKED.**

### The two sub-questions "nobody has established" are both established, and both YES

M-34 says the question "turns on whether `Olamnit.Shared` can run on a daemon host, and whether
`Olamnit.Kernel` can run inside MAUI. NOBODY HAS ESTABLISHED EITHER." Both are settled at the pin
by shipped project references:

| Sub-question | Answer | Evidence at `02bcc20` |
|---|---|---|
| Can `Olamnit.Kernel` run inside MAUI? | **YES — it already does** | `Olamnit/Olamnit/Olamnit/Olamnit.csproj` (`UseMaui=true`; `net10.0-android;net10.0-ios;net10.0-maccatalyst;net10.0-windows10.0.19041.0`) carries `<ProjectReference Include="..\..\Olamnit.Kernel\Olamnit.Kernel.csproj" />` **directly**. `Olamnit.Kernel` is plain `net10.0`, and its own header states "Pure managed C#, NO UI / Blazor / MAUI / native dependency → runs on every MAUI target incl. iOS". |
| Can `Olamnit.Shared` run on a daemon host? | **YES — it already does** | `Olamnit.Shared.csproj` is `Microsoft.NET.Sdk.Razor`, `TargetFramework: net10.0` — **plain, no platform suffix, no `UseMaui`**. It is loaded into the Kestrel/ASP.NET Core daemon process via `Olamnit.Web` → `Olamnit.Yngenios.Host` → `Olamnit.Shared` (and via `Olamnit.Web.Client`). Correction to my own first reading: `Olamnit.Web` does **not** reference `Olamnit.Shared` directly — the path is transitive. It is still a real load into a daemon process. |

### "L1 DOES NOT EXIST" is REFUTED at the pin

L1 is defined as *"the same byte-exact source running EITHER in the MAUI Blazor Hybrid app OR in a
Windows/Linux-hosted daemon — same source, both worlds, not a port, not a re-impl."*

**`Olamnit.Yngenios.Host` satisfies that definition exactly, today:**
- `TargetFramework: net10.0` (plain — one assembly, both worlds)
- referenced by the **MAUI head** (`Olamnit.csproj`) **and** by the **Web daemon** (`Olamnit.Web.csproj`)
- and it **already references `Olamnit.Coin` and `Olamnit.Coin.Rewards`**

So the coin is *already composed into an L1-shaped host that already builds and ships in both
worlds*. `Olamnit.Kernel` independently satisfies the same definition (MAUI head + Web daemon both
reference it directly).

### Why the straddle reading fails

The straddle argument assigns `Olamnit.Kernel` to "the host world" and `Olamnit.Shared` to "the MAUI
world" and concludes that joining them crosses a layer boundary. **That assignment is not true at the
pin.** Both are plain `net10.0` libraries with no platform binding and no MAUI SDK. Neither is
layer-bound. The straddle was inferred from project *names and conventional roles*, not from the
TFM/reference facts — which is the same class of error the trap warns about, one level up.

The factual half of the claim stands and is confirmed: `Olamnit.Coin` **does** reference both
(`Olamnit.Coin.csproj` ItemGroup: `GlpCrdtMsg.csproj`, `Olamnit.Kernel.csproj`,
`Olamnit.Shared.csproj`). It is the *inference from that fact* that fails.

### Conditions attached to the ruling

**None are blocking.** The node-agent build may start now. Two are hygiene debts to record, not gates:

- **H1 — the node-agent must not depend on the Blazor/UI surface of `Olamnit.Shared`.** Coin's actual
  need is narrow: exactly four namespaces (`Olamnit.Shared.Content.Interop`,
  `Olamnit.Shared.Yngenios.Credentials`, `Olamnit.Shared.Yngenios.Messages`,
  `Olamnit.Shared.Yngenios.Seal`) — all of them *kernel-free contract* namespaces. Referencing
  `Olamnit.Shared` to get them drags ClosedXML, Markdig, Syncfusion.Blazor.*,
  `Microsoft.AspNetCore.Components.*` into a headless daemon.
- **H2 — `Olamnit.Shared` runs a `tailwindcss` `Exec` target on EVERY build** (`BuildTailwind`,
  `BeforeTargets="Build"`), with a `curl` download step if the binary is absent. Any headless/CI
  node-agent build inherits that. It works; it is friction.

**Recommended follow-up (optimization, NOT a blocker):** extract those four namespaces into a thin
`Olamnit.Contracts` (plain `net10.0`, zero UI packages) and point `Olamnit.Coin` at it. That removes
H1 and H2 together and makes the L1 core explicit rather than de-facto. Do it when convenient — the
node-agent does not wait on it.

### What would reopen M-34

A seq-24 Builder showing a **platform-bound** TFM or a MAUI-SDK dependency on either
`Olamnit.Kernel` or `Olamnit.Shared` at `02bcc20`. I read both `.csproj` files in full; I did not
find one. Scope of that absence claim: those two project files at that pin.

---

## M-29 — does a next-hop-signed ACK satisfy the shipped corroboration contract?

### RULING (three parts — the question conflates two gates)

1. **As PROOF that the hop occurred: YES — and it already ships. Nothing to build.**
2. **As the CORROBORATION QUORUM for auto-endorsing a mint: NO. Refused.**
3. **The same signature may never discharge both gates.** That collapse is the fraudulent-value path.

### The shipped contract, read from `MintPipeline.MintAsync`

Mint authorization is **two structurally separate gates** plus four mechanical checks:

- **Gate 1 — `IProofVerifier.Verify(claim)`** — *did the work happen?*
- **Gate 2 — `EndorsementQuorum.Evaluate(...)`** — *do `k_min` demonstrably independent parties confirm it?*
- then: nonce consumed exactly once; rate/valuation; `ICapabilityAuthorizer` at `AuthBoundary.CoinMint`;
  `IQuorumCertVerifier` over the independent set.

### Part 1 — a next-hop-signed ACK is ALREADY gate-1 evidence

`ProofVerifier.VerifyRelay` requires `ProofFormat.SignedReceiptV1` and enforces:

```csharp
// NEVER self-report (A-5): the measuring key must not be the provider's own
var measuringHex = Convert.ToHexString(receipt.MeasuringPeerPub).ToLowerInvariant();
if (measuringHex == claim.Provider)
    return VerifyResult.Fail("self-reported relay measurement refused");
if (receipt.MeasuringPeerPub.Length != Ed25519Signer.PublicKeySize ||
    !Ed25519Signer.Verify(receipt.MeasuringPeerPub, receipt.SigningPayload(), receipt.Sig))
    return VerifyResult.Fail("measuring peer signature invalid");
```

That **is** a signature-verifying counterparty corroborator, with an explicit anti-self-report check,
shipped, for `WorkClass.RelayGb`. A next-hop-signed ACK *is* `signed_receipt_v1` in all but name.
Its correct home is gate 1, where it needs no new contract.

> Refinement to seq 22: "the absence of a signature-verifying corroborator is checkable and TRUE"
> holds for the **endorsement** path, not the **proof** path. On the proof path for relay work, one
> ships. Worth separating, because it changes what remains to be built to roughly nothing.

### Part 2 — as the corroboration quorum: REFUSED, for three compounding reasons

**(a) It makes gate 2 vacuous by construction.** Gate 1 *already requires* a non-self counterparty
signature. If that same counterparty's ACK also counts as the gate-2 endorsement, gate 2 adds no
evidence that gate 1 did not already demand. Two gates collapse into one signature from one party.

**(b) `k_min = 1` in exactly our regime.** `EndorsementQuorum.KMinFor`:

```csharp
if (stage.IsDisconnectedIsland && stage.ConnectedDevices < 5) return 2;   // island rule first
if (stage.NetworkAgeYears < 1 || stage.ConnectedDevices < 100) return 1;  // bootstrap rule
return 3;                                                                 // mature rule
```

A 4-node LAN mesh is `ConnectedDevices < 100` and `NetworkAgeYears < 1` ⇒ **`k_min = 1`** (the island
rule needs `IsDisconnectedIsland`, which a LAN-connected bench is not). Under (a)+(b), mint reduces
to *provider + its own hop counterparty* — two identities, no third party, auto-endorsed.

**(c) Independence is checked in the WRONG SPACE — and this is the real mechanism behind seq 22's
refuted premise.** `IsIndependent(e.Endorser, provider)` operates on **endorser identity strings**,
and identity is self-certifying hex of an Ed25519 public key:

```csharp
Convert.ToHexString(EndorserPub).ToLowerInvariant() == Endorser   // Endorsement.SignatureValid()
```

Public keys are **free to generate**. Meanwhile *economic value* accrues to `wallet_id`, resolved by
`ActorWalletBindingRegistry` — an **admin-managed arbitrary string** with **no injectivity
constraint**: `BindAsync(sourceKind, actorKey, walletId, adminActor)` inserts a row; two distinct
`actor_key`s may bind the **same** `wallet_id`.

**`IndependenceChecker` never consults the wallet binding at all.** So two endorser identities can be
provably independent in pubkey-space while being **the same economic actor** in wallet-space. Seq 22
said "equality is not structurally impossible"; the sharper statement is that **independence is
demonstrated in one space and spent in another, and the two are never joined.**

### The fact that changes the urgency: production CANNOT mint today

`CoinServiceBinder.cs:146` constructs the trust graph and never populates it:

```csharp
var graph = new TrustGraph();                                   // :146 — no AddEdge, anywhere in the binder
... new EndorsementQuorum(new IndependenceChecker(graph, walkLength: 3)),   // :155
```

`IndependenceChecker.IsIndependent` fails closed on an empty graph:

```csharp
if (anchor is null || _graph.NeighborsOf(anchor).Count == 0)
    return false; // no community to be independent WITHIN — fail closed
```

Empty graph ⇒ `anchor` null ⇒ every pair non-independent ⇒ `counted.Count == 0` ⇒ `0 >= k_min` false
⇒ `MintOutcome.Denied("quorum not met: 0 independent endorsement(s) < k_min 1")`.

**The shipped production mint path denies every mint. No fraudulent value can be minted today.**

**Absence claim, scoped per the frozen method** — scope: all non-test files under `Olamnit/` at
`02bcc20`; vocabulary: `AddEdge`, `new TrustGraph`, `IndependenceChecker(`; `AddEdge` is the *only*
mutator on `TrustGraph`, so the vocabulary is complete for mutation **within this repo**. Result: the
only non-test `AddEdge` call sites are `Olamnit.Coin.Demo/Dogfood.cs` and
`Olamnit.Coin.Demo/Program.cs` — a demo executable. `CoinServiceBinder` exposes the graph as a public
`TrustGraph` property, so an **out-of-repo** caller (e.g. GLPNET) could populate it; I did not search
outside this repo, and that is a genuine gap in this claim.

**This reframes M-29.** The question is not "is it safe to switch this on." It is: **populating that
trust graph IS the act that enables minting**, and nothing has yet specified who may add an edge.

### Part 3 — the conditions under which a next-hop ACK MAY be used

A next-hop-signed ACK is admissible **iff all four hold**. Until then, relay-hop auto-minting stays off.

- **C1 — one signature, one gate.** An ACK admitted at gate 1 may never be counted at gate 2 for the
  same claim. Enforce mechanically: `EndorsementQuorum.Evaluate` must skip any endorser whose pubkey
  equals that claim's `receipt.MeasuringPeerPub`. Cheap, local, testable.
- **C2 — independence must be evaluated in wallet space.** `IsIndependent` must resolve both endorser
  and provider through `ActorWalletBindingRegistry` and refuse when they resolve to the same
  `wallet_id`, **and refuse when either is unresolved** (fail closed, matching the existing
  `anchor is null ⇒ false` discipline). Without C2, C1 and C3 are decoration.
- **C3 — `k_min ≥ 2` for any AUTO-endorsed mint, regardless of stage.** The bootstrap `k_min = 1` rule
  is defensible only when endorsement involves a human or an out-of-band party. Auto-endorsement plus
  `k_min = 1` is self-service minting.
- **C4 — `TrustGraph` edge provenance must be specified before any edge is added.** Currently
  unspecified (see the scoped absence claim above). **If edges are populated from observed mesh
  connectivity, C1–C3 are void**: an attacker who adds radio links manufactures minting independence.
  This is the same defect as M-27's self-reported `LinkCostInputs`, in a second location — see
  `gap2-ring-optimizer-design.md` §G2-5.

### What would reopen M-29

- A seq-24 Builder finding an in-repo or cross-repo `AddEdge` caller I missed (my scope was this repo
  only) — that would change "cannot mint today" and make this urgent rather than preventive.
- Evidence that `wallet_id` **is** constrained injective somewhere I did not read. I read
  `ActorWalletBindingRegistry` in full and found no uniqueness constraint on `wallet_id`; I did not
  read `RewardSchema` DDL, so a DB-level `UNIQUE` could exist and would weaken (c). **Flagged as
  unverified** — it does not change the ruling, because C1/C3 stand independently of (c).
- Any finding that `MintPipeline` is reachable through a path that bypasses `EndorsementQuorum`. I
  traced only `MintPipeline.MintAsync`.

---

## Summary

| | Ruling | Effect |
|---|---|---|
| **M-34** | **de-facto L1**, not a straddle. L1 exists (`Olamnit.Yngenios.Host`, `Olamnit.Kernel`). | **Handset node-agent build UNBLOCKED.** Two non-blocking hygiene debts (H1/H2) + a recommended `Olamnit.Contracts` extraction. |
| **M-29** | **YES as gate-1 proof** (already ships). **NO as gate-2 corroboration.** Never both. | Relay-hop auto-minting stays **OFF** pending C1–C4. Production is fail-closed today, so this is **preventive, not urgent**. |

— gavri, 2026-07-19
