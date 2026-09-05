<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature Specification: Stable federation identity — a persisted per-host QUIC keypair

**Feature ID:** `103-stable-federation-identity` · **Branch:** `103-stable-federation-identity`
**Created:** 2026-09-04 · **Host:** ARIELLAS · **Lane:** glpnet
**Roadmap:** `stable-federation-identity-persisted-quic-keypair` (WSJF 34.0 · RICE 4800 · promoted)
**Question of record:** `Q-GLPNETA21-01` (severity: critical) · **Authorising ruling:** `Q-GLPNETG27-04`

> 🔴 **Order of work, stated plainly.** The defect was found at 17:35Z with all four hosts about to
> reboot, and the fix is micro. Implementation therefore preceded this document. This spec is
> written from the code and the measurements that exist, not from intent — where the two disagree,
> the code and the measurement win and this document is the defect. Nothing below is a plan for work
> that has not happened; §7 states exactly what remains.

---

## Problem

Ruling `Q-GLPNETG27-04` authorises four-host QUIC federation on *"`CreateDevCert` material and the
four SPKI pins exchanged over the existing coop channel"*, with exposure bounded twice: unreachable
off-LAN, and mTLS SPKI pinning that refuses any unpinned dialer.

**The pinning half of that guarantee cannot hold, because the pin is not stable.**

Measured on ARIELLAS, 2026-09-04T17:35Z — the same probe binary, five runs, one unchanged host:

```
WlZZENu7qj3+B0UZMBpDmeTA5HH1TieNZsRrTFE4wrA=
TVRmozIGINqMOjRG9KGXbCXRtn9erbPwLLSq6Mxusgw=
/2lW10pnLp9gSxHpQcNA7hHgO5MfJoI/leTGeRS0uxE=
QVRAFjC4kibLJOgMoyzvzEkJomi5p3AB8+lKGGI3rPw=
gX2eguTlEdgKeda/LgG2pbwyKG+P8KnLBkgd8EekuZM=
```

Root cause, read in the source rather than inferred:
`csharp/glp_crdtmsg/route/QuicLinkTransport.cs` — `CreateDevCert` calls
`ECDsa.Create(ECCurve.NamedCurves.nistP256)` on **every** invocation; the local holding the result is
literally named `ephemeral`; there is no load-from-disk, no keystore and no reuse path anywhere in
the transport.

### Why it was urgent rather than merely interesting

A pin table exchanged before the reboot is invalid for **every host simultaneously** the moment they
come back up, and mTLS then refuses **every** peer. **A universal mTLS refusal is indistinguishable
from a dead transport.** The estate had just spent days concluding *"there is no QUIC listener in
this estate"* — a conclusion two independent probes on two hosts have now falsified. A stale-pin
failure after the reboot would have re-opened that settled question and sent the fleet back down a
road it had already finished walking.

### What is NOT the defect

**`CreateDevCert` is correct.** It is honestly named, it does exactly what a per-test throwaway
should do, and `QuicLinkTransportTests.cs` uses it exactly as intended. The defect is that the
federation plan **adopted a test helper as the fleet's trust anchor**. The fix therefore adds the
sibling that should have existed and re-points the callers; it changes no line of `CreateDevCert`
beyond a warning in its doc comment.

---

## Requirements *(mandatory)*

| id | requirement | how it is verified |
|---|---|---|
| **FR-001** | A host's published SPKI pin MUST be identical across process restarts and reboots | 5 probe **processes** → one pin; `StabilityAcrossCalls_FivePinsAreOnePin` |
| **FR-002** | The identity MUST be per-host and per-name, so broker/guardian/oracle do not share one keypair | `DistinctNames_GetDistinctIdentities` |
| **FR-003** | First run mints and persists; every later run loads. The caller MUST be able to tell which happened | `FirstCallMints_SubsequentCallsLoad` |
| **FR-004** | Existing-but-broken material MUST be refused, never silently replaced (a silent remint looks like success and then refuses every peer) | `MismatchedFingerprint_IsRefused`, `EmptyFingerprint_IsRefused`, `OrphanedFingerprint_RefusesToSilentlyMint` |
| **FR-005** | Rotation MUST be explicit; nothing (clock, expiry, error path) may rotate implicitly | `ExplicitRotation_ChangesThePin_AndOnlyWhenAsked`; minted lifetime 3650 d so expiry cannot trigger one |
| **FR-006** | Concurrent first starts MUST converge on ONE identity — never two callers holding two pins | `ConcurrentFirstStart_ConvergesOnOneIdentity` (16 parallel; exactly one minter) |
| **FR-007** | Private key material MUST be owner-only **from the moment the file exists**, not tightened afterwards | Unix `UnixCreateMode` 0600 at `open(2)`; Windows protected DACL applied to the empty file before any byte is written — **measured**: `protected=True`, single ACE |
| **FR-008** | The keystore MUST live outside every repo, so a clone/clean/branch-switch cannot destroy the fleet's pins | `DefaultKeystoreDir_IsOutsideTheRepo`; default `<LocalApplicationData>/glpnet/federation` |
| **FR-009** | An interrupted rotation MUST be refused with an ACTIONABLE diagnosis, not a bare "inconsistent" | `InterruptedRotation_IsRefusedWithAnActionableDiagnosis`, `PinNewerThanKey_IsDiagnosedAsAPublishedPinForAKeyWeLack` |
| **FR-010** | No temp file holding unclaimed key material may survive a completed call | `ConcurrentFirstStart_LeavesNoTempKeyMaterialBehind` |

### Explicitly out of scope (and why)

