<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-wireproto-crdt-convergence` (WP b3-c1-012, wave 2)

**Date**: 2026-07-23
**Method**: existence sweep of `glp_gleam/src` + `gleam_quic/` for the wire-stack terms, component-level diff against the three C# reference trees, and a per-item **required-vs-host-side scope table** (ruling-**input** — the classification is a recommendation for engineer sign-off, not a unilateral ruling).
**Paired close**: `close-wireproto-crdt-convergence` (b3-c1-037) — its Risk states it **must not start until this scope table has engineer sign-off**; the table in §3 is that judgment surface.
**Backing detail_ids**: `crdt-convergence`, `durable-mesh-messaging`, `message-envelope`, `schema-language`, `wire-registry`.

## 1. Existence sweep — the app-layer wire stack is Gleam-absent

`rg -in 'crdt|envelope|schema|registry|wal' glp_gleam/src gleam_quic/`, resolved word-level:

| term | Gleam hits | what they actually are |
|---|---|---|
| `crdt` | **0** | — (no CRDT anywhere in the Gleam corpus) |
| `schema` | **0** | — (no wire-schema toolchain) |
| `wal` (word-boundary) | **0** | the 33 raw hits are all substrings (`walk`, `wall`), no write-ahead log |
| `registry` | 5 | the **transport** registry (link seam T050, "a HOST interface, BELOW GLP" — `link/seam/transport.gleam:7,27,49`, `link_scheme.gleam:8`) + one type-checker instantiation map. **No** wire/schema registry. |
| `envelope` | 116 | the engine **result** envelope (`codec/result_envelope.gleam` — "version 0x01 + payloadType 0x11 RESULT_ENVELOPE", the FE/BE result seam). **Not** the CRDT router message-envelope. |

So none of the five wire-stack capabilities exists in Gleam. The only term that overlaps — "envelope" — is a **different layer** (the engine→client result seam), and the delivered 038 **term codec provides TLV encode/decode primitives**, but neither is the router-opaque *message* envelope.

## 2. Component diff vs the C# reference trees

All three C# packages are present and rich; Gleam mirrors **0** of their components:

| C# package | components (source dirs/files) | Gleam mirror |
|---|---|---|
| `csharp/glp_crdtmsg/` | bridge · cap · crdt · envelope · header · model · route · sig · store | **0 / 9** |
| `csharp/glp_schema_lang/` | ast · evolve · lift · lower · parser · pattern · registry · validate | **0 / 8** |
| `csharp/glp_wire_registry/` | `SchemaRegistry.cs` · `WireRegistry.cs` | **0 / 2** |

## 3. Required-vs-host-side scope table (ruling-input — needs engineer sign-off)

Recorded axes are from the gap-inventory (2026-07-19). The **architecture test** applied: *is the capability invoked by GLP language semantics (a kernel, guard, the reduction loop, or the M2 term-link), or is it protocol/application infrastructure a GLP program consumes across the host boundary?* The former ⇒ Gleam-required; the latter ⇒ host-side (satisfied-by-reference, interop-tested from Gleam).

| detail_id | inventory axes | Gleam | **recommended scope** | rationale (evidence) |
|---|---|---|---|---|
| `crdt-convergence` (b3-c1-064) | parity-required=**yes** · not-promised · b3 | absent | **HOST-SIDE** | CRDT rich-text ops over an op-WAL (Fugue/Peritext) — an application data-type capability above the engine (`glp_crdtmsg/crdt/`, `route/Mesh.cs`; tests StoreConvergence/Fugue/Peritext). No GLP kernel/guard/link touches it. |
| `message-envelope` (b3-c1-063) | parity-required=**yes** · not-promised · b3 | absent (router envelope); **TLV substrate present** | **HOST-SIDE routing, INTEROP-REQUIRED** | Router-opaque unified envelope w/ verbatim-forwarded TLV sections (`header/UnifiedHeader.cs`, `envelope/`). Routing is host-side, **but** if a Gleam instance must forward/consume messages across the M2 link, it must correctly produce/consume the envelope — the delivered 038 TLV term codec is the reusable substrate. **Strongest interop obligation; do not drop.** |
| `schema-language` (b3-c1-066) | parity-required=**yes** · not-promised · b3 | absent | **HOST-SIDE** | A full wire-schema DSL toolchain (parse/validate/evolve/CDDL interop; 8 components). Porting to Gleam = large redundant build with no GLP-semantics driver. Highest **bloat** risk if misclassified as required. |
| `wire-registry` (b3-c1-067) | parity-required=**yes** · not-promised · b3 | absent | **HOST-SIDE** | Payload-type→schema lookup table (`WireRegistry.cs`/`SchemaRegistry.cs`) — protocol infrastructure. A Gleam instance does registry lookup via interop if needed, not a native port. |
| `durable-mesh-messaging` (b1-c1-070) | parity-required=**no** · partial · b1 | absent (no WAL) | **HOST-SIDE / not a current obligation** | Signal-then-fetch WAL/PGLite-tiered protocol: prototype delivered (roadmap:55 `[closed]`), full protocol **captured/unspecified** (roadmap:105 `[captured]`). Parity not required; lowest ambiguity. |

**Recommendation summary**: all five → **host-side (satisfied-by-reference)**, with `message-envelope` carrying an explicit **interop obligation** (Gleam produces/consumes the envelope at the link boundary, reusing the 038 TLV codec). This split directly addresses both horns of the WP Risk — it avoids **bloating** the Gleam port with the schema-lang/CRDT/registry toolchains, while refusing to **silently drop** the one item (message-envelope) where cross-link parity is a live interop requirement.

**Uncertainty flagged for the engineer (per the close's sign-off gate):** the four `parity-required=yes` items (crdt, message-envelope, schema-language, wire-registry) are, by their recorded b3 axis, parity obligations — yet the architecture places them host-side. That contradiction is the judgment surface. This verify recommends host-side classification **but does not rule**; `close-wireproto-crdt-convergence` must obtain engineer sign-off on this table before implementing anything.

## Activation

- **`close-wireproto-crdt-convergence` (b3-c1-037)** is the sole downstream WP (covers all five detail_ids). It is **gated on engineer sign-off of §3** (its own Risk). On sign-off, its per-item plan follows directly: host-side items → recorded satisfied-by-reference with an interop test from Gleam (envelope forwarding, registry lookup); any item the engineer re-classifies Gleam-required → a Gleam test mirroring the named C# suite (`glp_crdtmsg.tests` StoreConvergence/Router/Foundation, `glp_schema_lang.tests`, `glp_wire_registry.tests`).
- No code work in scope here; the Gleam wire-stack is confirmed absent, and the load-bearing output is the §3 scope table.
