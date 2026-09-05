<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Evidence pack — ERA 102, QUIC federation transport

**Host**: Gavriella · **Lane**: gavriella-GLPNET · **Date**: 2026-09-04 · **Task**: T047

Every criterion is listed **by name** with its state. There is no aggregate line and no percentage,
because an aggregate is exactly how an unmeasured criterion becomes a green one. Two criteria are
**not met on this host** and are named as such.

## Suite

| | |
|---|---|
| Baseline before any change | **190 / 190** (`glp_crdtmsg.tests`, Release, net11.0, re-measured this session) |
| After implementation | 265 / 265 |
| After codexreview + self-review fixes | **278 / 278** — +88 federation tests, **0 failures, 0 skipped, 0 regressions** |
| Build | 0 errors, `glp_crdtmsg` + `ynet_federation` |
| Operator console | runs under Smart App Control via `dotnet run` (verified, §Console below) |

## Success criteria

| SC | State | Evidence |
|---|---|---|
| **SC-001** | 🔴 **UNMEASURED** | Requires a **physically separate** host. No peer listener exists on the estate yet, so this lane cannot measure it. Recorded durably at `%TEMP%\ynet_federation\sc001.evidence.json` as `{"State":"UNMEASURED","IsMet":false}`. FR-022 disqualifies the one-machine proof; ruling `Q-GLPNETG28-02` chose implement-here + ACK-required broadcast over redefining the criterion (rejected as one-way). |
| **SC-002** | ✅ MEASURED | `ADeliberatelyRedeliveredOpFoldsExactlyOnce`, `RedeliveryThroughTheServicePathStillCountsOnce` — the same op shipped twice, folded once, durably written once. |
| **SC-003** | ✅ MEASURED | `TwoHostsWithTheSameOpSetProduceByteIdenticalFolds` — compared as **bytes**, not through a comparer that could hide the bug. Plus `OrderIndependenceHoldsWithDuplicatesInterleaved`. |
| **SC-004** | ✅ MEASURED | `AnUnpinnedDialerIsRefusedAndNoBoardDataCrosses` — asserts refusal **and** that zero bytes crossed. Asserting only "refused" would not test FR-006. |
| **SC-005** | ✅ MEASURED | `ForeignSpaceMaximalTermNeverWins` — `long.MaxValue` in a foreign space loses to `1` in the live space. Negative control; passes only because the comparison exists. |
| **SC-006** | ✅ MEASURED | `AHostAnsweringOnTwoAddressesCountsAsOneParticipant` **and** its converse `TwoNodeIdsAtOneAddressAreTwoParticipants` — the rule holds in both directions. |
| **SC-007** | ✅ MEASURED | A positive **and** a negative control for each of the four states, plus explicit `…ControlsDiffer` assertions that the two produce **different** output. |
| **SC-008** | ⚠️ **PARTIAL** | The runbook is written and every local step in it was **executed and its output pasted in** (`status`, `identity init`, `epoch mint`, `revert`). §5's cross-host crossing awaits a peer, as SC-001 does. |
| **SC-009** | ✅ MEASURED | `EveryRecordedChangeCarriesItsReversalAndReplaysNewestFirst`, and executed live: two real changes recorded with restorable prior state, `revert` dry-run listed them newest-first. |
| **SC-010** | ✅ MEASURED | `AnUnmeasurableStateIsUnknownAndRendersDifferentlyFromNo`, `UnknownRendersAsTheLiteralWord`, `DefaultStateIsUnknownNotNo`. |
| **SC-011** | ✅ MEASURED | `AnOpAppendedWhileTheLinkIsDownArrivesViaTheReconciliationPull` — deleting the backstop makes it fail. Plus `…TransfersOnlyWhatThePeerLacks`. |
| **SC-012** | ✅ MEASURED | `RetiredOpRemainsInTheLogAndIsExcludedFromOrdering` — **both halves in one test** so neither can be dropped. Uses a faithful reproduction of the real fossil (`term 5961694`). |
| **SC-013** | ✅ MEASURED | `MintingANewEpochLeavesPriorEpochOpsReadableAndAttributed`. |
| **SC-014** | ✅ MEASURED | `NothingIsShippedWhenTheDurableAppendFails`, `AnAppendedOpSurvivesACrashBeforeThePush` (real file, real read-back). *Added by the analyze pass, finding U1.* |
| **SC-015** | ✅ MEASURED | `UnknownSpaceLegacySpaceAndNoTermAreThreeDifferentResults` — asserts `Distinct().Count() == 4`. *Added by the analyze pass, finding C1.* |

