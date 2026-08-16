# Open decision register — ariellas / lane `glpnet` — 2026-08-16T19:15Z

Every open block requiring engineer input, plus every item originating from a tension, contradiction
or weakness in requirements or assumptions. Each carries background → tension → options with impact
→ recommendation.

**Closed since the 15:30Z register:** `glpnet#D7` (5-min timer — **RETIRED** by the engineer,
replaced by *stale heartbeat beyond the 120 s R10 lease **and** empty op log*) and `glpnet#D11`
(cold-start — **CONFIRMED** via ESC-1). The *scope consequence* of D11 remains open as **D14**.

Decision labels use the ruled `<lane>#<label>` grammar.

---

# TIER 1 — blocking other lanes right now

## glpnet#D1 · The C→G Section I failure

**Background.** 076 implement is complete; suite 549/550. The one failure is Section I
`link_both_ways` — `pc_integers [C→G]`, `bidirectional [C→G]`. The harness drives only `gleam run`
and the C# REPL; **the Dart type checker 076 changes is never invoked on that path**. Both runtime
artifacts unchanged (mtime 06/08). Reproduces standalone. Failure surface is transport
(`IOException: connection aborted`, `Got = []`), not semantics. What changed between green and red
was **host state** — concurrent suites, Git-Bash fork exhaustion, orphan kills.

**Tension.** 076's task **T014 is literally "both suites green vs T001 baseline (SC-002)"**. So the
*literal* criterion fails while every *substantive* one passes: A 221/221, B 112/112, C 51/51, plus
19 new unit tests.

| Option | Impact |
|---|---|
| **A. Run the isolated script on the clean host** | Minutes. Pass → SC-002 signs on a green run, 076 completes, **olamnit's 041 unblocks**. Fail → real defect, own feature, 076 decoupled |
| **B. Sign SC-002 on section-level attribution** | Zero time. Puts a signed zero-regression claim over a red suite **on record as precedent** — the false-green class this programme exists to kill |
| **C. Hold indefinitely** | Two lanes stay blocked |

**Recommendation: A.** Only option producing evidence rather than judgment; costs minutes; host
verified clean. Caveat: claims `glp_gleam/build/` for the Windows Erlang leg — a later WSL
`gleam test` needs `rm -rf glp_gleam/build`.

## glpnet#D2 · §1.14 callee-end

**Background.** You approved occurrence-pair licensing 2026-08-12 — the **caller** end. Its own
rationale says caller and callee are *"two ends of one binding channel"* and the checker models the
callee end but not the caller. A callee-end shape then surfaced that the head rule rejects.

**Tension.** The canonical callee-end shape is **`self.glp`'s own `X? = X.` under
`procedure =(_?, _).`** — the prelude relies on a construction user code cannot express. But the
approved proposal deliberately chose the *"smallest sound extension"* and declined the symmetric
case for lack of evidenced shapes. **And `DISCIPLINE §18.4` states `-mode(system)` does *not* exempt
a module from type checking**, which sits badly with a system-only privilege.

| Option | Impact |
|---|---|
| **A. License the head combination generally** | Symmetric, one rule both ends. Widens acceptance beyond evidenced shapes; contradicts the approved "smallest extension" principle |
| **B. `self.glp`-only privilege** | Minimal; user code unchanged. Makes explicit a **two-tier language**; in tension with §18.4 |
| **C. Require a written proposal with evidenced shapes first** | One round-trip |

**Recommendation: C, then A or B.** The caller-end decision was good *because* it was written up
with a worked example and an explicit non-licensing clause. B in particular needs §18.4 reconciled
explicitly, not by implication. **Gates olamnit's occurs-check FR-002**, correctly left OPEN
selecting neither.

## glpnet#D3 · 066 US4 — a structurally unsatisfiable gate

**Background.** T015 requires *"one grammar, ≥2 targets (Dart + C# minimum)"*. olamnit's 069 spike is
ADOPT-WITH-CONDITIONS, SC-001 7/7, SC-003 10 000-case fuzz, 0 un-caused divergences. **BC-3: parity
C#-only, Dart unmeasured → T015 unmet. BC-4: Gleam is not an ANTLR target.**

**Tension.** US5 is the Gleam chain, gated on `T016`-go. **BC-4 means no ANTLR outcome can ever
produce a go that helps Gleam.** The gate cannot be satisfied by any result; waiting is futile and
re-running the spike cannot help.

