# Curator report — meshtest-recure-ring, run 20260715T152146Z-0455

Task type `plan`. Method `method-20260715T152146Z-0455` (20 elements, frozen 20260715T154205Z).
Evidence pin **`02bcc20`** (isolated worktree). 4 blind Builders, pairwise-disjoint slices.
**113 claims / 0 unattributed / independence audit 6 inputs / 0 violations.**
Critic: **codex** (cross-provider, code-reading at the pin — NOT code-blind).
Verdict: **halted** at `cycle-1-curator-stop` — a deliberate stop, recorded as such, never a pass.

---

## 1. Cycle-1 adjudication (the thing this run was missing)

**96 CONFIRM / 14 REFUTE / 3 ESCALATE** over all 113 claims.

The cycle-1 Critic pass had never been run. It is run here with one deliberate calibration change,
because the previous planning-cycle Critic was miscalibrated: a **code-blind** role was asked to rule on
code facts, so its 13 ESCALATEs were *structurally inevitable rather than a verdict*. This Critic ran
with the repository at the pin as its working directory, read-only, and was told in terms that
"I'd have to read some code" is not grounds to escalate. Result: **3 escalates, and all 3 are
legitimately unresolvable** — two are design-intent judgements and one is a claim about what a Builder
personally assessed, which no repo read can settle.

## 2. THE META-FINDING — the absence claim is this run's dominant error mode

**11 of the 14 refutations are the same defect wearing different masks:** a Builder greps its OWN slice,
finds nothing, and reports an absence at REPO scope. The Critic, reading the whole tree, finds the thing
somewhere else.

This is **exactly how SG-2 fell** — and SG-2 was not an anomaly. It was the first instance of the run's
systematic defect, caught only because Builder-3 was honest enough to write "I could not verify whether a
multi-key anchor exists elsewhere; that is outside my slice." The synthesis already carried the warning
("treat every gap as true-within-the-slice until checked at repo scope"). **The adjudication measures the
warning's cost: ~10% of all claims, and they were concentrated in the load-bearing ones.**

Mechanism, stated plainly for the next run: **a blind disjoint-slice partition cannot establish an
absence.** It can establish a presence (a file:line either says the thing or it does not). An absence
claim is a claim about the complement of the slice — precisely the region the Builder was forbidden to
read. This is a **method defect, not a Builder failure**: M-18 (evidence discipline) demands file:line
evidence but never says an absence needs repo-scope evidence. Every Builder complied and was still wrong.

**For the next method: an absence claim MUST be either (a) scoped verbatim to its slice
("no X in <slice>"), or (b) routed to a repo-scope check that no slice owns.** Never phrased at repo scope
by a slice-blind role.

## 3. What FALLS — corrections that make the design CHEAPER, not dearer

The refutations run overwhelmingly in ONE direction: things the synthesis said **must still be written
already ship somewhere else**. Under M-01 (reuse, never a peer implementation) each is a *reuse* target.

