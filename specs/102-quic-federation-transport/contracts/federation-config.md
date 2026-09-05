<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract — Federation configuration and reversibility

**Satisfies**: FR-002, FR-003, FR-004, FR-024, FR-025, FR-026
**Verified by**: SC-008, SC-009, SC-013

---

## G1 — Location and shape

Per-host, **outside the repo** (it carries host-specific addresses and a key reference):

```
%LOCALAPPDATA%\ynet\federation\config.json      # Windows
$XDG_CONFIG_HOME/ynet/federation/config.json    # Linux
```

```json
{
  "enabled": false,
  "bind_address": "0.0.0.0",
  "bind_port": 47890,
  "space_id": "ynet-epoch-2026-09",
  "identity_path": "%LOCALAPPDATA%\\ynet\\federation\\node.key",
  "push_on_append": true,
  "pull_interval_seconds": 60,
  "peers": [
    {
      "name": "olamnit",
      "node_id": "<64-hex>",
      "endpoints": ["192.168.0.136:47890", "192.168.0.129:47890"]
    }
  ]
}
```

**Defaults are the safe state**: `enabled=false`, `peers=[]`. A host that has never been configured
federates with nobody and serves its lanes normally (FR-004).

## G2 — Changeable without rebuilding, and readable back

FR-002. Two verbs, and the second is not optional:

```
ynet-federation config show          # prints the EFFECTIVE config, after defaults and validation
ynet-federation config set <k> <v>   # writes, then re-reads and prints what was actually stored
```

Write-only configuration cannot be verified and therefore cannot be trusted. `config show` prints the
**effective** values — the ones the service will use — not the file's literal text, because the gap
between the two is where configuration bugs live.

## G3 — Validation refuses loudly

| Condition | Result |
|---|---|
| `bind_address` is a loopback address while `enabled=true` | **refuse** — "loopback bind is not peer-reachable" (FR-001) |
| a peer endpoint is a hostname rather than a literal address | **warn and record** — names resolve to `fe80::` only on this estate (FR-003) |
| `space_id` empty while `enabled=true` | **refuse** — an unminted space cannot order anything (FR-026) |
| `space_id` looks clock-derived (all digits, 6+ chars) | **refuse** — this is how the fossil was born (FR-015) |
| duplicate `node_id` across peers | **refuse** — one participant, one entry (FR-007) |
| same address under two `node_id`s | **allow** — addresses are not identity (I-21) |

A refusal names the field and the reason. "Invalid config" is not a reason.

## G4 — Identity persistence

FR-007. `identity_path` holds the persisted node key. On first run the service **mints and persists**
one, prints its `node_id`, and that value is what gets published to peers.

**🔴** `QuicLinkTransport.CreateDevCert()` mints a **fresh** cert per call. A pin taken from a probe
run is **ephemeral** and MUST NOT be published to peers as stable. This is the whole reason
`NodeIdentityStore` exists.

Key file permissions are owner-only. The key is never logged, never printed, and never included in
`config show` — only the derived `node_id` is.

## G5 — Every change is reversible, and the reversal is recorded

FR-024, FR-025, SC-009. Enabling federation touches exactly three things on a host. Each is recorded
with its reversal **beside it**, as data, in `%LOCALAPPDATA%\ynet\federation\changes.jsonl`:

| # | Change | Reversal |
|---|---|---|
| 1 | write `config.json` with `enabled=true` | restore the recorded prior file (kept verbatim) |
| 2 | mint + persist `node.key` | delete the key file (a new one is minted on next run) |
| 3 | inbound firewall rule, **Private profile**, `192.168.0.0/24`, UDP/47890 | `Remove-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890'` |

```powershell
New-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890' -Direction Inbound `
  -Action Allow -Protocol UDP -LocalPort 47890 -Profile Private `
  -RemoteAddress 192.168.0.0/24 -Enabled True
```

**FR-024**: scoped to the local network and the single federation port. **No host protection is
disabled** — Smart App Control stays on; turning it off was declined as one-way by ruling
`Q-GLPNETG27-02`.

**🔴 Needs elevation.** `New-NetFirewallRule` returns `Access is denied` from an unelevated shell and
this lane cannot self-elevate. It gates **inbound** dials only; outbound dialling, both loopback
ends, the fold, the term rule and the status surface are all testable without it.

**SC-009 test**: apply all three changes, run the recorded reversals, assert the host is
byte-identically back to its prior state (config file restored, key absent, rule absent).

## G6 — Epoch minting is an operator action

FR-026, ruling `Q-GLPNETG28-01`.

```
ynet-federation epoch mint --rationale "<why>"
```

Records who, when and why; writes the new `space_id`; leaves every prior-epoch operation readable and
attributed (SC-013). It is **not** derived from a host identity and **not** from wall-clock time.
