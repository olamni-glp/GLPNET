<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: QR-code link + cert provisioning via generated PDF or hub display page

**Feature Branch**: `067-qr-link-provisioning`
**Created**: 2026-08-04
**Status**: Draft
**Input**: User description: "QR-code link + cert provisioning via generated PDF or hub display page. Joining a device or host to the glp-quick QUIC+WS mesh currently requires hand-copying the shared cert dir (pem/key/pfx/fingerprint) plus endpoint params out-of-band; 049 US3 hit this twice on real hardware (cert absent on second host, SMB credential walls blocked push and pull), and phones/tablets have NO copy channel at all, blocking android-quick-link-endpoints (olamnit-assistant consumer). Deliver one-scan provisioning: glp-quick renders the link endpoint (addr/port/SPKI pin) and trust material as one or more QR codes (chunked to QR capacity with integrity checks), presented either as a generated PDF or as a hub display page; a new endpoint scans to acquire the full trust bundle and joins the mesh under the 036 manual-pin trust model (unchanged). MANDATORY first-class security posture, not optional and NOT waivable by time-boxing (Gabi correction 2026-07-08): the shared cert/private-key (pfx) is long-lived, unchangeable TRUNK credential material for public infrastructure — NEVER render the trunk key itself; provision short-lived, per-device, revocable derived credentials instead; encrypt the QR payload (one-time passphrase / out-of-band key); never persist secret images (no saved PDF of secrets); full audit of every render plus a revocation path; printed output forbidden for trunk material. Producer side lives in glp_quick; declared areas: cert-trust, glp_quick, hub-display, provisioning. Pairs with olamnit-assistant android-quick-link-endpoints as consumer."

## Security Posture (mandatory, first-class scope)

This feature handles credential material for public infrastructure. The following posture is a
**precondition of the feature, not a follow-up** (engineer correction 2026-07-08), and no
requirement below may be waived, deferred, or time-boxed away:

1. The shared trunk cert/private-key (pfx) is long-lived, unchangeable TRUNK credential material.
   **It is never rendered** — not as a QR code, not in a PDF, not on a display page, not in a log.
2. What is provisioned to a new device is **short-lived, per-device, revocable derived credential
   material**, never the trunk secret itself.
3. Every secret-bearing QR payload is **encrypted**; the decryption key travels out-of-band
   (one-time passphrase or equivalent), never alongside the QR.
4. Secret-bearing images are **never persisted** — no saved PDF, file, cache, or screenshotable
   artifact containing secret payloads is written by the producer.
5. **Printed output is forbidden for trunk material** and for any secret-bearing payload; the PDF
   path carries non-secret material only.
6. Every render, issuance, and revocation is **audited**, and a working **revocation path** exists
   for every credential issued through this feature.

## Clarifications

### Session 2026-08-04

Answers adopted as delegated defaults: the engineer re-issued the full pipeline chain command
mid-clarify (2026-08-04), delegating the open questions to the suggested defaults below. Each is
vetoable at analyze/review; a veto reopens the affected sections.

- Q: How do mesh hosts accept a derived credential while 036 manual-pin trunk trust stays
  unchanged? → A: Short-lived per-device certificates signed by the trunk key — every existing
  host verifies against the already-pinned trunk with no new distribution channel; revocation is
  enforced by a hub-checked revocation list at the join seam. (delegated default)
- Q: Does revocation propagate mesh-wide or bind at the join seam? → A: Join/rejoin-seam
  enforcement only — an already-connected revoked device is cut at its next
  reconnect/re-authentication, not force-disconnected mesh-wide. (delegated default)
- Q: What binds a derived credential to a device? → A: A device-generated keypair fingerprint
  captured at provisioning, plus a human-entered device label at session start; audit records
  carry both. (delegated default)
- Q: Default validity/enforcement values? → A: Provisioning session window 10 minutes; derived
  credential TTL 30 days; revocation enforcement at the join seam within 60 seconds — all three
  are engineer-configurable (registered as key configurable items). (delegated default)
- Q: Is the printable non-secret PDF story in scope? → A: Yes, kept at P4, strictly non-secret
  material only. (delegated default)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One-scan desktop host onboarding via hub display page (Priority: P1)

An engineer stands up a new desktop host that must join the existing glp-quick QUIC+WS mesh.
Instead of hand-copying the cert directory and typing endpoint parameters, the engineer opens the
hub display page on an already-provisioned machine, which shows a provisioning session: one or
more QR codes carrying the link endpoint (address/port/SPKI pin) and an encrypted, short-lived,
device-scoped derived credential. The new host scans the codes (or captures them with any camera
and feeds the images to its provisioning intake), supplies the one-time passphrase received
out-of-band, assembles and verifies the bundle, and joins the mesh.