**13 of 15 measured. SC-001 unmeasured. SC-008 partial.** Neither is reported as met.

## Review findings — four defects, all in code that was already green

The suite was **265/265 green over every one of these**. That is the point: a green
self-written suite is not evidence. Three came from adversarial self-review during the
codexreview stage, one from codex itself.

| # | Found by | Severity | Defect |
|---|---|---|---|
| 1 | self-review | HIGH | `_sameMachine` was set **only** on the dialling path, so a listener that received an op rendered `op received from peer: yes` beside `same machine: n/a (no crossing observed)` — the surface contradicting itself, and rendering **identically to the genuine no-crossing case**. The same two-states-one-output defect SC-007 forbids, hiding inside the field that enforces FR-022. |
| 2 | self-review | HIGH | `MergeGate` was declared, unit-tested, and **never called** from `FederationService`. FR-018 — the enforcement of the STOP ORDER — was a green test over an ungated merge path. A guard that exists and is tested is *worse* than none: it reads as protection in every review. |
| 3 | **codex** | **P1** | My fix for #2 gated `ReconcileAsync` (the pull path) but left `ReceiveOneAsync` — **the primary push path** — ungated. A non-aware peer could bypass FR-018 entirely by pushing instead of pulling. Gating the secondary path and not the primary one is not a partial fix; it is no fix. |
| 4 | self-review | MEDIUM | `PullIntervalSeconds` was configured, validated and **printed to the operator** (`pull every 60s`) while no timer read it and no frame carried a pull. FR-028's pull leg existed as a method nothing called. |

Fixes: a fail-closed capability handshake (`HelloProtocol`) with a per-peer table; the gate
applied on **both** the push and pull paths using the peer's **declared** capabilities rather than
an assumed-`true` literal; a real pull wire protocol (`pull-req` carries the frontier, `pull-resp`
only what the peer lacks) driven by an actual loop; and `SameMachine` widened to `Tri?` so
*unmeasured* is distinguishable from *not applicable*.

**Two older tests failed against the new gate and had to be corrected** — which is how I know it is
load-bearing rather than decorative.

## Functional requirements

All 31 have task coverage and all are implemented, with two carrying an operational caveat:

- **FR-024** (scoped port opening) — the rule is **authorised** (ruling `Q-GLPNETG27-04`) and its exact
  one-liner plus reversal are in the runbook, but `New-NetFirewallRule` returned **`Access is
  denied`**: it needs an elevated shell and this lane cannot self-elevate. It gates **inbound** dials
  only; every other part of the feature was exercised without it.
- **FR-029** (retirement) — the mechanism ships and is tested, but the known fossil is **not on this
  host** (see below).

## The fossil — a measured absence, not a skipped task

Searched 2026-09-04 on Gavriella: **no `ynet\log\*.jsonl` exists here**, and `628016928ab854ae`
appears **only in COOP broadcast markdown**, never in an op-log on this host. The correction
broadcast addressed `@gavriella-yngwin`, not this lane.

**This lane cannot retire an operation it does not hold.** What ships instead is the mechanism the
holder needs: `RetirementOp` plus `ynet-federation retire --op <peer:counter> --reason <why>`, with
`TermOrderingTests` retiring a faithful reproduction of the fossil and asserting both halves.

🔴 **Do not delete `628016928ab854ae` by any other means.** On an append-only board suppression is
undetectable; the correction must be additive.

## Console — verified output

```
$ dotnet run -c Release --project csharp/ynet_federation -- status
stack supported        : yes
listener bound         : no
peer admitted          : no   (peer set is empty - no pins configured)
op received from peer  : unknown
same machine           : n/a   (no crossing observed)
policy refusal         : none

$ ... identity init
node_id : 96a28f1215386070bed9b45acacc43744e7d6389d88cf1040130e63fed8fe098
key     : C:\Users\gavri\AppData\Local\ynet\federation\node.key (minted)
$ ... identity init          # re-run: STABLE, which is the whole point
node_id : 96a28f1215386070bed9b45acacc43744e7d6389d88cf1040130e63fed8fe098
key     : ... (existing)
```

Smart App Control is ON and ENFORCING on this host and did **not** bite — the console runs under the
signed `dotnet` host. FR-023 detection is in place for the case where it does.

