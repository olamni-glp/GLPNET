# Sources — blazor-spa-bg-api

The AIGRID prereq-patterns catalog is glpnet's consolidating upstream for this pattern. AIGRID's `prereq-patterns/blazor-spa-bg-api/` index is research-grounded — no hatzinor reference exists for the assembly today. The cited sources are predominantly trusted-web canonical references for Blazor WebAssembly hosting, Flask + websocket integration, and the underlying websockets protocol, plus forward-references to two sibling glpnet patterns (`flask-sqlalchemy-alembic-api` substrate and `background-task-manager` supervisory). Glpnet has no own implementation today.

## Index

| Path | Upstream | Action | Summary |
|---|---|---|---|
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/blazor-spa-bg-api/description.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's pattern description — Blazor-SPA + Flask-websocket assembly, substrate-friendliness rationale. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/blazor-spa-bg-api/applicability.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's consumer-class notes for the four assembly shapes (static-served SPA, laptop bg API, hosted-under-bgtm, websocket endpoint). |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/blazor-spa-bg-api/sources.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's seven trusted-web canonical references (Microsoft Blazor docs, Flask docs, flask-sock, websockets) plus two sibling-pattern forward references. |

## Per-source notes

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/blazor-spa-bg-api/description.md`

- Three pieces compose: Blazor WASM (static-served front-end), Flask + websocket (the API + live channel), background-task-manager (the supervisory hosting). The novelty is composing them while honouring the pglite single-session-pool constraint end-to-end.
- The websocket-endpoint substrate-friendliness rule: the endpoint MUST NOT hold a SQLAlchemy session for the lifetime of the connection. Two acceptable shapes (poll-then-push; publish-channel + write-side trigger) are sketched; the experimental validation pins which shape glpnet adopts.

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/blazor-spa-bg-api/applicability.md`

- Per-shape H3s cover: static-served SPA (CORS / origin), laptop bg API (`.env`-loaded; binds `127.0.0.1:<dev-port>`), hosted-under-bgtm (lifecycle managed by the supervisory pattern), websocket endpoint (the substrate-friendly shapes).

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/blazor-spa-bg-api/sources.md`

- Cites Microsoft Blazor WebAssembly hosting docs (`https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly`), Blazor SPA → API (`https://learn.microsoft.com/en-us/aspnet/core/blazor/call-web-api`).
- Cites Flask top-level (`https://flask.palletsprojects.com/`), `flask-sock` (`https://flask-sock.readthedocs.io/`), `python-websockets` (`https://websockets.readthedocs.io/`).
- Cites two sibling-pattern forward references: `flask-sqlalchemy-alembic-api/description.md` (the substrate); `background-task-manager/description.md` (the supervisory host).
