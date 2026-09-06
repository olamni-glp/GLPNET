# Feature Specification: Revoked trust material is refused at load

**Feature ID**: `revoked-trust-material-refused-at-load`
**Spec directory**: `specs/109-revoked-trust-refusal`
**Created**: 2026-09-06
**Status**: specified
**Roadmap**: WSJF 19.5 · RICE 242250 · rank #1
**Engineer ruling**: G-03 (2026-09-06) — refuse the revoked SPKI at load as a **hard-coded
constant**, not a config-driven denylist; the current-generation assertion is a **complement**,
not an alternative.

---

## Why this exists (mandatory context)

On 2026-09-06 this lane measured, and proved by hash, that **SHIRAS is serving a QUIC private key
that is published in public git history**:

```
sha256  94fbe87d:glpquick-cert/glpquick.key   93cfb06aec576d3b292fc071903cb48a0c76f02d06bed975ff081e5e980d14a0
sha256  SHIRAS live glpquick.key              93cfb06aec576d3b292fc071903cb48a0c76f02d06bed975ff081e5e980d14a0   IDENTICAL
sha256  ARIELLAS live glpquick.key            de0cc051b575ffa9747b1f995a1d3034be39880d789e7ce71ef3fe15035714da   (gen-3, correct)
```

Commit `94fbe87d` is reachable from `origin/main`, `origin/develop` and 10+ origin branches.
SHIRAS's four files are dated **2026-09-04** — restored from git history two days ago.

**The defect is not one lane's carelessness; it is a trap the repository sets.** The material is
`.gitignore`d and untracked, so it cannot be restored from `HEAD`. When `SharedCertMaterial.Load`
fails closed with *"could not locate glpquick-cert/glpquick.pfx"*, the only place those four
filenames still exist together is git history — so `git checkout <old-sha> -- glpquick-cert/` is
**the first thing that works**, and it silently restores the compromised generation. GAVRIS hit the
identical abort on the same day (`glpquick-cert/` reduced to one file, dir mtime **2026-08-12**,
untouched for 25 days) and was one command away from making the same mistake.

### The precise gap, measured at source

`csharp/glp_link/transports/SharedCertMaterial.cs` is already rigorously fail-closed. It refuses a
missing pfx, a missing pin file, an empty pin, a private-key-less cert, and — importantly — a
**self-inconsistent** pair, because it recomputes `QuicTransport.SpkiPin(cert)` and requires it to
equal the fingerprint file's contents.

**Every one of those checks passes for SHIRAS's material.** The restored gen-1 `.pfx` and gen-1
`.fingerprint` were restored *together*, so they agree with each other perfectly.

> **The loader validates INTERNAL CONSISTENCY. It has no notion of IDENTITY.**
> A coherent-but-revoked generation is, to this code, indistinguishable from a coherent-and-current
> one. That single sentence is the whole feature.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — A revoked generation is refused by name (Priority: P1)

An operator whose `glpquick-cert/` is missing recovers it the obvious way, by checking the files
out of git history. The next link establishment **refuses loudly**, names the material as revoked,
and says what to do instead — rather than establishing links on a key an outsider holds.

**Acceptance scenarios**

1. **Given** `glpquick-cert/` contains the gen-1 material (self-consistent: pfx and fingerprint
   agree, private key present), **When** trust material is loaded, **Then** the load **fails** with
   an error that (a) states the material is **revoked**, (b) quotes the offending pin, and (c) names
   the remedy — obtain current material from a peer; do not check it out of git history.
2. **Given** the same material, **When** the load fails, **Then** **no link is established** and no
   degraded or no-pin mode is entered.
3. **Given** current (gen-3) material, **When** trust material is loaded, **Then** the load
   **succeeds** unchanged — the guard is invisible to correct material.

### User Story 2 — Any non-current generation is refused, not just the one we enumerated (Priority: P2)

The revoked-pin list only catches material somebody has already identified as bad. A
**current-generation assertion** is fail-closed by construction and catches material nobody has
enumerated — including a future compromise, a partial restore, or a hand-generated gen-4.

**Acceptance scenarios**

1. **Given** material whose pin is neither the current generation nor any enumerated revoked pin,
   **When** loaded, **Then** it is refused with a message distinguishing *"not the current
   generation"* from *"explicitly revoked"* — the two are different operator situations.
2. **Given** current material, **When** loaded, **Then** it succeeds.

### User Story 3 — The suite proves the guard fires (Priority: P2)

A guard that has never been observed to fire is a guard that has never been tested.

**Acceptance scenarios**

1. **Given** a test fixture carrying the revoked pin, **When** the suite runs, **Then** a test
   asserts the refusal **fires** — a **positive control**.
2. **Given** current material, **When** the suite runs, **Then** a test asserts the guard **does
   not** fire — a **negative control**, so a guard that refuses everything cannot pass.

### Edge Cases

- **A pin file that is current but a pfx that is revoked** (a partial restore). The existing
  consistency check already refuses this pair; the new guard must not weaken or bypass it, and the
  refusal must remain attributable to whichever check fired first.
