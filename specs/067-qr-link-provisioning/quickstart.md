# Quickstart: QR-code link + cert provisioning (067)

End-to-end walkthrough once implemented (mirrors US1/US2 acceptance).

## Provision a new desktop host (US1)

```bash
# Hub (already-provisioned host, holds glpquick-cert/)
glp-quick provision session --label "gavri-laptop"
#  → terminal page shows ≤8 QR codes + one-time passphrase (displayed once)
#  → speak/type the passphrase to the person at the new host

# New host: capture the QR contents (any scanner app → text lines), then
glp-quick provision join --input chunks.txt
#  → prompts for passphrase → writes glpquick-derived/

# New host joins the mesh with derived material
glp-quick --client --derived-dir glpquick-derived
#  → hub session flips to 'redeemed'; audit rows: render, issue, redeem
```

## Verify the posture (US1-4 / SC-003)

```bash
glp-quick provision audit --json          # every event, no key material anywhere
grep -r "BEGIN.*PRIVATE" glpquick-cert/provision/  # → no hits (stores hold fingerprints only)
```

## Revoke and confirm enforcement (US2)

```bash
glp-quick provision revoke --fingerprint <spki-b64> --reason "device lost"
# within ≤60s any join with that credential → ERR cert_revoked; audit row 'revoke' + 'refuse'
```

## Non-secret PDF hand-off (US4)

```bash
glp-quick provision pdf --session <id> --out handoff.pdf   # endpoint+pin+instructions only
```

## Consumer conformance without a mesh (US3)

Decode `glp_quick/tests/vectors/provision/*.json` per contracts/payload-contract.md — all valid
vectors assemble byte-identically; every invalid vector yields its named error.

## Test suites

```bash
cd glp_quick && .venv/Scripts/python -m pytest tests/unit tests/integration -q
dotnet test csharp/glp_link.tests -c Release
```
