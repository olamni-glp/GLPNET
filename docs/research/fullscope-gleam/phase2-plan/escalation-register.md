<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Escalation Register — feature 059 full-scope Gleam GLP

Engineer-only conflicts. An OPEN entry names its due-before gate; a RESOLVED entry cites the ruling
plus the `wp_id` that enforces it. Scope never leaves silently (plan E3e); rulings live in
`../phase2-verify/rulings.md` and are mirrored here.

---

## `quic-sideprocess-relay`  (WP `rule-quic-sideprocess-relay`, detail_id `quic-sideprocess-relay`)

- **Status**: **RESOLVED 2026-07-27** (OPEN from 2026-07-20).
- **Depends on**: `freeze-link-transport-seam`.

**Filed request** — drift control for the sole delivered-but-**untested** capability: the Profile-A
QUIC OS-port line relay (`gleam_quic/src/glpq_ffi.erl`; long-line reassembly; stdio byte-identity to
the C# stack; `gleam_quic/test` empty — the single silent-drift hole in the delivered foundation).
Two dispositions were filed:

1. **freeze-by-file-pin as-is** — git-hash pin `glpq_ffi.erl`, no behavioral guard; or
2. **require a minimal in-corpus relay smoke test** before any Wave-4 build WP may depend on it.

**Ruling (Gabi, engineer/owner, 2026-07-27)** — **Disposition 2**. No Wave-4 WP may depend on the QUIC
OS-port relay until a minimal in-corpus smoke test exercising `glpq_ffi.erl` — **long-line reassembly
+ stdio byte-identity to the C# stack** — exists in the corpus and passes. Environment-fragility is
acknowledged: the smoke test targets the Profile-A QUIC C# host side-process and may be host-gated;
where it cannot run, that is classified **environment** (per the Profile-C QUIC discipline), recorded,
and the dependency stays **blocked** — never silently waived.

- **Enforcing `wp_id`**: `close-quic-sideprocess-relay-smoketest` (Wave-3 close; gates the Wave-4 QUIC
  build WPs `close-quic-transport-leaf` / `close-quic-client-inprocess-tests` /
  `close-quicws-link-completion-live-repl-bridge`). Until it passes, `glpq_ffi.erl` may not be
  depended on by any build WP.
