# glp_quick — `/GLP-Quick` control-plane tool (feature 036)

One Python tool that hosts both roles of a **genuine HTTP/3 (QUIC) + WebSocket channel-link**
between independently-started CLI processes on a LAN, used to run GLP between GLP REPL endpoints
(one-way send/listen → full-duplex → peer-to-peer duplex mesh). Backed by the `/GLP-Quick` skill.

- **Control plane (this package)** — operator CLI (`glp-quick`), shared self-signed certificate
  generation + out-of-band trust pinning (`cert.py`), launch/supervision of the per-stack
  transport runtime, the GLP-REPL ↔ link bridge (`repl_link.py`), and the LAN-IP demo (`demo.py`).
  Python is **never** the QUIC endpoint.
- **Data plane** — C#/.NET first (reference; `System.Net.Quic`/MsQuic, GA in .NET 9, cross-platform),
  then Gleam (two deployment profiles). Each is a real QUIC handshake + a genuine RFC 6455
  WebSocket link carried over one QUIC bidi stream (spec 025 `FrameCodec`), reusing spec 025's
  `ILinkTransport`/`ILinkEndpoint` seam (FR-018).

## CLI surface (see `specs/036-http3-quic-ws-link/contracts/cli-contract.md`)

```
glp-quick cert generate --out <dir> [--days 365]
glp-quick --server --addr <ip|name> --port <udp> --cert <dir> [--stack csharp|gleam] [--max-clients 3] [--repl csharp|dart]
glp-quick --client --addr <server-ip|name> --port <udp> --cert <dir> [--stack csharp|gleam] [--repl csharp|dart]
glp-quick demo   --addr <server-ip> --port <udp> --cert <dir> [--stack csharp|gleam] [--clients 3]
```

## Dev setup

```
py -3 -m venv .venv
.venv\Scripts\python -m pip install -e .[dev]
.venv\Scripts\python -m pytest
```

Status: scaffolding (Phase 1/2 of tasks.md). Behaviour lands per the user-story phases (US1 MVP onward).