- **Whitespace, casing and trailing newlines** in the fingerprint file. Comparison must be on the
  parsed pin value, so a trailing `\n` cannot smuggle revoked material past a naïve string compare.
- **The guard's own constant being wrong.** If the revoked constant were ever set to the *current*
  pin, every host would be refused. The negative control (US3 #2) is what makes that a failing
  build rather than a fleet outage.
- **Derived per-device credentials** already have their own append-only revocation set at
  `glpquick-cert/provision/revoked.jsonl` (`DerivedCredentialValidator`). This feature is about the
  **shared root** material and must not be confused with, or wired into, that mechanism.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The shared-trust-material loader MUST refuse material whose SPKI pin appears in an
  enumerated set of **revoked** pins.
- **FR-002**: The revoked set MUST be a **compiled-in constant**, not a configuration file, not an
  environment variable, and not a data file read at run time. *(Engineer ruling G-03. Rationale: a
  denylist that ships empty admits everybody — the inverse of the "empty pin table admits nobody"
  tension raised on SC-001. A denylist is fail-open by nature; making it a constant removes the
  empty-file failure mode entirely.)*
- **FR-003**: The revoked set MUST contain `0LOmLNM0HYv79Rkoasuu6L4MKGRyg7axgJufbZBcyTo=` — the
  gen-1 pin, already named in `.gitignore` as one that *"must never be trusted again by any peer."*
- **FR-004**: The loader MUST additionally refuse material whose pin is **not the current
  generation**, with a message distinguishing that case from an explicitly-revoked one.
- **FR-005**: A refusal MUST name the offending pin, state which rule refused it, and state the
  remedy: obtain current material from a peer host — **explicitly NOT** by checking it out of git
  history.
- **FR-006**: A refusal MUST be a hard failure on the existing fail-closed path. No degraded mode,
  no no-pin mode, no warning-and-continue.
- **FR-007**: The guard MUST NOT weaken, reorder or bypass any existing check in the loader
  (missing pfx, missing/empty pin file, absent private key, cert/pin inconsistency). Ordering MUST
  be pinned by test so a later edit cannot merge or reorder the clauses.
- **FR-008**: Comparison MUST be against the parsed/normalised pin value, insensitive to
  surrounding whitespace and trailing newlines.
- **FR-009**: The suite MUST carry a **positive control** (the refusal fires on revoked material)
  and a **negative control** (it does not fire on current material). Both are required; either
  alone is insufficient.
- **FR-010**: This feature MUST NOT rotate the pin, mint new material, or modify git history.
  *(History rewrite is `F7b`, explicitly a coordinated multi-host window and never a single-host
  act. Rotation is what invalidated the original exposure and is not re-litigated here.)*

### Key Entities

- **SPKI pin** — `base64(SHA-256(SubjectPublicKeyInfo))`; the unit of trust identity, per FR-011 of
  feature 050. Trust is the pin, not expiry/CA/hostname.
- **Generation** — a rotation of the shared material. gen-1 (`0LOm…`, **revoked**, private key
  public), gen-3 (`jKMV…`, **current**, installed 2026-08-10 by feature 069).
- **Shared root material** — `glpquick-cert/{glpquick.pfx, glpquick.fingerprint, glpquick.key,
  glpquick.pem}`; gitignored, untracked, distributed peer-to-peer. Distinct from derived per-device
  credentials.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A host holding revoked material cannot establish a link. Verified by loading the
  revoked fixture and observing refusal — measured, not asserted.
- **SC-002**: A host holding current material establishes links exactly as before. Verified by the
  existing suite remaining green, including the 064 service-box drills that depend on this loader.
- **SC-003**: The refusal message is sufficient to act on without reading the source: it names the
  pin, the rule, and the remedy. Verified by asserting all three appear in the message text.
- **SC-004**: The guard is proven to fire **and** proven not to over-fire. Both controls present and
  green; removing either fails the build.
- **SC-005**: Introducing the revoked material and running the suite produces a **named** failure,
  not a timeout, a crash, or a silent pass.

---

## Assumptions

- The current generation is gen-3, pin `jKMVqlvEL0evFBPw4TWIlEln3TBbXT1u1t072Zp1AlY=`, as measured
  on ARIELLAS (2026-08-10 files) and as installed on GAVRIS on 2026-09-06.
- Only gen-1 is known to be compromised. gen-2 is not enumerated because no evidence of it was
  found on any of the four hosts; **FR-004's current-generation assertion is what covers unknown
  generations**, which is precisely why it is specified alongside FR-001 rather than instead of it.
- `.gitignore` continues to cover `glpquick-cert/`, so restored or copied material cannot be
  committed. Verified on GAVRIS after restoration (`git check-ignore -v` confirms).
- Remediating SHIRAS's live exposure is an **operational** act owned by SHIRAS and is not blocked
  by, nor does it block, this feature. This code guard cannot un-publish a key that is already
  public.

## Out of scope

- Rotating to a gen-4 pin.
- Rewriting git history to purge `94fbe87d` (`F7b`).
- Changing how material is distributed between hosts.
- The derived per-device credential revocation set (`provision/revoked.jsonl`).
