# Data Model: QR-code link + cert provisioning (067)

**Date**: 2026-08-04 · **Sources**: spec.md Key Entities, research.md R-002/R-005/R-006/R-008/R-010

## ProvisioningSession

One engineer-initiated act of provisioning one device (hub-side, in-memory + audit rows).

| Field | Type | Notes |
|---|---|---|
| session_id | str (8-char base32) | printed on display page + non-secret PDF |
| device_label | str | human-entered at session start (clarify Q3) |
| created_at / expires_at | UTC timestamps | window default 10 min, configurable |
| state | enum | `open → rendered → redeemed \| expired \| aborted` |
| passphrase | 6-word one-time | generated hub-side, displayed once, never persisted |
| bundle | ProvisioningBundle | built at `rendered` |
| minted_fingerprint | str (SPKI b64) | the derived credential this session issued |

State transitions: `open→rendered` (QR page shown) · `rendered→redeemed` (first successful join
observed, single-redemption per R-010) · `rendered→expired` (window lapse) · any→`aborted`
(operator cancel / hub crash recovery marks interrupted sessions expired). Every transition
emits an AuditRecord.

## ProvisioningBundle (payload plaintext)

Exists only transiently: hub memory → encrypted envelope → device memory → `glpquick-derived/`.

| Field | Type | Notes |
|---|---|---|
| v | int (1) | payload format version (`GQP1`) |
| endpoint | {host, port} | link endpoint |
| trunk_spki_pin | str | base64 SHA-256 SPKI of trunk cert (same math as cert.py) |
| device_cert_pem | str | trunk-signed, TTL-bounded (default 30 days) |
| device_key_pem | str | minted per R-003; never persisted hub-side |
| session_id | str | audit linkage |

## Encryption Envelope (chunk transport form)

scrypt(N=2^15,r=8,p=1, salt 16B) over one-time passphrase → AES-256-GCM(nonce 12B).
Chunk-0 header carries {salt, nonce}; ciphertext+tag spread across chunks.

## QRChunk / QRChunkSet

| Field | Type | Notes |
|---|---|---|
| header | `GQP1\|<bundle_id8>\|<index>/<total>\|<crc32>` | fixed, versioned (R-006) |
| body | base64url slice | sized for QR v25 / EC-M |
| bundle_sha256_16 | bytes16, final chunk only | whole-bundle integrity |

Constraints: total ≤ 8 (refuse above bound, FR-007 edge case); assembly order-independent;
missing/duplicate/corrupt chunks named by index.

## DerivedCredential

| Field | Type | Notes |
|---|---|---|
| fingerprint | str (SPKI b64) | primary identity, audited + revocable |
| device_label | str | from session |
| not_before / not_after | UTC | TTL default 30 days, configurable |
| issuer | trunk cert | signature verifiable against pinned trunk SPKI (R-002) |
| state | derived | `live \| expired (clock) \| revoked (revoked.jsonl membership)` |

## AuditRecord (`glpquick-cert/provision/audit.jsonl`, append-only)

| Field | Type |
|---|---|
| ts | UTC ISO |
| event | `render \| issue \| redeem \| expire \| refuse \| revoke \| pdf_render` |
| actor | OS user on hub |
| session_id | str |
| fingerprint | str or null |
| device_label | str or null |
| outcome | `ok \| refused:<reason>` |

Never contains key material, passphrases, or payload bytes (FR-002 extends to audit rows).

## RevocationRecord (`glpquick-cert/provision/revoked.jsonl`, append-only)

| Field | Type |
|---|---|
| fingerprint | str (SPKI b64) |
| revoked_at | UTC ISO |
| actor | OS user |
| reason | free text (secret-redacted) |

Consumed by `DerivedCredentialValidator` (C#): reload on file mtime change, checked per accept —
enforcement ≤ 60 s default (FR-009).

## IssuedIndex (`glpquick-cert/provision/issued.jsonl`, append-only)

fingerprint, device_label, session_id, not_before, not_after — operator query surface backing
SC-004 ("which devices, when, by whom, which revoked" joined with revoked.jsonl).

## Relationships

Session 1—1 Bundle 1—1 DerivedCredential; Session 1—N AuditRecords;
DerivedCredential 0—1 RevocationRecord; PayloadContract (contracts/payload-contract.md)
versions Bundle+Envelope+Chunk wire forms and publishes test vectors
(`glp_quick/tests/vectors/provision/`).
