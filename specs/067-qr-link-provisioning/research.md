# Research: QR-code link + cert provisioning (067)

**Date**: 2026-08-04 · **Input**: spec.md (clarified), producer-area code scan (glp_quick /
glp_quick_host / glp_link / 036 / 049 evidence)

## R-001 — Where the producer lives

- **Decision**: All producer/lifecycle logic in the Python `glp_quick` package (new
  `provision/` subpackage + a `provision` typer sub-app); the C# side changes only at the
  acceptance seam.
- **Rationale**: The trunk material is already generated and loaded by
  `glp_quick/src/glp_quick/cert.py` (shared self-signed cert + SPKI pin math via
  `cryptography`); the CLI, cert-dir validation, and the only display surface (terminal pages)
  are Python. `csharp/glp_quick_host` is a stdio console exe with no HTTP/display surface.
- **Alternatives considered**: (a) C#-side producer — rejected: would duplicate cert tooling
  and add a display surface to a stdio host; (b) new web service for the hub page — rejected:
  new attack surface + new dependency for a LAN flow the terminal page already serves.

## R-002 — Derived-credential acceptance mechanism (hardest dependency)

- **Decision**: Extend `QuicTransport.cs`'s `PinValidationCallback` to accept EITHER (existing)
  a peer cert whose SPKI pin equals the trunk pin, OR (new) a certificate that (1) is signed by
  the trunk certificate (signature verified against the pinned trunk SPKI — not a name/CA
  chain), (2) is within its validity window, and (3) whose SPKI fingerprint is absent from the
  hub's revocation list. Checks (1)-(3) live in a new `DerivedCredentialValidator` used by the
  callback; refusals surface as `ERR cert_revoked` / `ERR cert_expired` / existing
  `ERR cert_mismatch` so `stacks/csharp.py` exit-code mapping keeps working.
- **Rationale**: Today both ends must present the ONE shared cert with its private key
  (`ClientCertificateRequired=true` + mutual pin) — exactly the friction 049 recorded. A
  trunk-signed derived cert lets every existing host verify a newcomer against the
  already-pinned trunk with no new distribution channel (clarify Q1), and keeps the pin — not
  names/CAs — as the sole trust anchor (036 FR-003 unchanged for existing endpoints, FR-012).
- **Alternatives considered**: (a) hub-side join registry with the shared cert still required —
  rejected: doesn't remove the key-copy bottleneck; (b) one-time token redeemed for the shared
  pfx — rejected: still ships trunk material to devices, violating the posture; (c) macaroon
  gate extension only — rejected: `MacaroonLinkGate` gates the 050 native path, not the
  quick-host TLS handshake where identity is established.

## R-003 — Who mints the device keypair (spec refinement)

- **Decision**: The hub mints the device keypair AND its trunk-signed certificate inside the
  provisioning session; the private key exists hub-side only in memory, leaves only inside the
  encrypted QR payload, and is never written to disk. The audited "device-generated keypair
  fingerprint" of the clarify session is realised as the SPKI fingerprint of the minted device
  cert, computed and recorded at mint time.
- **Rationale**: A true device-generated keypair requires the device to deliver a CSR to the
  hub before any channel exists — the exact chicken-and-egg this feature removes. One-scan
  (US1/SC-001) is only achievable if the credential travels in the QR. TTL-bounded, per-device,
  revocable material bounds the exposure; the passphrase-encrypted envelope bounds the render.
- **Alternatives considered**: two-phase enrollment (QR carries endpoint+token, device connects
  provisionally and submits CSR) — rejected for v1 as it requires an unauthenticated
  provisional accept mode in the transport (a degraded path, forbidden by the fail-closed
  loader/no-degraded-mode constraint); noted as a possible future hardening step.
- **Spec impact**: refines the Clarifications bullet on device binding; flagged for the analyze
  stage to confirm (delegated-default regime).

## R-004 — QR rendering without persisting secret images

- **Decision**: `segno` (pure Python, zero deps) rendering to the terminal (ANSI/Unicode block
  output) inside an existing `terminal/pages.py` page. No image file is ever produced for
  secret-bearing payloads; there is no code path that writes a secret QR to disk.
- **Rationale**: Satisfies FR-005 "no secret-bearing image is persisted" by construction rather
  than by cleanup; terminal pages are the existing display surface; scanning an on-screen code
  works for phone cameras and for capture-and-feed intake alike.
- **Alternatives considered**: `qrcode` + PIL — rejected: PIL dependency and image-file
  orientation; HTML page — rejected per R-001.

## R-005 — Encryption envelope + one-time passphrase