**Why this priority**: This is the core value — it removes the exact cert-copy bottleneck that
blocked 049 US3 twice on real hardware, and it exercises the full mandatory security chain
(derived credential, encryption, display-only rendering, audit). Everything else builds on it.

**Independent Test**: On a two-host setup with an established mesh, provision a fresh second host
using only the hub display page and a spoken/typed one-time passphrase; verify the host joins the
mesh and that no cert files were copied by hand and no trunk key material appeared in any
rendered output.

**Acceptance Scenarios**:

1. **Given** an established mesh hub and a new unprovisioned host, **When** the engineer starts a
   provisioning session and the new host scans all displayed QR codes and enters the correct
   one-time passphrase, **Then** the new host acquires endpoint + pin + a device-scoped derived
   credential, joins the mesh, and the session is recorded in the audit trail.
2. **Given** a provisioning session is displayed, **When** the session's validity window expires
   before scanning completes, **Then** the displayed payload becomes unusable, the expiry is
   audited, and the engineer can start a fresh session.
3. **Given** a scanned encrypted payload, **When** an incorrect one-time passphrase is supplied,
   **Then** decryption fails cleanly with an actionable error and no partial credential material
   is retained on the scanning device.
4. **Given** any provisioning render (display page or PDF), **When** its content is inspected,
   **Then** it contains no trunk private-key material in any encoding.

---

### User Story 2 - Credential lifecycle: audit and revocation (Priority: P2)

An operator reviews what has been provisioned and cuts off a device. Every render of a
provisioning payload, every credential issuance, and every revocation is recorded with actor,
device identity, timestamp, and outcome. When a device is lost, compromised, or retired, the
operator revokes its derived credential; the revoked device can no longer join or rejoin the
mesh, while all other devices are unaffected.

**Why this priority**: The mandatory posture makes audit + revocation preconditions of shipping
the feature at all. They are second only to the core flow because they gate every issuance the
core flow performs.

**Independent Test**: Provision a device (US1 flow), verify audit rows exist for the render and
issuance; revoke the device's credential; verify the device's next join attempt is rejected and
the rejection plus the revocation are audited; verify a second, non-revoked device still joins.

**Acceptance Scenarios**:

1. **Given** any completed or expired provisioning session, **When** the operator queries the
   audit record, **Then** every render, issuance, expiry, and revocation event appears with
   actor, device, timestamp, and outcome.
2. **Given** a provisioned device with a live derived credential, **When** the operator revokes
   it, **Then** subsequent join attempts by that device are rejected with an auditable refusal,
   within one enforcement interval of the revocation.
3. **Given** a revoked device, **When** the engineer intentionally re-provisions it through a new
   session, **Then** it receives a fresh derived credential and joins normally — revocation is
   per-credential, not a permanent device ban.
4. **Given** a derived credential past its validity window, **When** the device attempts to join,
   **Then** the join is refused with an expiry-specific, actionable error and a re-provisioning
   hint.

---

### User Story 3 - Android consumer contract (payload schema + assembly) (Priority: P3)

A phone or tablet — which has no file-copy channel at all — becomes provisionable. The producer
publishes a versioned payload contract: QR chunk format, integrity checks, assembly rules,
encryption envelope, and decode test vectors. The Android consumer (olamnit-assistant
`android-quick-link-endpoints`) implements the scanning side against this contract; conformance
is verifiable from the published test vectors alone, without access to a live mesh.

**Why this priority**: Unblocking device onboarding is a stated motivation, but the consumer
implementation lives in a different repository; this feature's deliverable is the contract and
producer-side conformance, which must be stable before the consumer work can proceed.

**Independent Test**: Using only the published contract and test vectors, decode and assemble the
sample bundles (including a multi-chunk one) to byte-identical plaintext; verify a deliberately
corrupted chunk is detected and rejected.

**Acceptance Scenarios**:

1. **Given** the published contract and its test vectors, **When** a consumer implementation
   decodes them, **Then** every valid vector assembles to the expected bundle and every invalid
   vector (corrupt chunk, missing chunk, wrong-version header) is rejected as specified.
2. **Given** a bundle larger than a single QR code's capacity, **When** the producer renders it,
   **Then** it is split into multiple QR chunks, each self-identifying (index/total/bundle id)
   and integrity-checked, and assembly succeeds regardless of scan order.
