# CLI Contract — `glp-quick provision` (067)

All subcommands validate the cert dir first (existing `_require_cert_dir` behaviour) and refuse
with the posture-citing error `provision_posture` on any path that would render/persist/print
trunk key material (FR-002).

## `glp-quick provision session --label <device-label> --addr <ip|name> --port <udp> [--window-min 10] [--ttl-days 30]`

`--addr`/`--port` name the link endpoint the joining device will connect to — they travel
inside the encrypted bundle (data-model.md `ProvisioningBundle.endpoint`; folded back from the
T026 loopback validation, which found them omitted here).

Mint device keypair + trunk-signed cert; build bundle; render chunked QR to the hub terminal
display page with the one-time passphrase shown alongside (once); block until redeemed /
expired / Ctrl-C (aborted). Audit: `render`, `issue`, then terminal event.
Exit: 0 redeemed · 3 expired · 4 aborted · 64 cert-dir/posture refusal.

## `glp-quick provision pdf --session <id> --out <path.pdf>`

Non-secret page only (endpoint, trunk pin, session id, instructions). Audit `pdf_render`.
Refuses (`provision_posture`, exit 64) any flag/path asking for secret content.

## `glp-quick provision revoke --fingerprint <spki-b64> [--reason <text>]`

Append RevocationRecord; idempotent re-revoke warns, exits 0. Audit `revoke`.

## `glp-quick provision audit [--since <iso>] [--json]`

Query audit.jsonl joined with issued/revoked indices (SC-004 operator answer). Read-only.

## `glp-quick provision join --input <chunks.txt|-> [--out-dir glpquick-derived]`

Consumer intake on the joining host: parse chunk lines (any order), report
missing/corrupt/conflicting per the payload contract, prompt for passphrase, decrypt, write
`glpquick-derived/{device.pem,device.key,trunk.pin,endpoint}`. Abort at any point leaves no
plaintext on disk. Exit: 0 ok · 5 `bad_passphrase` · 6 assembly error (message carries the
contract token) · 7 `version_mismatch`.

## Client role change

`glp-quick --client` (and `stacks/csharp.py`) gains `--derived-dir glpquick-derived`: the C#
host is launched with the derived cert/key instead of the shared pfx; trunk pin still verified
against the pinned trunk (unchanged for existing endpoints, FR-012).

## Refusal tokens (host `ERR <token>` surface, mapped by `stacks/csharp.py`)

`cert_mismatch` (existing) · `cert_expired` (new) · `cert_revoked` (new) ·
`session_replayed` (new). Each maps to a distinct non-zero exit and an actionable message
(expiry hints re-provisioning, FR-013).