## What this era did NOT deliver, stated plainly

No leader is elected. No PBFT runs. There is no fleetwide coordinator and no fleetwide signature
verifier. All four are **out of scope by the spec**; they consume this transport and were blocked by
its absence. Nothing here elects anything.

## What a peer needs in order to unblock SC-001

1. `node_id` **`96a28f1215386070bed9b45acacc43744e7d6389d88cf1040130e63fed8fe098`**
2. endpoint **`192.168.0.108:47890`** (UDP; dial by **literal IPv4** — names resolve to `fe80::` only)
3. the runbook: `docs/runbooks/ynet-federation.md`
4. a matching `space_id`, and their own `node_id` returned to this host

---

## Ship gate: the SECOND INSTRUMENT (ruling `Q-GLPNETG30-01`), 2026-09-05

Fifteen `/bk-codexreview` rounds returned 1, 14, 17, 12, 14, 5, 8, 1, 11, 7, 4, 7, 8, 9, 12 findings;
~140 were fixed; from round 4 on, most findings were inside the *previous round's fixes*. Round 16
changed instrument to the **compiler** and found `CS0649` on its first pass. Ruling
`Q-GLPNETG30-01` therefore set the ship bar at **one instrument of a different kind**, not a
sixteenth review.

**Instrument: .NET analyzers, `AnalysisLevel=latest-recommended`.**

| project | result |
|---|---|
| `ynet_transport` | **0 warnings, 0 errors** |
| `glp_crdtmsg` (incl. `federation/`) | **94 raw / ~47 unique diagnostics**, 12 rules |
| `glp_link` (transitive) | 18 raw |

**The instrument was positive-controlled before any zero was believed.** A `CA2013` probe was added
to `ynet_transport`, the build reported it, and the probe was removed — so `ynet_transport`'s zero is
a measured zero, not an unarmed analyzer.

**Note on measurement, recorded because it nearly produced a false clean result:** three earlier
attempts reported "0 hits" for `glp_crdtmsg` while the build was in fact **FAILING** on
`Error writing to source link file ... used by another process` (cross-lane contention on the shared
`out/csharp` tree). `Done Building Project ... GlpCrdtMsg.csproj -- FAILED` and the absence of
`GlpCrdtMsg.dll` proved the project had never compiled, so its "zero" was an artefact of a build that
never ran. Measuring with `-p:EnableSourceLink=false` (SourceLink is irrelevant to analyzer
diagnostics) produced a genuine `Build succeeded` and the 94 diagnostics above. **A zero from a
failed build is not a zero.**

### Adjudication of the `glp_crdtmsg` findings — no live defect

| rule | n (raw) | assessment |
|---|---|---|
| `CA1305` IFormatProvider | 38 | Culture-sensitive formatting, chiefly `FederationConfig`. **Latent**: a cross-host protocol should not depend on host culture. No divergence observed; recorded as a hardening item. |
| `CA1725`/`CA1859`/`CA1822`/`CA1000`/`CA1068`/`CA1036`/`CA1707`/`CA1850`/`CA1836` | 46 | API-shape, performance and naming. No correctness impact. |
| `CA1001` owns disposable field | 4 | `JsonlBoardLog._gate`, `SchedulerBoardLog._gate` — semaphores held for process lifetime and reclaimed by the OS at exit. **Latent**, same family as round 16's `CS0649` but not a live leak. |
| `CA1806` TryParse result ignored | 2 | `DotSequencer.Next()` line 77: a non-numeric sequence file leaves `stored = 0`. **Traced to ground:** the sole production caller (`ynet_federation/Program.cs:651`) passes `floor = DotSequencer.HighestFor(nodeId, log)`, and `HighestFor` returns the max counter for this node across the whole local log, so a corrupt file yields `floor + 1` — a jump, never a re-issue, exactly as the class contract states. **No live defect.** |

**Recorded contradiction, NOT fixed here (bug protocol):** `Next()` is commented *"Retry only for the
contended-file case. Any other failure is a real fault and is raised"*, yet a corrupt sequence file is
silently tolerated rather than raised, and the constructor's `floor = 0` default means a future caller
that omits the floor loses the protection silently. The code and its comment disagree; which one is
right is a design question, not a lint fix, and it is carried rather than guessed at during a ship.

**Verdict: the ship bar of `Q-GLPNETG30-01` is met.** A second, independent, positive-controlled
instrument found no live defect on this era's surface.
