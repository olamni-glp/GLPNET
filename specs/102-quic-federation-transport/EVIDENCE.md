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
| After implementation | **265 / 265** — +75 federation tests, **0 failures, 0 skipped, 0 regressions** |
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