| Synthesis claim | Repo-scope reality |
|---|---|
| SG-3: "no group-commit exists anywhere ⇒ **must still be written**" (M-05's mechanism) | **Ships.** `Mailbox/FileWriteAheadLog` — "Group commit = many appends, one fsync"; `DurabilityMode.FsyncEveryNms` + a group-commit window. True only of `DurableExecution/`. ⇒ **wire, don't write** |
| SG-3: "no CRC/checksum/frame-resume" | **Ships.** Mailbox WAL frames carry crc32; replay truncates torn records; `IWriteAheadLog` documents resume-after-crash |
| SG-3: "`FsyncAlways` cost/semantics unverifiable" | Semantics **verifiable** (`Flush(flushToDisk:…)`). The **cost is still unmeasured** — that half stands |
| SG-3: "~20–25 PGlite round trips per hop" | `initEdge` **memoizes** `_dbPromise`; DDL executes on first init only |
| ID-2: "dot-keying **unverifiable** — `PgliteOpWal` is out-of-repo at `$(GlpnetRoot)`" | **A corpus copy is IN-REPO**: `specs/056-…/corpus/internal/GLPNET/csharp/glp_crdtmsg/store/PgliteOpWal`. `Merge`→`Append`→`InsertOp` with documented ON CONFLICT dot idempotence |
| SG-2: trust stack single-device | `PeerSetTrustAnchor` + `PeerSetAmuletVerifier` (already self-refuted pre-adjudication) |
| C-5: "**no** signature/verify **of any kind**; frames cross in clear" | **Overreach.** The grep is right about *signature* vocabulary; `Link/Secure` ships a **pin-verified ECDH handshake + AEAD frame sealing**. Not a signature — but it demolishes "no verification of any kind" |
| C-1: "**TWO** mutually exclusive relay impls" | **Three.** `MeshRelayRoute` (DV-based) also exists. **See §4 — the conclusion is untouched** |
| T-2: "no membership-change API ⇒ ring cannot re-form" | `MeshNodeRuntime.AddNeighbor` / `RemoveNeighborAsync` ship; telemetry documents administrative neighbour removal |
| "no ring anywhere in-repo" | **`ring-5`** in `Tests/Mesh/DeterminismTests.cs:28`; **`ring-4`** in `Verification/MeshInvariantsTests.cs:35` — described as *"the in-repo mesh-invariant proof surface"* (K5, feature-015) |
| b1: "could not verify DI registration of `MeshNodeRuntime`" | The DI site is in-repo (`YngeniosRegistration.cs:261-273`); no registration exists ⇒ the null→`Unavailable` path is real |

**E-B — the one that hurts.** "The independence check can never fire on the auto path **by construction**"
was reached independently by the lead AND, blind, by Builder-4. **The Critic refuted it:** `EndorserKey`
is domain-separated from `HostIdentity`, but `wallet_id` is an **arbitrary string** accepted by
`ActorWalletBindingRegistry.BindAsync`, so string equality is **not structurally impossible**. Two
independent agreeing analyses were still wrong. *Agreement is not verification* — both read the same
derivation and neither checked the other side of the comparison.

## 4. What SURVIVES — the refutation of the ring design STANDS

The evidence base moved a lot; **the six refutations did not**. Where a claim's enumeration was wrong its
conclusion still holds, and this must not be over-read in either direction:

- **SG-1 (no delivery/receipt anywhere) — CONFIRMED.** `@mesh` is send-only (`mesh.send`/`mesh.res`, no
  inbound tag); `_pending` clears only at TTL. The ACK gap is real, and it is still the run's
  highest-leverage finding (RL-1).
- **C-1 / BLOCKER-3 — CONFIRMED, and *reinforced* by its own refutation.** The count was wrong; the
  mechanism was not. `MeshRelayRoute.Decide` decodes `(dest, src, hop, flags, inner)` — **there is no path
  or source-route field** — and forwards on `_router.TryNextHop(dest)`. A third DV-based relay is *more*
  evidence that consensus can DECIDE a ring while the transport cannot ENFORCE one.
- **C-3, C-4, C-6 — CONFIRMED.** Unmergeable dust; `PoolAudit` does not convict a forged mint; zero
  measured evidence.
- **BLOCKER-2 (identity regenerates per launch) — CONFIRMED.** In-memory only, never persisted.
- **BLOCKER-5 (`exp` is whole-day `DateOnly`) — CONFIRMED.**
- **Kill-one is proven for ROUTING ONLY — CONFIRMED, and sharper than the synthesis said.**
  `FailoverTests` builds a `FakeFabric`, cuts links, asserts `TryNextHop`/`Snapshot` cost — **no
  `SendAsync`, no inbox, no delivery assertion anywhere in the file.** Kill-one survival is established
  for route reconvergence, **never for message delivery across it**.
- **The O(rate×TTL) 50 ms retransmit scan — CONFIRMED**, with no multi-message/soak test covering it.

**All five blockers stand.** What shrank is SG-3 ("nothing built for 10⁶"): materially smaller than
stated, because the durability primitives ship and need wiring, not authoring.

## 5. The three ESCALATEs (engineer's, never the Curator's)

1. `a12fa60aa557` — whether the ledger's three-word wrap note ("already kernel-adjacent") *proves* the
   mesh conversion was not designed for a relay/ring use case. Design intent; unresolvable from repo text.
2. `6ebfd75c2289` — whether a next-hop-signed ACK corroborator **would** satisfy `ICorroborationSource`.
   The *absence* of a signature-verifying corroborator is checkable and **true**; the sufficiency of a
   hypothetical one is a design judgement. **This is E-B's load-bearing addition** — it needs a ruling.
3. `82b5e8470c80` — a claim about what a Builder personally assessed. Not repo-resolvable by construction.

## 6. Honest limits of this run

- **Cycle 2 was NOT run** (`converged=false`, `min_cycles=2`). Deliberate: cross-querying singletons would
  sharpen claims about a design refuted on six counts. **Recorded as a halt, not a pass** — the verdict is
  `halted` naming `cycle-1-curator-stop`, never `budget_stop` (no budget was exhausted) and never
  `converged`.
- **`0 corroborated / 113 singleton` is a phrasing artifact**, not weak evidence. Merge is set-ops over a
  normalised claim-TEXT hash (FR-003, "never a judgment call"); Builders 1 and 2 found the no-ACK gap from
  disjoint sources in different words, so the hash sees two singletons. Semantic agreement is invisible to
  the mechanical layer BY DESIGN.
- **Critic-integrity note, recorded rather than smoothed over.** In long sequential lists the Critic
  slipped `claim_id` labels: one 11-char id (a dropped leading char), two duplicated ids in b3 whose
  rationales demonstrably belonged to *other* claims, and one id absent from the corpus. Every id with
  ≠1 entry was **re-adjudicated in a focused pass** rather than guessed; unknown ids were dropped, never
  mapped by similarity. A file-overlap check across all 113 then caught **one surviving misalignment**
  (`dc3d019db9df` carried `FailoverTests` reasoning on a signature-absence claim, marked CONFIRM); it was
  re-adjudicated alone and came back **REFUTE**. The 6 remaining no-overlap decisions are all REFUTEs,
  where citing a different file is the point. **113/113 are covered with exactly one attributed decision
  each**; the assembler refuses to emit otherwise.
- The 4-batch split is a **token artefact**, not a method element: the Critic saw each Builder's claims
  with repo access, never the other Builders' claims.

## 7. What the next run must carry

1. **The absence-claim rule (§2)** — the single highest-value method change this run produced.
2. **M-13 must be AMENDED, not overridden** — the operator's PBFT directive raises the threat model above
   the frozen "REFUTE any BFT claim". **The method of THIS run cannot be amended** (`freeze-method` is
   append-only: *"method already frozen for this run — a re-plan needs a new run"*). The amendment is an
   input to the next run's method draft.
3. **Reuse targets, not build targets** (§3) — group commit, CRC/resume, `PeerSetTrustAnchor`, the in-repo
   `PgliteOpWal` corpus copy, `AddNeighbor`/`RemoveNeighborAsync`, and the **`ring-4`/`ring-5` in-repo
   mesh-invariant proof surface** the ring demonstrator can build on.
4. **The E-B escalate (§5.2)** — a ruling is owed before any auto-endorsed hop reward is designed.

— Curator, cycle 1

---
## Run footer

- run: `20260715T152146Z-0455`  verdict: **halted**  cycles: 1
- critic: codex
- terminal review: skipped — task_type=plan (terminal codexreview defaults to code only); no implementation exists to review - the design was refuted at cycle 1. The cross-provider codex Critic adjudicated all 113 claims at repo scope instead (96 CONFIRM / 14 REFUTE / 3 ESCALATE).
- ⛔ halted at: **cycle-1-curator-stop** (first failing gate halts the run)