| Option | Impact |
|---|---|
| **A. Amend: add a Dart parity leg + re-cut `T016 → T017`** | Honest fix; satisfies T015 legitimately, frees US5 from an unreachable dependency |
| **B. Drop ≥2 targets to 1** | Cheap; lowers an acceptance bar that existed for a reason |
| **C. Re-scope US5 off ANTLR** | Larger surgery, possibly correct long-term |

**Recommendation: A, authored by gavriella** — she found it, holds custody, and has declined to
attempt it without a ruling. **Explicitly not a second C# spike** (duplicates evidence in hand).

---

# TIER 2 — unsafe actions, currently stopped

## glpnet#D4 · `/bk-close` targets the wrong feature

`.specify/feature.json` names **076**. `/bk-close` resolves its subject from that file and never
verifies it is the requested one (F1 instance 15). Closing anything else runs against 076 — which is
incomplete and ineligible — and **exits clean**.

**Recommendation:** procedural fix now (repoint, verify by re-read); defect filed for the real fix.
**No `/bk-close` until in place.**

## glpnet#D5 · `buildkit ship` vs `glpquick-cert/`

Present, gitignored, **destroyed three times** by `buildkit ship`; checkout mechanism REFUTED,
blanket `git clean` REFUTED, **cause unidentified**; jKMV has not landed.

**Recommendation: no ship from this host until jKMV — cost is currently zero** because nothing is
eligible (D6). Integrity baseline recorded (sizes + sha256, no key material copied). If something
becomes eligible, out-of-repo backup first.

## glpnet#D6 · 064 T041 — a task whose outcome already exists

`064-post-wave-gap-closure` is the **only** feature meeting implement + codexreview
(`t040-codexreview.md`, run `20260803T214953Z`). Sole open task **T041 = "ship then /bk-close"**.

**Tension.** Its roadmap row is **already `closed`**. T041's stated outcome is achieved; executing it
re-ships a closed feature and incurs D5 risk **for no state change**.

**Recommendation: tick T041 as already-satisfied, with evidence recorded.**

---

# TIER 3 — contradictions in requirements and assumptions

## glpnet#D8 · Four `ariellas` lanes, one identity — **now a 4th instance**

glpnet, buildkit, tefl, yngenios-windows/qhstate all run as `ariellas`. Two have filed false-absence
claims; one **lost `status/ariellas.md`**. This round gavriella asked "@ariellas" to ACK holding
P-core — **accepted at `122734Z`, not one of my stamps** — and her own table names the owner as
`ariellas/yngenios-windows`. **An ask addressed to a host has no routing.**

**Recommendation: extend the just-ruled `<lane>#<label>` grammar to addressing —
`@<host>/<lane>`, never bare `@<host>`.** Scope to messages and status files; **not** board actor
ids, where `glpnet#Q7` (one host, one id) should stand or the allocator re-divides capacity.

## glpnet#D9 · Ownership declared but unenforced

`dispatch.py:156` — `ownership={}`, with two further statements that it is a non-binding default.
`declared_owners_on_this_board` is metadata **no code path enforces**.

**Tension.** A lane that *reads* it and self-restrains is constrained; a lane that ignores it is not.
**The honest lanes are the only ones bound** — strictly worse than no boundary at all.

**Recommendation: enforce it or remove the declaration. Not "document as advisory"** — that is the
current state and it is the worst of the three.

## glpnet#D12 · Coordination overhead

A session producing 15+ channel documents and advancing zero features. The R1–R4 contract mandates
per-feature ACKs, WIP lines, completion reports and filename handshakes, reciprocally, across three
hosts.

**Recommendation:** drop *"a round with nothing to report still gets a note"*, collapse R1/R4 into
one document per host per round, and **report the coordination ratio** so overhead is measured
rather than assumed benign.

---

# TIER 4 — newly surfaced this round

## glpnet#D13 · `roadmap import` crashes on its own status value

`importer.py:658` emits `"skipped_untrusted"`; `:603`, `:657`, `:874` use `"skipped_invalid"`; the
CHECK constraint knows only the latter. **glpnet's import is broken** — it aborts on the first
untrusted file, so no peer roadmap content can be imported here.

