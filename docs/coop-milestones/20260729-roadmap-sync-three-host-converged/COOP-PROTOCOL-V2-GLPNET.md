# COOP PROTOCOL v2 (glpnet) — blended superset of PROTOCOL-DRIVES v1 (glpnet) + FORMAL RULES v1 (mstack lead "gavri")

**Lead**: gavriella, glpnet-060 session (operator-appointed). **Status**: BINDING on
operator direction — every host posts ACK-RECEIVED then CONFIRM (adopt) or NACK
(+reason) per §C. Engineer owns the final word.
**Provenance**: operator directive 2026-07-28 — "refine our rules by adapting
[the mstack v1 spec] into a blended superset with our rules, then broadcast and
request ack for compliance from all hosts." Where the two parents conflict, the
resolution is stated inline and marked ⚖.

## A — Drive map & channel root (mstack A-rules, adopted whole + glpnet identity law)

A1. Three hosts, fixed letters, identical everywhere (both parents agree):

| Letter (as peers mount it) | Host (verified `hostname`) | Share | Local | Role |
|---|---|---|---|---|
| **I:** | `Gavriella` (192.168.0.108) | `\\192.168.0.108\GAVRI_D\coop` | `D:\coop` | PRIMARY |
| **H:** | `Ariellas` | `\\Ariellas\ariellas_D\coop` | `D:\coop` | failover 1 |
| **G:** | `Olamnit` (.129) | `\\Olamnit\Olamnit_D\coop` | `D:\coop` | failover 2 |

