# Contract — Terminal sub-protocol (L6 over the 036 L5 `GlpMessage`)

**Status**: authoritative for feature 040 terminal-message shapes. Rides `repl_link.GlpMessage` (feature 036);
does **not** re-implement L4/L5 reliability, sequencing, or dedup (constitution VIII; FR-018/FR-026).

## Framing

Every terminal message is one `GlpMessage` whose `payload` is a **ground GLP term** of the form

```
tmsg(<Kind>, <Field1>, <Field2>, …)
```

- `Kind` is a lowercase atom (see table). Term strings are quoted `"…"` with GLP escaping; a bare text payload
  with no `tmsg(` head decodes as `chat` (backward compatibility with the prototype).
- Ground-relay is enforced: no `_w(` / `_r(` may appear (any such literal inside user text is escaped by the codec
  before egress and un-escaped on ingress). Violations raise `GroundRelayViolation` (a caller bug, not tolerated).
- Routing (`to` / `broadcast`) and identity (`from`) live in the enclosing `GlpMessage`, resolved per
  `contracts/wire-contract.md`; `@name` targets are resolved against `handle.peers()` before send (FR-040).

## Message kinds

| Kind | Term shape | Purpose (FR) |
|---|---|---|
| `chat` | `tmsg(chat, "Text")` or bare `"Text"` | plain conversation line (US1) |
| `page` | `tmsg(page, "Name", "Owner", Kind, "Text")` | transmit a whole page as an owned block; `Kind ∈ {plain,mask,repl}` (FR-007/FR-010) |
| `pinpoint` | `tmsg(pinpoint, "Page", Row, Col, H, W, "Block", Class)` | joint live overwrite; `Class ∈ {transient,permanent}` (FR-012/013/014) |
| `form_def` | `tmsg(form_def, "Page", [label(R,C,"L")…], [field(R,C,W)…])` | define a mask/form (FR-015) |
| `form_fill` | `tmsg(form_fill, "Page", [fill(Idx,"V")…])` | return a filled form, labels intact (FR-015) |
| `repl_goal` | `tmsg(repl_goal, "Page", "goal.")` | a GLP goal entered on a REPL page (FR-016) |
| `repl_result` | `tmsg(repl_result, "Page", "Rendered")` | the REPL's rendered result for that page (FR-016) |
| `rcopy_offer_query` | `tmsg(rcopy_offer_query)` | ask a peer what file service it offers *this* user (FR-018) |
| `rcopy_offer` | `tmsg(rcopy_offer, [root("Name",[ "folder"… ],QuotaLeft)…])` | the responder's offer; empty list ⇒ none (FR-018/FR-032) |
| `rcopy_manifest` | `tmsg(rcopy_manifest, "Root", "Folder", Mode, [file("rel",Size,"sha")…])` | proposed transfer set; `Mode ∈ {synchronise,force}` (FR-029/FR-030) |
| `rcopy_verdict` | `tmsg(rcopy_verdict, [verdict("rel", V)…])` | per-file `V ∈ {need,skip_identical,reject(Reason)}`; `Reason ∈ {quota,perm,path}` (FR-034/FR-038) |
| `rcopy_chunk` | `tmsg(rcopy_chunk, "rel", Seq, "b64")` | one bounded file chunk (base64) (FR-039) |
| `rcopy_outcome` | `tmsg(rcopy_outcome, "rel", Outcome, Reason)` | final per-file outcome `∈ {transferred,skipped_identical,filtered_out,rejected}` (FR-031) |
| `link_status` | `tmsg(link_status, State, "Detail")` | out-of-band link-state note surfaced to the OIA (R6/FR-043) |

## Invariants

1. **Owned pages, not a shared chat**: a `page` message MUST render on the receiver as a distinct page owned by
   `from` (the sending peer), MUST NOT overwrite a same-named local page, and MUST NOT be merged into the CHAT
   page. It raises the OIA "new page" indicator and MUST NOT steal focus. (FR-010 / SC-002)
2. **Directed vs broadcast**: `page`, `pinpoint`, `form_*`, `repl_*`, and every `rcopy_*` message MUST be directed
   (`to = PeerId`), resolved against `handle.peers()`; an unknown target is reported, never silently redirected.
   (FR-040 / SC-011)
3. **Ground terms only**: every payload is a ground GLP term; the codec is the single encode/decode point shared by
   `tui` and `link_console` so the two cannot drift. (VIII / R2)
4. **No parallel transport**: all kinds ride this one seam; `/rcopy` opens no second socket. (FR-026)
5. **Chunk bound**: `rcopy_chunk` byte length is bounded well under the 025 frame/line limit; large files span many
   chunks and rely on 025 reliability + the ≥1 MiB reassembly already in place. (FR-039 / R8)

## Backward compatibility

A prototype peer that sends bare chat text still interoperates (decoded as `chat`). New kinds are additive; an
unrecognized `tmsg(Kind,…)` is surfaced as an informational line rather than crashing the receiver (still
**reported**, never silently swallowed — R6).
