# Contract: Bridge CLI surface (`pglite_bridge.mjs`)

Source: spec FR-005, FR-012, FR-030; AIGRID prereq-patterns/pglite sources.md row A9; research R3, R4, R9.

This contract documents the bridge's CLI as it exists post-this-feature. Pre-existing flags carry forward unchanged; new flags are additions only.

## Invocation

```
node prereq-patterns/pglite/pglite_bridge.mjs [flags]
```

## Flags

| Flag | Type | Default | Status | Semantics |
|---|---|---|---|---|
| `--data-dir <path>` | string | (required) | existing | PGLite cluster directory. For repo-wide use: `.pgdb`. For prereq-pattern use in a feature-private deployment: any path. |
| `--port <int>` | int | `0` (ephemeral) | existing (default changed) | TCP port to listen on. `0` = OS-allocated. Use explicit port only for debugging or fixed-port tests. |
| `--host <ip>` | string | `127.0.0.1` | existing | Bind address. MUST be loopback for repo-wide use (`.pgdb/`); the bridge has no remote-access story. |
| `--daemon` | bool | `false` | existing | Disables stdin-end-exit handler so the bridge survives a detached spawn with `stdin=DEVNULL`. Required for the auto-spawn-on-demand path (FR-006). |
| `--transport <name>` | string | `tcp` | existing | Reserved (forward-compat; only `tcp` accepted today). |
| `--no-lock` | bool | `false` | NEW | Skip OS-level lock acquisition. **Permitted only for the manual launcher escape hatch and bridge unit tests.** Production / auto-spawn paths MUST NOT pass this flag. |

## Stdout

- Pre-detach: exactly one line `BRIDGE_READY port=<int> pid=<int>\n` after `listen()` resolves AND `bridge.json` is written. No other stdout pre-READY.
- Post-detach (with `--daemon`): nothing to terminal; all stdout redirects to `<data-dir>/bridge.log` (rotated, R9).

## Stderr

- Pre-detach: `[bridge]`-prefixed diagnostic lines (existing `[bridge] pglite ready data_dir=...` style). On lock failure: `[bridge] BRIDGE_LOCK_HELD pid=<int> at <host>:<int>`. On PGLite init failure: `[bridge] BRIDGE_ERROR pglite_init_failed <msg>`.
- Post-detach: redirected to `bridge.log` along with stdout.

## Exit codes (compiled)

| Code | Meaning |
|---|---|
| 0 | Graceful shutdown (SIGTERM / SIGINT received cleanly) |
| 1 | PGLite init failed |
| 2 | Generic listen error |
| 5 | Lock acquisition failed (another bridge holds `.bridge.lock`) OR explicit `--port` already in use |
| 9 | Sidecar JSON write failed |

## Side-effects on file system

When invoked successfully:

1. Acquires `<data-dir>/.bridge.lock` (held for process lifetime; kernel-released on exit).
2. Writes `<data-dir>/bridge.json` atomically (tmp + rename) before emitting `BRIDGE_READY`.
3. With `--daemon`: writes `<data-dir>/bridge.log` (rotates `bridge.log.1`, `.log.2`, `.log.3` at ~5MB each, R9).
4. Best-effort deletes `<data-dir>/bridge.json` on graceful shutdown.

## What is NOT in scope of this CLI contract

- Connection-level Postgres-wire protocol — see existing bridge implementation.
- Schema migrations or DDL — clients are responsible for their own schema lifecycle.
- Authentication — bridge accepts any credentials (FR-005 unchanged); it is loopback-only and trusts the OS.
- COPY-IN — forbidden per FR-026; `pglite_bridge.mjs` does not intercept.

## Interaction with prereq-pattern's `description.md` and `applicability.md`

Per FR-012, `prereq-patterns/pglite/description.md` MUST be amended in this feature to clarify that:

- The canonical bridge file IS the live deployment for repo-wide PGLite use against `.pgdb/`.
- The "copy the bridge into your feature working tree" guidance from feature 011 still applies for features that need a SEPARATE PGLite (no current consumer); it is no longer the default for in-repo PGLite use.

Implementation task: amend `description.md` in this feature; do NOT amend `applicability.md` (its consumer-facing content is unchanged).