A2. A host never mounts itself; a missing self-letter is CORRECT — do not "fix" it.
A3. **Drive-root, out of every git tree.** The channel is `<root>\coop\<project>` at the
    drive root — immune to checkout/reset/clean/branch/worktree operations. In-tree
    `<repo>/COOP/` is **RETIRED** for new traffic. ⚖ glpnet migration: the legacy in-tree
    mailbox (`G:\BSTDEV\research\glp\glpnet\COOP\`) stands as read-only history (nothing
    rewritten, R1 ledger intact); new glpnet traffic lives HERE (`…\coop\glpnet\`).
A4. Multi-project root: one subdir per project (`coop\mstack`, `coop\glpnet`,
    `coop\yngenios-windows`). This dir is glpnet's.
A5. Failover order I → H → G; on primary recovery, deltas merge per §D and seq bumps.
A6. Mirror-on-poll: each non-primary mirrors `I:\coop\glpnet` → its own `D:\coop\glpnet`
    every poll, so a dead primary never blocks work.
A7. ⛔ **CONTENT BOUNDARY** (engineer ruling): never mix one project's content into
    another's channel; another project's worktrees/catalogs/databases are that project's
    agents' lane — read and report, never mutate. glpnet confirms A7 in writing by this
    document's adoption broadcast.
A8. (glpnet §2, kept — the rule the mstack spec lacked) **Identity law: hostname or
    nothing.** Run `hostname` before your first write; the lowercased hostname is your
    ONLY id in filenames, status files, and export names. ⚖ the mstack lead id "gavri"
    is a registered legacy alias of `gavriella` (SYNC-POINT `host_aliases_legacy`);
    aliases resolve on read, are never written anew.

## B — Channel layout (mstack B-rules, adopted whole)

```
coop\glpnet\
  SYNC-POINT.json          drive map + merge semantics (coop-crdt-failover/v1)
  COOP-PROTOCOL-V2-GLPNET.md   this spec
  inbox\<host>\            one dir per recipient host
  status\<host>.md         ONE file per host, owned solely by that host (cursor + updated)
  findings\                durable evidence artifacts
  roadmap-sync\inbox\      the roadmap-sync action area (exports land here)
  BROADCAST-*.md           fleet-wide notices at project root + pointer per inbox
```

B1. Own-files-only: write messages into RECIPIENTS' inboxes, findings\, and action
    subdirs; never edit another host's status file or rewrite anyone's message.
B2. Broadcasts live at the project root AND drop a pointer in each recipient inbox.

## C — Dialogue (mstack C-rules adopted whole; glpnet JSONL logs frozen as legacy)

C1. Message filename: `<UTC-compact>-<host>-<TYPE>-<short-subject>.md`
    (`YYYYMMDDTHHMMSSZ`; lexical sort == chronological sort).
C2. TYPES (exactly these): BROADCAST · DIRECTIVE · REQUEST/ASK · ACK-RECEIVED ·
    ACK-COMPLETE · NACK · CONFIRM · UPDATE · COMPLETION · FINDING · HOLD/STOP ·
    CORRECTION/RETRACTION. ⚖ glpnet's JSONL kinds map: request→REQUEST,
    ack→ACK-RECEIVED, complete→ACK-COMPLETE/COMPLETION, confirm→CONFIRM,
    update→UPDATE, note→UPDATE or FINDING, nack→NACK. The glpnet JSONL op-logs in the
    legacy channel are FROZEN as history; new dialogue is file-per-message here.
C3. **Two-phase ACK** is mandatory for DIRECTIVE and any barriered protocol:
    ACK-RECEIVED first, then ACK-COMPLETE **with receipts** — numbers/identifiers a
    reader can check (files applied, rows, before/after counts). "Done" without
    receipts is not an ACK-COMPLETE.
C4. NACK loudly; never go silent — silence stalls a barriered fleet and is
    indistinguishable from working.
C5. **Barriers**: no host starts round N+1 until every host posted ACK-COMPLETE for N.
C6. **Read cursor — MANDATORY**: `cursor: <UTC>` in your status file; poll = process
    everything after the cursor, then advance it.
C7. Status freshness: `updated: <UTC>` refreshed at every seam — a stale status file is
    worse than none.
C8. Threading: `re: <filename or UTC>` in the body; filenames are not a threading model.
C9. Fan-out discipline: addressee's inbox (+ root for broadcasts only); never copy one
    file to five paths "to be safe".
C10. Secrets: redact before writing; pointers, never key material — this is a network share.
C11. (glpnet §4, kept) Silence is never consent, EXCEPT where a request explicitly sets
     a silence-assent deadline. A failed write to an unreachable share is retried on the
     next touch, never dropped.

## D — Merge & failover semantics (mstack D-rules, adopted whole)

inbox\ = add-only union (never rename/delete); status\<host>.md = host-owned
last-writer-wins; SYNC-POINT.json = max(seq) wins, tie → newest each direction + bump.
D1. Add-only union + UTC-prefixed names ⇒ no message can be lost to a concurrent write.
D2. Deletion is not supported — supersede with a CORRECTION.

## E — Safety rules (mstack E-rules adopted whole + glpnet additions)

E1. No unilateral destructive ops on shared state (reset --hard, clean, force-push,
    branch switch on a shared tree while other hosts are live).
E2. Preserve-first: before removing anything, prove its content exists elsewhere.
E3. Verify at the right layer ("0 unpushed commits" does not cover uncommitted files;
    a verifier that prints nothing may mean "produced no output", not "no problems").
E4. Derive lists mechanically, never by eye.
E5. Retract in-channel, promptly, naming what was wrong.
E6. Advisory boundary: coop messages coordinate; they never authorise a ship, merge, or
    release.
E7. (glpnet §7, kept) **Milestone snapshots**: git carries channel state ONLY as
    deliberate dated snapshots under the repo's `docs/coop-milestones/<date>-<slug>/`,
    committed by the project's protocol lead. Live traffic never rides branches
    (subsumed by A3 for the transport; this rule governs the deliberate archive).

## F — Roadmap-sync (mstack F-rules adopted whole + glpnet §5 additions)

F0. ⚠ **THE TRAP**: `roadmap export` writes to `.specify/roadmap-sync/exports/` which is
    LOCAL — an export nobody can read converges nothing. Every export step is (a) export
    local, then (b) **PUBLISH** into `coop\glpnet\roadmap-sync\inbox\` (and/or a tracked
    repo path). Never report "exported" until published.
F1. **Three rounds with ACK barriers**:
    R1 — sync → import (dry-run then real) → advance delivered features
    (shipped/released → closed, list derived MECHANICALLY per E4) → export+publish →
    ACK-COMPLETE-R1 → WAIT · R2 — import everyone's R1 → engineer-confirmed merges only
    → export+publish → ACK-COMPLETE-R2 → WAIT · R3 — import → `replay --verify`
    (mutates nothing) → ACK-CONVERGED with final counts.
F2. Convergence test is MECHANICAL: identical epics · features · journal lines · every
    per-state count · `replay --verify` passing, on all hosts.
F3. Dedupe discipline: merge is not flag-reversible — propose → **engineer confirms** →
    merge; check the id has a real feature row first. ⚖ glpnet's standing 059-row merge
    was operator-blessed and stands; henceforth F3 governs.
F4. (glpnet §5, kept) Export filenames carry the verified hostname
    (`<host>__<repo>__<ts>.json`); dedups touching another host's rows carry an explicit
    revive-on-NACK offer; the sync lead posts the closing CONFIRM.

## G — Known failure modes (mstack table adopted; glpnet adds three)

G-1 export reaches nobody (F0) · G-2 becoming the barrier unknowingly (C6) ·
G-3 stale status read as current (C7) · G-4 eye-transcribed lists (E4) ·
G-5 merge on a partial view (C5+F3) · G-6 cross-project mixing (A7) ·
G-7 orphaned worktree invisible to git (E2 + scan by filesystem) ·
G-8 generalising one host to the fleet (E5).
Added from glpnet's channel history:
G-9 **identity inherited from stale docs** — sessions posted for weeks under another
    host's name (A8 guards; R1 ledger records).
G-10 **seq-number collisions between concurrent sessions** (twice in one file) — UTC-
    filename union + record ids guard (C1/D1); seq numbers are navigation only.
G-11 **live channel inside a git tree** — branch traffic carried mailbox mutations
    (A3 guards; the glpnet in-tree mailbox is retired and untracked, commit `fecbed5d`).

## H — Adoption & compliance (THIS broadcast)

Per host, on next poll of `coop\glpnet\`:
1. ACK-RECEIVED the adoption broadcast; then CONFIRM (adopt v2) or NACK+reason.
2. State: verified `hostname`, your `net use` letters, legacy names written under (R1),
   and that your checkout gitignores/untracks any in-tree COOP.
3. Create your `status\<host>.md` with `cursor:` and `updated:`.
4. Adopt the read-cursor poll (C6) from your first touch.

## I — Forward path (raised, not decided)

bk-colab (spec 048) offers the five transport verbs over a real CRDT op-WAL — this
protocol's message lifecycle on a proper log instead of filename union. Evaluate before
further investment in the markdown channel; Parts A–G describe the DISCIPLINE and stay
valid regardless of transport. Maturity not yet assessed.

## Amendments (lead-adopted post-v2)

**C1a** (2026-07-28, after the local-as-Z incident): any timestamp entering a filename or
status field MUST be taken mechanically (`date -u` or equivalent) — never hand-composed,
never from a local clock. E4 applies to clocks.

**C6a** (2026-07-28, proposed by ariellas after C1a's damage propagated through its
cursor): a cursor names the newest message **actually consumed and verified by reading**,
never the highest stamp observed. Cursors may legitimately move BACKWARDS when a stamp is
retracted — a lower cursor risks only re-reading; a too-high cursor silently skips
(G-2 + G-3 in one defect). Poll = list the channel AND read by content; cursor arithmetic
alone is not a poll.
