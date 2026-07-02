# Contract: `/GLP-Quick` CLI Surface

**Feature**: 036-http3-quic-ws-link | One Python tool hosts both roles (FR-007). The `/GLP-Quick` skill is a thin
front end that invokes this CLI. The surface is **identical across stacks** (FR-010): only `--stack` changes (and,
for `--stack gleam`, an optional `--profile a|c` selecting the deployment profile — A: AtomVM + native QUIC
side-process; C: full BEAM + `quicer`/MsQuic in-process; default `c`). The operator-facing surface is otherwise unchanged.

Entry point: `glp-quick = "glp_quick.cli:app"` (Typer).

## Commands

### `glp-quick cert generate --out <dir> [--days 365]`
Generates the shared self-signed certificate + key (FR-003). Prints the SHA-256 fingerprint.
- **Out**: `<dir>/glpquick.pem` (public), `<dir>/glpquick.key` (private), `<dir>/glpquick.fingerprint`.
- **Trust model**: this exact cert is the only anchor; copy `glpquick.pem` + fingerprint out-of-band to each host.

### `glp-quick --server --addr <ip|name> --port <udp> --cert <dir> [--stack csharp|gleam] [--max-clients 3] [--repl csharp|dart]`
Starts a server: binds the UDP port, loads the shared cert, launches+supervises the chosen stack runtime, accepts
up to `--max-clients` concurrent client links, and bridges a GLP REPL endpoint to each (FR-005/FR-008b).
- **Exit/clear-failure** (FR-019): bind failure, cert load failure, stack-runtime launch failure → non-zero exit + clear message.
- **Over-ceiling behaviour** (edge case): additional clients are **rejected with a clear error** (chosen policy), not silently dropped.

### `glp-quick --client --addr <server-ip|name> --port <udp> --cert <dir> [--stack csharp|gleam] [--repl csharp|dart]`
Connects a client: performs the real QUIC/HTTP-3 handshake trusting only the shared cert (by fingerprint), brings
up the WebSocket link, bridges a GLP REPL endpoint, and exchanges messages full-duplex (FR-001/002/008a).
- **Clear failure** (FR-019, edge cases): cert mismatch, ALPN/version mismatch, UDP blocked, server-not-ready →
  clear, distinct error; never a silent hang or half-open link; server-not-ready may retry with `--retry`.

### `glp-quick demo --addr <server-ip> --port <udp> --cert <dir> [--stack csharp|gleam] [--clients 3]`
Runs the LAN-IP conformance demo (the SC-001..SC-006 harness): one server + N≥3 clients, verifies a real on-wire
handshake, full-duplex exchange, a ≥3-REPL mesh, concurrent isolation, and single-client-failure resilience.
Emits a pass/fail report per success criterion.

## Cross-stack invariance (FR-010 / SC-006)
The operator-visible surface — flags, message/wire contract, handshake, and reported outcomes — is identical for
`--stack csharp` and `--stack gleam`. Only the data-plane runtime differs; `glp-quick demo` produces the same
observable outcomes on each implemented stack.
