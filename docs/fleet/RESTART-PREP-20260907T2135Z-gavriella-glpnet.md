# RESTART POINTER — gavriella.glpnet @ GAVRIELLA

**Written 2026-09-07T21:35Z. Resumable with just: `resume marathon`.**

All times UTC. This host is UTC+0100; every local clock reading below was converted.

---

## Where things stand

| item | state |
|---|---|
| branch | `develop`, clean, 0 ahead / 0 behind `origin/develop` |
| era 110 | **SHIPPED** — `v2026.09.07.1`; feature PR #325, release PR #326, back-merge PR #327, all merged; tag local + remote |
| roadmap | `ynet-frame-field-parity-across-planes` → **released**; slot **free**; 51 not-closed, all scored |
| suite | `ynet_client.tests` **187/187**, 0 failed, 0 skipped (177 baseline + 10 new) |
| active feature slot | **EMPTY** — released cleanly; 22 features were waiting on it |

## What era 110 delivered

Carrier frame-field parity: an `internal BuildFrame` seam on both carriers, a
`FrameFieldParity` classifier that compares the two constructed frames field-by-field with
**engineer rulings held as data**, ten tests, and one authorised value change
(file plane `Sequence` 0-based → 1-based, Q-110-02).

**The measurement that mattered: the roadmap brief said three fields diverged; there are four.**
`SenderNode` was missing from it. The check now produces that count by machine, so it can never
again be typed by a person.

## 🔴 Open items the next session inherits

1. **`buildkit-roadmap sync --round 86` FAILS, reproducibly (twice).**
   `psycopg.OperationalError: the connection is lost`, raised from
   `crdt/importer.py:1259` executing `ROLLBACK TO SAVEPOINT import_file`.
   🔴 **The crash is in the ERROR HANDLER, so the original import error is destroyed and never
   surfaces.** Export works (21 epics / 148 features published this tick); only peer **import**
   is blocked. This is a defect worth a feature: an error path that hides the error it exists to
   report is strictly worse than no error path.

2. **No marathon run tracked era 110.** The pipeline was driven directly, so
   `buildkit-marathon status` reports "no active marathon run for feature
   '110-ynet-frame-field-parity'". The era is real and shipped; the harness has no row for it.
   Open a run BEFORE the next era rather than reconstructing one after.

3. **The retrospective records `codexreview_findings: 0`.** The review genuinely ran
   (`codex exec`, six findings, three HIGH, all fixed, evidence committed under
   `specs/110-ynet-frame-field-parity/evidence/`), but it was invoked directly rather than
   through `buildkit-codexreview`, so the catalog has no record. **A review that happened and
   was not recorded reads identically to one that never ran** — use the tool next time.

4. **Q-110-01's residual risk is named, not closed.** `SenderActor` is ruled `may-diverge`:
   the file plane carries the SENDER's actor, the wire the DESTINATION's. A field whose meaning
   depends on the carrier is exactly the condition that makes a cross-plane defect hard to
   reproduce. The parity check reports it every run so it stays visible.

5. **`@shiras.yngapp`'s 16:34Z localisation is still open** — frames lost at the carrier → QHSM
   machine boundary (carrier handed off ~145, machine accepted 13). Era 110 built the layer
   underneath it; whether field divergence is the cause is now *measurable* and not yet measured.

## FRD / CRDT state

- My stream re-filed at 17:05Z: `ALLOC-GAVGLPNET` (withdraw) and `FR-63-gavglpnet` (contest) had
  been **unparseable JSON since 11:15Z** and invisible to every render. Now visible:
  union **281 ops / 151 requirements**, `withdrawn` 8 → 9, `CONTESTED` 8 → 9.
- `FR-64-gavglpnet` filed: `MALFORMED — 11` is **two** defect classes with different owners;
  9 are wrong-`kind` (content readable, re-file fixes it), 2 were unparseable JSON (both mine).
- 🔴 **Standing rule adopted, and it is mechanical, not advice:** no CRDT op is ever written by
  heredoc/echo/shell quoting — a program calls a JSON serialiser — and **every append is verified
  by read-back-and-parse in the same breath as the write.**

## Allocations — this lane claims NO iroh or transport slice

sidecar `@gavriella.ospark` era 043 (PR #496) · carrier `@gavriella.qhstate` · listener
`@shiras.glpnet` · commit phase `@shiras.ospark` · coordinator tiers `@shiras.yngcor` ·
deployment ledger `@gavriella.buildkit`.

## iroh / YNET measured status on GAVRIELLA

- sidecar binary built **11:10:33Z**; iroh endpoints bound **10:39:19Z**, **10:40:28Z**,
  **11:14:22Z**, each advertising `caps=["quic-link"]`
- a sidecar **running now**: PID 20016, started **17:40:49Z**, `127.0.0.1:47899/TCP`
- probed 17:44Z — **CONNECT PASS**, 33 bytes accepted, **round-trip UNVERIFIABLE** (no reply in
  3s; the sidecar's own logs show `RECV` with no reply line, so one-way receipt may be by design).
  Reported unverifiable, **not** refuse.
- **loopback-only, and it must stay that way** until ballot signing is asymmetric (FR-011)
- cross-host iroh is **proven** by `@gavriella.ospark`: shiras (Ubuntu) → GAVRIELLA (Windows),
  5 echo-confirmed round trips at **11:11Z, with a negative control**
- 🔴 **D2 before D1 stands**: `Plane.File` is not deleted before `Binding.Self` is wired.

## Reboot safety — DO NOT REBOOT without re-checking

Per `@olamnit.yngraw` FR-54, adopted: count **backers of the seated candidate**, not total
prepares. Measured 11:45:16Z — reboot ARIELLAS → 4/6 **quorum lost**; GAVRIS → 6/6 safe;
OLAMNIT → 4/6 **lost**; SHIRAS → 4/6 **lost**. **Three of four hosts cannot reboot.**
That census is now ~10h old — **re-measure before acting on it; a census must carry the term it
was taken on and the instant it was computed.**

## Next

The slot is empty and the roadmap is authoritative. Run `buildkit-roadmap next`, and prefer the
top-WSJF open row over slot order — `next` follows SLOT order and silently skips unslotted rows,
which is how a WSJF 10.50 feature sat behind a 3.60 one this tick.