3. **Given** a payload-format revision, **When** the producer emits the new version, **Then** the
   version is explicit in every chunk header and an older consumer fails fast with a
   version-mismatch error rather than misparsing.

---

### User Story 4 - Printable non-secret hand-off (generated PDF) (Priority: P4)

For environments where the hub display page cannot be shown to the joining device (no shared
screen, remote hand-off), the engineer generates a PDF carrying **non-secret** provisioning
material only: link endpoint, SPKI pin, provisioning instructions, and a session reference. The
secret-bearing part of the bundle is never in the PDF; the device completes provisioning by
combining the printed non-secret material with a display-only or out-of-band secret channel.

**Why this priority**: A convenience path. Valuable for awkward hand-offs, but the display-page
flow (US1) already covers the primary need, and the posture strictly bounds what a PDF may carry.

**Independent Test**: Generate the PDF for a session; verify it renders the endpoint/pin/
instructions, contains no secret payload in any encoding, and that its generation event is
audited.

**Acceptance Scenarios**:

1. **Given** a provisioning session, **When** the engineer requests a PDF, **Then** the generated
   document contains endpoint, SPKI pin, instructions, and session reference — and no derived
   credential, no encrypted secret payload, and no trunk material.
2. **Given** a request to include secret material in a PDF (by any option or path), **When** the
   producer processes it, **Then** it refuses with a posture-citing error, and the refusal is
   audited.

---

### Edge Cases

- A QR chunk is missed or scanned twice: assembly must detect the gap/duplicate via chunk
  headers and report exactly which chunks are missing; scan order must not matter.
- The bundle exceeds the capacity of a reasonable number of QR codes: the producer must bound
  chunk count, and refuse with a clear error rather than emit an unscannable wall of codes.
- A bystander photographs the hub display page: the payload is encrypted and the session
  short-lived, so the photograph alone must be useless without the out-of-band passphrase and
  within-window redemption.
- The same provisioning session is redeemed twice (replay): a session/credential must be
  single-redemption; a second redemption attempt is refused and audited.
- Clock skew between hub and joining device: validity-window enforcement must tolerate skew
  within a declared bound and fail with an actionable skew error beyond it.
- The hub process crashes mid-session: no secret image or partial secret artifact may remain on
  disk; on restart the interrupted session is expired and audited as such.
- A revoked or expired credential is presented: refusal must be specific (revoked vs expired),
  audited, and must not leak whether other credentials exist.
- The scanning device aborts mid-flow: partial bundle material on the device must be discardable
  without security consequence (secrets remain encrypted until final passphrase entry).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The producer MUST render a provisioning bundle — link endpoint (address, port),
  SPKI pin, and a device-scoped derived credential — as one or more QR codes, chunked to QR
  capacity, with per-chunk integrity checks and a whole-bundle check.
- **FR-002**: The producer MUST NEVER include trunk private-key material (the shared pfx/private
  key) in any rendered output — QR, PDF, display page, log, or audit record — in any encoding;
  any path that would do so MUST refuse with a posture-citing error.
- **FR-003**: Provisioned credential material MUST be short-lived (bounded validity window,
  default 30 days, configurable), per-device (bound to a device-generated keypair fingerprint
  captured at provisioning plus a human-entered device label), and revocable, derived from — but
  never equal to — the trunk material.
- **FR-004**: Every secret-bearing QR payload MUST be encrypted; the decryption secret (one-time
  passphrase or out-of-band key) MUST travel on a separate channel from the QR imagery, and a
  wrong decryption secret MUST fail cleanly without exposing partial plaintext.
- **FR-005**: The hub display page MUST render provisioning sessions display-only: sessions have
  an explicit validity window (default 10 minutes, configurable), expire visibly, and no
  secret-bearing image is persisted to disk, cache, or export by the producer.
- **FR-006**: The generated PDF MUST carry non-secret material only (endpoint, SPKI pin,
  instructions, session reference); requests to embed secret material in printable/persistable
  output MUST be refused and audited.
- **FR-007**: Multi-QR chunking MUST make each chunk self-identifying (bundle id, index, total,
  format version) so that assembly is order-independent, detects missing/duplicate/corrupt
  chunks, and names the missing pieces on failure.
- **FR-008**: Every render, issuance, redemption, expiry, refusal, and revocation MUST produce an
  audit record (actor, device identity, timestamp, event kind, outcome) queryable by the
  operator.
- **FR-009**: The operator MUST be able to revoke an issued derived credential; a revoked
  credential MUST be refused at join/rejoin within one declared enforcement interval (default 60
  seconds at the join seam, configurable), without affecting other devices, and revocation MUST
  NOT require changing the trunk material.
