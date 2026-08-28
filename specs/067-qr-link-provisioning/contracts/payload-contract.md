# Payload Contract GQP1 — QR provisioning bundle (067, FR-011)

**Status**: v1 (authoritative; the Android consumer `android-quick-link-endpoints` implements
against THIS document + the test vectors — no live mesh required).

## 1. Bundle plaintext (JSON, UTF-8)

```json
{
  "v": 1,
  "endpoint": {"host": "<string>", "port": <int>},
  "trunk_spki_pin": "<base64 SHA-256 of trunk cert DER SPKI>",
  "device_cert_pem": "<PEM>",
  "device_key_pem": "<PKCS8 PEM>",
  "session_id": "<8-char base32>"
}
```

## 2. Encryption envelope

- KDF: scrypt(passphrase_utf8, salt=16 random bytes, N=2^15, r=8, p=1, dkLen=32)
- Cipher: AES-256-GCM, nonce = 12 random bytes, AAD = ASCII `GQP1`
- Envelope bytes = `salt(16) || nonce(12) || ciphertext || tag(16)`
- Passphrase: 6 lowercase words joined by `-`, generated hub-side, transported ONLY out-of-band.
- Wrong passphrase ⇒ GCM tag failure ⇒ consumer MUST discard all buffered material and report
  `bad_passphrase`; no partial plaintext may be exposed (FR-004).

## 3. Chunking (QR transport form)

- `payload_b64 = base64url(envelope_bytes)` (no padding)
- Split into `total ≤ 8` slices sized for QR version 25 / error-correction M.
- Each QR encodes one line, ASCII: `GQP1|<bundle_id>|<index>/<total>|<crc32>|<slice>`
  - `bundle_id`: 8-char base32, identical across chunks
  - `index`: 1-based; `total`: chunk count
  - `crc32`: lowercase hex CRC-32 of `<slice>` ASCII bytes
- Final chunk (`index == total`) appends `|<sha16>` — lowercase hex of the first 16 bytes of
  SHA-256 over the complete `payload_b64` ASCII bytes.

## 4. Assembly rules (consumer MUSTs)

1. Accept chunks in ANY order; key by `(bundle_id, index)`.
2. Reject a chunk whose crc32 fails → report `corrupt_chunk:<index>`.
3. Duplicate `(bundle_id, index)` with identical bytes: idempotent; with different bytes:
   `conflicting_chunk:<index>`.
4. On assembly attempt with gaps: report exactly the missing indices (`missing:<i,j,...>`).
5. Verify `sha16` over the joined `payload_b64` before decrypting → `bundle_integrity` on fail.
6. Unknown leading tag (not `GQP1`) → `version_mismatch` — MUST NOT attempt best-effort parse.
7. After successful decrypt: validate JSON fields; `v != 1` → `version_mismatch`.

## 5. Test vectors (`glp_quick/tests/vectors/provision/`)

| File | Purpose |
|---|---|
| `v1_single_chunk.json` | 1-chunk bundle: chunks[], passphrase, expected plaintext |
| `v1_multi_chunk.json` | 5-chunk bundle; includes shuffled-order assembly expectation |
| `v1_corrupt_chunk.json` | chunk 3 crc-violated → expect `corrupt_chunk:3` |
| `v1_missing_chunk.json` | chunk 2 absent → expect `missing:2` |
| `v1_bad_version.json` | `GQP9` tag → expect `version_mismatch` |
| `v1_bad_passphrase.json` | wrong passphrase → expect `bad_passphrase`, no partial output |

Vector schema: `{"chunks": [...], "passphrase": "...", "expect": {"result": "...", "plaintext_b64": "..."}}`.
Vectors contain ONLY synthetic throwaway keys minted for the vectors — never real material.

## 6. Non-secret PDF page (bounded companion, FR-006)

Carries ONLY: endpoint host/port, trunk_spki_pin string, session_id, human instructions.
It is NOT part of this wire contract and MUST NOT carry any chunk of an encrypted envelope.
