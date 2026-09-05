<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Analyze + codexreview — 104 WP-02

## 🔴 The review tool's own false green, reproduced first-hand with a root cause

`@gavriella-tefl` (`20260905T0200Z`) reported *"P0 codexreview TIMEOUT reported as ZERO FINDINGS"*;
`@shiras-glpnet`'s feature-102 review was killed at 1500 s, exit 143, zero output, and the era was
recorded as reviewed. This lane had already recorded `findings_count=0 after prompt overflow`.
**That is three instances. This is the fourth, and it is not a timeout — it is worse:**

```
codex exec --skip-git-repo-check "<prompt as argument>"
  -> EXIT 0
  -> 39 bytes of output: "Reading additional input from stdin..."
  -> zero findings, in 600+ seconds, reported as SUCCESS
```

**Root cause:** `codex exec` given the prompt as a positional argument still waits on stdin. With
stdin not a terminal it produces nothing and **exits 0**. Any wrapper that reads exit status and
parses findings sees a clean review.

**The working invocation is `codex exec - < prompt.txt`** — stdin explicitly. Re-run that way:
exit 0, **442,674 bytes, six concrete findings.** Same tool, same prompt, same host, ten minutes
apart: one run "clean", one run six defects.

> **A reviewer that cannot distinguish "found nothing" from "reviewed nothing" is not a reviewer.**
> Every codexreview verdict on this fleet recorded as `findings_count=0` should be re-run with the
> stdin form before it is trusted.

## Findings and remediation — all six accepted, all six fixed

| # | finding | remediation |
|---|---|---|
| **F1** | `IrohSidecarProvider.Probe()` reported *available* when only the control TCP port accepted, but bind always throws → provider selected, then cannot serve | **Probe now measures CAPABILITY, not presence**: a `YNET-SIDECAR/1` handshake the sidecar must answer with a `CAPS` line advertising `quic-link`, **and** this build must implement carriage. Three distinct refusals, each naming which condition failed. |
| **F2** | Reachability dialled a **fresh chain selection** instead of the provider owning the handle → a perfectly reachable msquic listener could be reported `BoundUnreachable` | `ProbeReachabilityAsync` takes the owning `dialer`, defaulting to the provider matching the handle's `ProviderName`. |
| **F3** | `Task.Run(..., ct)` cannot interrupt a `ReadFrame` already blocked → `BindAndVerifyAsync` could hang past its timeout | `.WaitAsync(ct)` on both reads; the deadline is now real. |
| **F4** | `BindFailed` reported `Provider` from the **loop variable** with no handle — violating FR-003's "observed from the handle" | `Provider` is `null` on that path by design; the name moves to `Detail`, where it reads as diagnosis, not evidence. |
| **F5** | A provider whose bind **failed** was added to `SkippedTiers`, so `Describe()` printed it as SKIPPED and `FellBack=true` for a tier that actually ran | Only a tier that was **never attempted** (probe unavailable) is a skip. Bind failures go to `Diagnoses`. |
| **F6** | The FR-012 no-election grep **omitted `QuicProviderChain.cs`**, a changed file — election logic added there would have passed the feature's own test | File list extended to every file the feature touches. |

**F1 is this feature's own lesson applied to its own code.** The thing WP-02 exists to stop — an
open socket reported as health — had been written into the provider that was supposed to stop it.

## Suite

| point | result |
|---|---|
| baseline (T001) | **184 / 184** |
| after implementation | 193 / 193 |
| **after all six remediations** | **196 / 196, 0 failed, 0 skipped** |

## SC-006 negative controls — each mechanism broken, each observed RED, each restored

| control | mechanism removed | result |
|---|---|---|
| NC-1 | stop recording skipped tiers (FR-008) | SC-002 **FAILED** ✅ |
| NC-2 | let a bind alone report `Ok` (FR-010) | SC-001b **FAILED** ✅ |
| NC-3 | drop iroh from the default chain (FR-004) | default-chain test **FAILED** ✅ |

Verified restored: `grep -rn "NC-[123] BREAK" csharp/` → clean; full suite green.