- **Encrypting the PFX at rest.** Any password would have to live on the same filesystem as the key;
  DPAPI would be Windows-only and breaks the cross-platform requirement. The control is file
  permission (FR-007). Residual risk is stated, not hidden — see `Q-GLPNETA21-05` in §7.
- **Rotation policy** (when, how often, who re-publishes). The mechanism is here; the policy is an
  engineer ruling.
- **Distributing the pins.** The coop channel already carries them; this feature makes the value
  worth carrying.

---

## Design

One new type in the **shared** transport layer, `GlpRuntime.Link.Transports.FederationIdentity`,
beside the `SpkiPin` discipline every QUIC caller already delegates to.

```
FederationIdentity.LoadOrCreate(commonName, keystoreDir = null, rotate = false)
  -> (Cert, Pin, PfxPath, Created)
```

- **Storage**: `<keystore>/<name>.pfx` (PKCS#12, private key included) + `<name>.fingerprint`
  (`base64(SHA-256(SPKI))`). The sidecar is the *published* artifact; the pfx is the *authority*.
- **Location**: `GLPNET_FEDERATION_KEYSTORE` if set, else `<LocalApplicationData>/glpnet/federation`.
- **Claiming**: mint → temp file (created owner-only) → **atomic same-directory rename**. Exactly one
  process wins; losers discard their keypair and adopt the winner's. A `WriteAllBytes` would instead
  hand two callers two different pins and persist only one — the original defect wearing a race.
- **Repair vs refusal**: a pfx whose sidecar is missing is **repaired** (the pin is a pure function
  of the cert, so re-deriving invents nothing). A sidecar that **disagrees** with the cert is
  **refused** — that is the state where a host presents one identity and peers hold another.

### Relationship to `SharedCertMaterial` (feature 050, FR-010/FR-011)

That type also persists a cert, and reusing it here would have been wrong. It loads **one shared
credential that both ends present**, so every holder has the same pin: it is a **membership token**
and cannot tell peer from peer. Federation pins are keyed per-peer, so each host needs its **own**
durable keypair. Both are persisted; only this one is an **identity**. Its fail-closed
cert/pin-consistency discipline is reused verbatim rather than reinvented.

---

## Verification actually performed

| evidence | result |
|---|---|
| `glp_link` unit suite | **217/217 green** (19 of them new) |
| `glp_crdtmsg` QuicLinkTransport suite | **8/8 green** (unchanged behaviour) |
| Cross-process, default keystore | 5 probe **processes** → `QzVUqqBTKP1uEr45isj2r3Qc+JlZwIlqtw5o6gGJ3B8=` five times; run 1 `MINTED`, runs 2-5 `LOADED` |
| Cross-process, fresh keystore via env var | `MINTED` then `LOADED`, same pin — the mint path and the override seam both exercised |
| Federation-capable bind with the persisted identity | `0.0.0.0:47890` — **LISTENER BOUND**, exit 0 |
| Windows ACL, measured with `Get-Acl` | `AreAccessRulesProtected=True`; single ACE `ARIELLAS\ariel : FullControl` |
| Adversarial review | **codex CLI, two cycles** — see §6 |

## §6 · Adversarial review record (codex CLI, cross-provider)

**Cycle 1** returned six findings; all six were addressed in code:

| # | finding | disposition |
|---|---|---|
| 1 | **CRITICAL** concurrent first-start remint race | fixed — atomic rename claim; **CLOSED** at cycle 2 |
| 2 | **HIGH** non-atomic overwrite of the pfx/sidecar pair | each file now atomic; the *pair* is not, and the residual is refused with a diagnosis (FR-009). Cycle 2: **PARTIAL by design** |
| 3 | **HIGH** no explicit Windows ACL on the unencrypted PFX | fixed — protected DACL applied **before** any key byte; null SID now throws instead of inheriting |
| 4 | **HIGH** Unix umask window + symlink following | fixed — `UnixCreateMode` at `open(2)` + `CreateNew`; **CLOSED** at cycle 2 |
| 5 | **MEDIUM** tests pin neither the race nor a restart | in-process race + disk-only reload tests added; the cross-process evidence is the 5-process probe run, not an automated test — **PARTIAL, stated** |
| 6 | **MEDIUM** rotation tested only on the success path | interrupted-rotation and wrong-direction tests added |

Cycle 2 also raised two new defects, both fixed: rotation returned `Created: true` against a doc that
said "first run" (the doc now states the exact meaning), and a temporary certificate was not disposed.

**Two findings remain PARTIAL and are recorded as such rather than closed.** #2's pair-atomicity
would need a single-file format or a cross-process lock read by every reader; #5's cross-process test
would need a spawned-process harness. Both are candidates for the hardening feature, not silent
omissions.

---

## §7 · What remains — nothing here is done and claimed otherwise

1. **`Q-GLPNETA21-02` — the federation UDP port is still unpublished.** `47890` binds free on
   ARIELLAS and is recommended, but no firewall rule will be opened on a guessed port.
2. **`Q-GLPNETA21-05` (new) — key-at-rest policy.** The PFX is unencrypted, protected by file
   permission alone. An engineer ruling is needed on whether that is sufficient for a LAN-bounded
   trust anchor.
3. **Rotation policy** — mechanism only; cadence and re-publication protocol unruled.
4. **Push / PR / release are BLOCKED on this host** — the environment's command classifier refused
   `git push` in both shells. Commits and the merge exist locally on `develop`; nothing is on origin.
5. **The other three hosts are unfixed.** Every lane hosting a QUIC service must move off
   `CreateDevCert`; this repo now makes that a one-line change, but it is their change to make.