**Tension — and this is the real finding.** After the traceback, `reconcile` said *"already in
sync"*, `dedupe` reported 0 groups, **`replay --verify` returned ✓**, and `export` wrote cleanly. **A
reader seeing only summary lines records a successful round.** Fourth instance today of a step
failing while its neighbours report success.

**Recommendation:** ~1 line (add the value to the constraint, or emit `skipped_invalid`). **Separately
and more importantly: the sync chain must fail closed** — a crashed import must make the round fail,
not let `replay --verify ✓` stand as the round's headline.

## glpnet#D14 · Cold-start is confirmed — is it in the superset's scope?

ESC-1 **confirmed H-COLD-START**. Independently measured here 14 Aug: no WP has ever completed → no
actuals → PERT cannot estimate → held out → never ready → never allocated. Verified by experiment:
after refreshing calendar and heartbeat (`stale=3 → stale=2`), the allocate view still read
`blocked: []`, `proposals: []`, `ready_undispatched: 0` — **nothing reached the allocator at all**.

**Tension.** The remediation programme (`yngenios-windows#D1` additive writers, `#D2` capacity
mechanism) is **downstream** of the admission gate. **Neither starts the glpnet stream.** A
remediation landing both and reporting the stream fixed would be a false-green over uncovered scope —
the exact shape F1 exists to kill.

| Option | Impact |
|---|---|
| **A. Add the admission gate to the superset scope** | Honest; the only route to actual flow |
| **B. Seed PERT priors so a no-history WP gets an estimate** | Narrow, cheap, unblocks admission specifically |
| **C. One manual transition to create the first actuals** | Cheapest; bootstraps the estimator, one-off |

**Recommendation: C now, A for the durable fix.** C is a single operator act that breaks the circular
dependency; A ensures the spec does not claim a fix it does not deliver.

## glpnet#D15 · `row_version` is not discoverable — the tool forces a destructive probe

gavriella wrote a literal `PROBE` into the notes of **064 and 077** while probing `--expect-version`,
and restored both byte-identical from an export minutes old. **Recovery was possible only because a
fresh export happened to exist.**

**Tension.** `status --json`, `brief` and the export journal **all omit `row_version`**, so every
editor is *forced* to probe — and probing is inherently destructive on a compare-and-set write.
**The tool structurally requires a destructive act to perform a safe one.**

**Recommendation:** surface `row_version` in `status --json` and `brief`. Until then the interim rule
stands: **probe only with the intended final value.**

## glpnet#D16 · The board has a permanent phantom WP

glpnet's board moved **6 → 7** — its only change in four days — because a `note --wp <free-form>`
minted a phantom WP. **`glpnet#D4`-class no-reverse-gear means nobody can remove it.**

**Tension.** The board's sole delta in four days is an artefact of a defect. **A board whose only
movement is self-generated is not reporting work — it is reporting itself.** Same class as the
064/077 walk-back that could not be executed (no demote verb) — filed against
`multi-host-state-discipline-reversible-states-…` (WSJF 3.00).

**Recommendation:** adopt the fleet rule already in force (*no `note --wp <free-form>`*), and treat
**reverse-gear as a first-class requirement** in that feature — board *and* roadmap both lack it.

---

## Summary — what needs a word from the engineer

| # | Decision | Cost of delay |
|---|---|---|
| **D1** | Run the C→G script? | **076 + 041 both blocked** — needs only "yes" |
| **D2** | §1.14 callee-end: A / B / write-up first | olamnit FR-002 blocked |
| **D3** | Authorise gavriella to amend 066 US4? | gavriella wave6 blocked |
| **D4** | Confirm no `/bk-close` until feature.json repointed | latent wrong-target close |
| **D5** | Endorse the ship hold until jKMV | zero today |
| **D6** | Tick T041 as satisfied? | zero |
| **D8** | `@<host>/<lane>` addressing? | mis-routing, 4 instances |
| **D9** | Ownership: enforce or remove? | constrains only honest lanes |
| **D12** | Cut the coordination contract? | ongoing overhead |
| **D13** | Who owns the importer fix? | **all import broken here** |
| **D14** | Admission gate into superset scope? | remediation may claim an undelivered fix |
| **D15** | Surface `row_version`? | forced destructive probes |
| **D16** | Reverse-gear as a requirement? | permanent phantom, permanent overstated rows |

— ariellas · lane `glpnet` · W4 · 2026-08-16T19:15Z
