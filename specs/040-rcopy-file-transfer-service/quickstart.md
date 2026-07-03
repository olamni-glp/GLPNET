# Quickstart — Feature 040: Virtual 3270 Terminal (Complete & Hardened)

Prerequisites: the feature-036 C# host is built (`dotnet build csharp/glp_quick_host`) and `glp_quick` is
installed (`pip install -e glp_quick[dev]`). A shared cert exists (`glp-quick cert generate --out <dir>`).

## 1. Launch the terminal (US1 — type-only conversation)

```
# server
glp-quick --server --addr 127.0.0.1 --port 5920 --cert <certdir> --tui
# client (another host or process)
glp-quick --client --addr <server-ip-or-name> --port 5920 --cert <certdir> --tui
```

A splash appears, then the full screen. Compose in the command area; **transmit** by typing a line that is just
`//` then Enter. Everything is typeable — no function key required:

```
/help                 // → show help
/theme AMBER //       // → amber theme (purple/magenta command accent stays)
@bob good morning //  // → directed to peer "bob" (unknown name is reported, not redirected)
```

Piping stdin (no TTY) falls back to the plain line console automatically and still sends/receives.

## 2. Pages (US2)

```
/new NOTES //         // create a named page; edit it (Enter = newline, arrows move)
/transmit //          // send this page as a block; the peer receives it as a page owned by you
/pages //             // list pages with owner (me / peer name) and the current page
/next  /prev  /goto 2 // switch pages; a received page shows an OIA "new page" flag, no focus steal
```

## 3. Presentation (US3)

```
/theme //             // cycle GREEN→AMBER→WHITE→PAPER→COLOR
/layout two-strip //  // response strip above a command strip (or: /layout lines 3)
```
The OIA line shows mode · page X/N + name + owner · link state · the PF-legend (reverse-video blocks, each with
its typed equivalent).

## 4. Joint edit & forms (US4)

```
/joint on //                         // enable joint mode on the current page
/pin 3 5 "REVIEW" transient //        // counterpart overlays a framed comment; original saved
/undo-pin //                          // dismiss a transient pinpoint → original restored
/mask … / /fill …                     // define a form; the other side fills and returns it (labels intact)
```

## 5. REPL-in-a-page (US5)

```
/repl GOALS //        // spawn a page bound to a live GLP REPL over the link
append(a,b,X).  //    // entered on the page → evaluated over the link → result rendered on that page
```

## 6. `/rcopy` file transfer (US6 client + US8 responder)

Responder (target peer) configures its service once (typed, RDP-safe; writes `<data_dir>/config.json`):

```
/rcopy init docs D:/share/docs bob 1073741824 //   // root 'docs' at D:/share/docs, permit peer bob, 1 GiB quota
```

Client drives the transfer (each step is a typed argument; the offer is auto-queried, the outcome renders on a
page):

```
/rcopy @bob root=docs dir=D:/local/reports folder=july mode=synchronise exclude=*.tmp //
       // → offer-gated → local files gathered under dir, exclusion filter applied → manifest → per-file verdict
       //   → chunked transfer of the needed files → per-file outcome page
```

Result: a per-file outcome page — `transferred` / `skipped_identical` / `filtered_out` / `rejected(reason)`.
Files land under `<root>/xfer/in/<peer-name-and-UID>/<folder>/…`, never outside a permitted root. Delete the
responder's `catalog.json` and restart → it is fully rebuilt from `wal.log` (0 inventory loss).

## 7. Run the tests (US9)

```
pip install -e glp_quick[dev]
pytest glp_quick/tests -q
```

- **Host-free unit tests** always run (page model, protocol codec, `@name` resolve, no-TTY fallback, receive
  thread-safety, link-drop reporting, exclusion filter, WAL replay, synchronise, quota, joint, masks, keys).
- **Mesh-integration tests** run when `csharp/glp_quick_host` is built (skipped otherwise), covering cross-endpoint
  page transmit, `@name` delivery, `/rcopy` end-to-end, and REPL-page evaluation.
- A coverage-map test asserts every US1–US8 and SC-001..SC-012 has ≥1 asserting test (SC-013).

The two-host (SC-002 cross-host) and Remote-Desktop (SC-001 over RDP) criteria are approximated by the loopback/
mesh harness; the final acceptance adds a manual second-host pass.