- **FR-010**: A provisioning session MUST be single-redemption: after a successful redemption (or
  expiry) the session's payload MUST be unusable, and repeat redemption attempts MUST be refused
  and audited.
- **FR-011**: The payload contract — chunk format, integrity checks, encryption envelope,
  assembly rules, format versioning — MUST be published as a versioned specification with decode
  test vectors sufficient for an independent consumer (olamnit-assistant
  android-quick-link-endpoints) to implement and verify without a live mesh.
- **FR-012**: The 036 manual-pin shared-cert trust model MUST remain unchanged for existing
  endpoints: already-provisioned hosts keep working with no migration, and the feature adds a
  provisioning path without altering how the mesh's trunk trust is anchored.
- **FR-013**: Joins with expired credentials MUST fail with an expiry-specific, actionable error
  distinct from revocation refusals, including a re-provisioning hint.

### Key Entities

- **Provisioning Session**: One engineer-initiated act of provisioning one device; has a validity
  window, single-redemption state, a rendered form (display page and/or non-secret PDF), and an
  audit trail.
- **Provisioning Bundle**: The material a joining device needs: link endpoint (address/port),
  SPKI pin, encrypted derived credential; exists only transiently in rendered form.
- **Derived Credential**: Short-lived, device-scoped, revocable credential material derived from
  trunk trust; the only secret ever provisioned; carries validity window and device binding.
- **QR Chunk Set**: The ordered set of QR codes encoding one bundle; each chunk carries bundle
  id, index/total, format version, and an integrity check.
- **Audit Record**: Immutable event row for render/issuance/redemption/expiry/refusal/revocation
  with actor, device, timestamp, outcome.
- **Revocation Record**: Operator action binding a derived credential to a revoked state, with
  enforcement deadline and audit linkage.
- **Payload Contract**: The versioned published specification (with test vectors) that producers
  and consumers implement against.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An engineer provisions a new desktop host into an existing mesh in under 5 minutes
  end-to-end, with zero manual credential-file copying and zero out-of-band file transfer.
- **SC-002**: A device with no file-copy channel (phone/tablet class) can acquire the complete
  trust bundle by scanning alone plus one out-of-band passphrase; the consumer contract's test
  vectors are sufficient for an independent implementation to prove decode conformance with no
  live mesh access.
- **SC-003**: Zero rendered outputs (QR, display page, PDF, logs, audit) contain trunk
  private-key material — demonstrated by negative tests on every render path and by inspection
  tooling over produced artifacts.
- **SC-004**: 100% of renders, issuances, redemptions, expiries, refusals, and revocations
  produce audit records; an operator can answer "which devices were provisioned, when, by whom,
  and which are revoked" from the audit surface alone.
- **SC-005**: A revoked credential is refused at join within one declared enforcement interval in
  100% of attempts; a photographed/copied QR payload is unusable without the out-of-band secret
  and after session expiry in 100% of attempts.
- **SC-006**: 049-US3-class onboarding friction is eliminated: the two failure modes recorded
  there (cert absent on second host; credential-walled file share) cannot recur, because the
  provisioning path requires no file share and no pre-placed cert.

## Assumptions

- The hub display page is reachable/visible to the person provisioning (shared screen or same
  room); the joining device has a camera or can ingest captured QR images.
- The one-time passphrase travels by an existing human channel (spoken, typed, messaged) and is
  out of scope to transport programmatically.
- Per-device derived credentials are short-lived per-device certificates signed by the trunk key
  (Clarifications 2026-08-04): existing endpoints verify them against the already-pinned trunk,
  so 036 trust anchoring is unchanged; detailed certificate profile and issuance mechanics are
  planning-stage decisions bounded by FR-003/FR-009/FR-012.
- Revocation enforcement is anchored at the mesh's join/accept seam via a hub-checked revocation
  list (Clarifications 2026-08-04); an already-connected revoked device is cut at its next
  reconnect/re-authentication — mesh-wide push of revocation state is out of scope.
- The operator initiating a provisioning session at the hub console is authorized to do so under
  the existing operational model; no new operator-identity system is introduced, but the audit
  record captures the acting identity as known to the host.
- Producer-side work lives in the glp_quick area (declared areas: cert-trust, glp_quick,
  hub-display, provisioning); the Android consumer implementation is a separate repository's
  feature and consumes only the published contract.
- PDF output is a convenience for non-secret material only; environments needing fully offline
  secret hand-off are out of scope for this feature.
