# Join-Seam Contract — derived-credential acceptance + revocation (067)

**Scope**: `csharp/glp_link/transports/QuicTransport.cs` (pin validation callback) + new
`csharp/glp_link/transports/DerivedCredentialValidator.cs`. `glp_quick_host` mesh accept loop
gains only the new `ERR` tokens. Loaders remain fail-closed — no degraded/no-pin mode exists.

## Acceptance decision (evaluated in the TLS validation callback, both roles)

Accept the presented peer certificate iff exactly one of:

1. **Trunk identity (existing, unchanged)**: constant-time equality
   `SpkiPin(presented) == trunk_pin`.
2. **Derived identity (new)**: ALL of
   a. `presented.Issuer` signature verifies against the trunk certificate's public key
      (raw signature check — NOT name-based X509 chain building, no CA/hostname semantics);
   b. `now ∈ [NotBefore, NotAfter]` (clock-skew tolerance ±90 s, declared bound per spec edge
      case; beyond it → `cert_expired` with skew detail);
   c. `SpkiPin(presented)` ∉ revocation set.

Any other certificate → `cert_mismatch` (existing semantics preserved).

## Revocation set

- Source: `glpquick-cert/provision/revoked.jsonl` (append-only; one JSON object per line with
  `fingerprint`).
- Load: at transport construction; reload when file mtime changes, checked on every accept and
  at minimum every 10 s while listening — enforcement latency ≤ 60 s guaranteed with margin
  (FR-009 default).
- Missing file = empty set (nothing ever issued/revoked) — NOT an error; unreadable/corrupt
  file = fail-closed: derived-path acceptance disabled (`cert_revoked` refusals name
  `revocation_list_unreadable`), trunk-identity path unaffected.

## Single-redemption / replay (FR-010, R-010)

The mesh server tracks live fingerprints: a join presenting a derived fingerprint that already
has a live connection from a different remote endpoint → refuse `session_replayed`, audit via
stdout event line (picked up by the Python supervisor). First-join observation is reported as
an event line `PROVISION_REDEEMED <fingerprint>` so the Python session marks `redeemed`.

## Refusal tokens (host stdout, `ERR <token>` style — FR-013)

| Token | Condition | Notes |
|---|---|---|
| `cert_mismatch` | neither trunk nor valid derived | existing exit mapping unchanged |
| `cert_expired` | 2b fails | distinct from revocation; message hints re-provisioning |
| `cert_revoked` | 2c fails | includes `revocation_list_unreadable` variant detail |
| `session_replayed` | live-fingerprint conflict | audited |

## Test matrix (`csharp/glp_link.tests/DerivedCredentialTests.cs`)

trunk accepted · derived-valid accepted · derived-expired refused(`cert_expired`) ·
derived-not-yet-valid refused · derived-revoked refused(`cert_revoked`) · revoked-mid-listen
enforced ≤ 60 s · unsigned/self-signed refused(`cert_mismatch`) · corrupt revocation file ⇒
derived path fail-closed, trunk path still accepted · replay refused(`session_replayed`) ·
negative: no render path ever receives trunk key bytes (SC-003 negative test hook).