- **Decision**: scrypt KDF (N=2^15, r=8, p=1) over a generated 6-word one-time passphrase →
  AES-256-GCM over the bundle plaintext; nonce + KDF salt travel in the chunk-0 header; the
  passphrase travels only out-of-band (spoken/typed) and is displayed once, hub-side, next to
  the QR page. Both primitives from `cryptography`, already a dependency.
- **Rationale**: FR-004 requires the imagery to be useless without the out-of-band secret; GCM
  gives integrity + confidentiality; scrypt hardens the short passphrase against offline
  brute-force of a photographed code within the 10-minute session window.
- **Alternatives considered**: `age`-style X25519 — rejected: needs a recipient key the device
  doesn't have yet; plain base64 with no encryption — forbidden by FR-004.

## R-006 — Chunking + integrity

- **Decision**: Bundle bytes → base64url → split into ≤ 8 chunks sized for QR version 25 /
  error-correction M; each chunk carries a fixed header `GQP1|<bundle_id8>|<index>/<total>|<crc32>`
  and the final chunk additionally carries the whole-bundle SHA-256 (truncated 16 bytes).
  Assembly is order-independent; missing/duplicate/corrupt chunks are named by index (FR-007).
- **Rationale**: Deterministic, versioned (`GQP1`), consumer-implementable from the contract
  alone; version-25/M keeps each code comfortably phone-scannable.
- **Alternatives considered**: fountain codes (Luby transform, `txqr`-style) — rejected: v1
  bundles fit in ≤ 8 static chunks; complexity unjustified.

## R-007 — PDF path (non-secret only)

- **Decision**: `fpdf2` generates a single page carrying endpoint address/port, trunk SPKI pin
  string, session reference, and join instructions — never any derived credential or encrypted
  secret payload; the generator API takes only non-secret fields so embedding a secret is a
  type-level impossibility, and any request routed toward secret content refuses with a
  posture-citing error (FR-006).
- **Rationale**: Spec P4 story kept in scope (clarify Q5); fpdf2 is pure-Python and tiny.
- **Alternatives considered**: reportlab — heavier, no benefit; printing the display page —
  forbidden for secret-bearing renders.

## R-008 — Lifecycle stores (audit / revocation / issued index)

- **Decision**: Three append-only JSONL files under `glpquick-cert/provision/` (gitignored with
  the trunk material): `audit.jsonl` (every render/issue/redeem/expire/refuse/revoke event,
  FR-008), `revoked.jsonl` (fingerprint + revoked_at + actor), `issued.jsonl` (fingerprint,
  label, TTL window, session id). The C# validator re-reads `revoked.jsonl` on an mtime change,
  re-checked per accept — worst-case enforcement latency well under the 60 s default (FR-009).
- **Rationale**: Constitution VI-b forbids a second working-data cluster; flat append-only
  files match the additive-persistence principle, survive crashes (no partial-write secret
  risk — no secrets are in these files, only fingerprints/labels/timestamps), and are trivially
  auditable.
- **Alternatives considered**: PGLite table in `.pgdb` — rejected: couples mesh trust lifecycle
  to the codeconv working-data cluster and its bridge; CRL/X.509 OCSP machinery — massive
  overkill for a LAN mesh with a hub-checked list.

## R-009 — Join intake on the new host

- **Decision**: `glp-quick provision join` accepts chunk text payloads (pasted, or decoded from
  captured images via any external scanner app — image decoding itself is out of scope for v1),
  prompts for the one-time passphrase, verifies + decrypts, and writes
  `glpquick-derived/` (device cert + key + trunk pin + endpoint) which the client role loads;
  partial intake is discardable at any point (secrets stay encrypted until final decrypt).
- **Rationale**: Keeps v1 free of camera dependencies on desktop; Android consumer implements
  scan-side per the published contract (US3); matches the spec edge case "scanning device
  aborts mid-flow".
- **Alternatives considered**: bundling a camera/zbar dependency — rejected for v1 scope.

## R-010 — Single-redemption + session expiry

- **Decision**: A session (default window 10 min) is redeemed when the joining device first
  completes a successful mesh join with the session's derived credential (observed at the hub
  accept seam and recorded in audit); the derived cert stays valid for its TTL, but the
  provisioning payload/session cannot be redeemed a second time — a second join attempt with
  the same credential from a different transport endpoint while the first is live is refused
  (`ERR session_replayed`) and audited (FR-010, replay edge case).
- **Rationale**: The photograph-replay threat is bounded by passphrase + window + single
  redemption; enforcement at the accept seam reuses the revocation-list plumbing.
- **Alternatives considered**: hardware attestation — out of scope for the 036 trust model.
