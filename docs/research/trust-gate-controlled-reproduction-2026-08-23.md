<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# A20 — controlled reproduction of the roadmap-sync import trust gate

**Host** `Ariellas` (the clean control host) · **measured** 2026-08-23 · **marathon step** A20
(`mstep-01a02a76-7ac5-74f7-a1a0-c4ed5ca4e085`), run `mrun-f5ef56dba3c1`.

## What was being reproduced

A peer reported an import trust gate refusing **65** files. The question was whether that is a
defect, and whether it reproduces on a host with a clean trust store.

## Method

Folded every `*.json` in the shared inbox `I:/coop/glpnet/roadmap-sync/inbox` by its `key_id`
field and by the repo segment of its `host__repo__stamp.json` filename, then compared against
what `buildkit-roadmap import` actually refused on this host.

## Measurement — 169 files, 6 distinct signing keys

| `key_id` | files | owner (first file signed by it) |
|---|---|---|
| `45d4c0f1a06e3117` | **65** | **`ariellas` — THIS HOST** |
| `36e450ef247a74d6` | 57 | `gavriella` |
| `6c6bbb680f3aea09` | 28 | `olamnit` |
| `66c9f04e045be536` | 7 | repo `buildkit` |
| `810f0bcaa9133135` | 7 | repo `yngenios-windows` |
| `8422afd5f6778bbd` | 5 | repo `olamnit-assistant` |

By repo: `glpnet` **150**, `buildkit` 7, `yngenios-windows` 7, `olamnit-assistant` 5.
0 unreadable. 76 files carry no `.license` sidecar.

## Finding 1 — the peer's "65 refusals" is NOT a defect, and the number is exact

**`45d4c0f1a06e3117` signs exactly 65 files, and it is this host's own signing key** — verified
against this host's own export `ariellas__glpnet__20260823T165142Z.json`.

So the peer's 65 refusals are **precisely the 65 exports authored by `ariellas`**, refused
because that peer has not verified `ariellas`'s key. The gate is doing exactly what it is
specified to do. **The count is a full explanation, not a coincidence**: it is a per-key total,
not an error rate.

**The trust gate is symmetric.** Each host trusts the keys it has verified and refuses the rest.
This host refuses **19** files (7 + 7 + 5) across three keys it has not verified, and refuses
**zero** `glpnet` files, because it has verified `ariellas`, `gavriella` and `olamnit`.

## Finding 2 (NEW) — every refusal on this host is CROSS-REPO pollution

The 19 refused files are **not glpnet exports at all**. They come from three other repos —
`buildkit`, `yngenios-windows`, `olamnit-assistant` — deposited into the **glpnet** inbox.

Every glpnet-authored file in the glpnet inbox imports cleanly. The trust gate here is firing
**only** on foreign-repo traffic that should not be in this inbox in the first place.

Two separate issues follow, and they must not be conflated:

1. **Trust distribution** — three keys are unverified on this host. Fixing that is key exchange.
2. **Inbox hygiene** — one shared inbox is carrying four repos' exports. Fixing *that* removes
   19 of 19 refusals on this host without exchanging a single key.

**Remedy order matters**: importing the foreign files with `--allow-untrusted` would "fix" the
warning by accepting unauthenticated cross-repo data into glpnet's roadmap. That is strictly
worse than the refusal. **Segregate the inboxes first.**

## Finding 3 — 76 of 169 files have no `.license` sidecar

Reported by `import` on every run as a separate warning from the trust refusal. These are
older files (July–early August). They import, so this is a hygiene debt, not a gate.

## Verdict

**The trust gate is CORRECT and reproduces exactly as designed on a clean control host.**
It should not be "fixed", and `--allow-untrusted` should not be adopted as routine. The real
defect it exposed is that four repos share one inbox.
