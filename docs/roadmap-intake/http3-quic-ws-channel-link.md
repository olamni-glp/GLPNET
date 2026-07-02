# Roadmap intake — HTTP3-QUIC-WS-Channel-Link-proto (feature 036)

> **Status**: epic `HTTP3-QUIC-channel-and-WS-link`, feature `HTTP3-QUIC-WS-Channel-Link-proto`.
> **Already specified AND built** this cycle as **feature 036** (`specs/036-http3-quic-ws-link/`).
> This roadmap entry is the durable record + the methodology + a re-`/bk-specify` prompt for a fresh
> session if a clean re-spec is wanted.

## What it is (delivered)

A **genuine** HTTP/3 (QUIC) + WebSocket channel-link between independently-started CLI processes, used
to run GLP between GLP REPL endpoints. One `/GLP-Quick` skill over **one Python tool** that hosts both
roles (`--server` / `--client`); a server can serve several clients; works on a LAN by **IP / machine
name** (no domain names). **No shortcuts — a real QUIC handshake** (`System.Net.Quic`/MsQuic, GA in
.NET 9+, IsSupported-gated), genuine **RFC 6455 WS over one bidi QUIC stream**, shared self-signed cert
pinned by **SPKI SHA-256**.

## Delivered this session (branch `036-http3-quic-ws-link`, commits `79465629..bd8a03d4`)

- **US1 MVP** — real QUIC+WS, full-duplex, cert-pin (5 xUnit).
- **US2** — multi-accept mesh server, ≥4 isolated clients, broadcast, isolation, over-capacity.
- **US3 Gleam Profile A** — Gleam/BEAM channel-link + C# genuine-QUIC side-process (SC-006 PASS);
  Profile C (`quicer` in-process) build-blocked (needs MSVC — available on **gavri** or via **WSL**).
- Interactive `--server`/`--client` console + a `--tui` 3270 prototype.
- **18 pytest + 104 xUnit green.** Research corpus (106 notes) + distillation already committed earlier.

## Methodology (Gabi's intended research-driven process — for a clean re-run)

Run as **one durable `/bk-marathon`** (no need to split into features), refined/extended to a working
prototype, with **two teams / pipelines**:
1. A strategy team that finds, in detail, all **architectural concerns** to confidently design a stable,
   extendable **C#** prototype.
2. A pipeline that first defines a **research strategy** (multiple team roles to identify + clarify
   research directions), then runs **agent teams** to research **both Gleam (AtomVM/WASM-BEAM) and C#**,
   collecting technical + academic papers — a **corpus of ~50 per stack**.
Then a research pipeline that **distills (not summarises)** the corpus via close reading using a
pre-formulated analysis strategy, with **follow-up web/GitHub research**, to extract the key
architectural concerns. Then a **detailed implementation plan** (components, interfaces, step-by-step) →
**skeleton/mock (top-down)** → implement on the basis of detailed tasks following the full roadmap.

## `/bk-specify` prompt (paste into a fresh session to re-specify cleanly)

```
/bk-specify HTTP3-QUIC-WS-Channel-Link-proto — A working prototype, runnable from two CLI instances
(one --server, one --client) that talk to each other, delivered as a /GLP-Quick skill over ONE Python
tool combining both roles. Data plane in C#/.NET (System.Net.Quic/MsQuic, real QUIC, no shortcuts) and/or
Gleam on AtomVM (WASM-BEAM, runnable via Node) — install any needed tooling (Erlang/Gleam/AtomVM or .NET).
A server can spin up and serve several client instances over a LAN, addressed by IP / machine name (no
domain names). Must FULLY demonstrate genuine QUIC end-to-end (a real on-wire handshake, not loopback
simulation) + a WebSocket link carrying GLP between GLP REPL endpoints (send/listen → full-duplex →
peer-to-peer mesh). Reuse spec 025's link seam. Run as ONE durable bk-marathon. First do web research:
two pipelines — (1) C# architectural-concerns strategy; (2) a research-strategy pipeline (multi-role) for
Gleam + C#, building a ~50-source corpus per stack, then a distillation pipeline that close-reads/distills
(not summarises) the corpus with follow-up web/GitHub research into key architectural concerns, then a
detailed implementation plan (components/interfaces/steps) → top-down skeleton/mock → implement.
```

## Restart / save-restart signal (when & how)

- **State of record** = the roadmap (`buildkit-roadmap status`) + this repo's commits + the marathon
  (when the `marathon` CLI is available — currently ABSENT from the installed buildkit; see memory
  `036-http3-quic-ws-marathon`). Until then, **commits are the durable checkpoints**.
- **On restart** (fresh session / post-compaction): read CLAUDE.md mandatory docs, then
  `buildkit-roadmap status` → pick the next feature; for 036 the build is largely done (see
  `specs/036-http3-quic-ws-link/tasks.md` for the per-task [x]/[~] state and the remaining items:
  Profile C `quicer`, true two-host run on **gavri**, live `glp_repl`-process bridge).
- **Save-restart signal**: commit + push (durable), update memory `036-http3-quic-ws-marathon`, and note
  the next action in `docs/current_plan.md`. A new session resumes from the roadmap + tasks.md + memory.
