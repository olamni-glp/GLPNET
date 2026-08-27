# Curator report — durable fix synthesis for the SHIRAS defect class

Run `20260825T112749Z-29ff` · task-type `plan` · 3 blind Builders · Critic on **codex**
(cross-provider, `independence_warning: false`) · method `method-20260825T112749Z-29ff`
(19 elements, **0 refutes outstanding**, frozen after one red-team round).

## Headline

**No proposed fix component is DURABLE, and the reason is uniform: every one of them is OPTIONAL.**

Ten components were proposed across three blind builders. The Critic adjudicated all ten conflicts
in favour of the one builder holding the live CLI contracts, and the ruling is the same each time:

> *"The contract does not require callers to perform it, so unaware callers remain unprotected."*
> *"A verified but optional transport remains a remember-to-use mitigation rather than an enforced safeguard."*

Under the frozen method's own gate — `HUMAN-MEMORY-ONLY` forces `NOT-DURABLE` — this is a
**negative result, honestly reached**: the fix for this defect class **cannot be a practice, a
convention, or a checklist.** Those are exactly what already failed twice in this fleet
("remember to pass `--root`"), and what failed again today across nine lanes.

## Merge

353 claims parsed, **0 malformed**, 171 distinct `(SUBJECT, DIM, SCOPE)` keys:

| | |
|---|---|
| **CORROBORATED** | **55** |
| SINGLETON | 106 |
| CONFLICT | 10 |

Independence audit: 3 builders, **0 violations**, sibling-content checks exercised.
Coverage: **17/17 sweep cells from every builder (1.0)**.

**The closed vocabulary worked.** 70 of 122 keys were reached by two builders before the third even
landed — byte-identical token halves from blind builders reading disjoint files. Contrast the
earlier run on this repo, where subject-spelling drift made packet-level corroboration structurally
impossible and produced a false 0-corroborated reading.

## The ten conflicts — a single, informative signature

Builder-3 alone could see **which verbs actually exist** and **what the hard constraints forbid**.
It systematically disputed durability where builders 1 and 2 (host facts, failure history) affirmed
it. The Critic sided with the contracts in all ten.

| component | ruling |
|---|---|
| `CMP-SSH-FIRST-PROBE` | **NOT-DURABLE** — reachability is preconfigured but its use is not mandatory |
| `CMP-TWO-TRANSPORT-CROSSCHECK` | **NOT-DURABLE** — detects the disagreement, but nothing requires it |
| `CMP-TRANSPORT-PROVENANCE-STAMP` | **NOT-DURABLE** — the field is optional; unstamped ops remain possible |
| `CMP-NEGATIVE-CAPS-TOKEN` | **NEGATED** — consumers unaware of the convention still read the positive declaration |
| `CMP-UNC-ROOT-REFUSAL` | **NEGATED** — holds on current engines; **version skew lets older engines still fork a board** |
| `CMP-RETRACTION-OP` | **UNSUPPORTED** — no withdrawal verb exists; grow-only LWW cannot compose one |
| `DEF-SWEEP-EXISTING-DIRS` | **ELIMINABLE-EXISTING-VERBS** — `bk-flow lanes` exposes the declared registry |

## What this means for the fix

**One component survives as buildable from existing verbs:** enumerate intended recipients from the
declared lane registry (`bk-flow lanes`) rather than iterating directories that already exist.
That eliminates the delivery defect two lanes hit independently.

**Everything else routes to `REQUIRED-UPSTREAM-CHANGE`** — and that is the method working, not
failing. A durable fix must be **enforced by the tool**, not remembered by the operator:

1. **Capability polarity + retraction** — a declaration that cannot be withdrawn decays into fiction.
   Belongs with crucible's ruling **Q-041-01**; this lane will consume that contract, not build a rival.
2. **A platform kind in the capability vocabulary** — `linux-host` was declared correctly at
   07:46:46Z and no matcher reads it, because there is nowhere correct to put it.
3. **Mandatory provenance derivation** — optional stamping does not survive an unaware caller, and
   the substrate has no VCS, no usable uid, no usable PID and no usable line endings.
4. **Root identity** — but **NOT YET**: *"you cannot verify convergence safety without the identity,
   and you cannot safely stamp the identity without verifying convergence."* Engineer-gated.

## Open escalations — ENGINEER's, not resolved here

- **E20** — which fix components are in scope. The ten are a non-exhaustive starting vocabulary;
  every builder extension is listed rather than silently adopted.
- **E17** (equality measure) and **E28** (SHIRAS disposition) remain open from the prior run. E28 is
  **materially changed**: SHIRAS is measured as an active participant, not a provisioning candidate.

## Status

Stopped at the **budget gate** (`warn_confirm`, 620k vs 400k) before cycle 2. Cycle 2 would run a
directed pass at the 106 singletons — where the non-conflicting component evidence sits. Residual
state is persisted.

## The finding I would keep above all others

Nine lanes, six dead detectors, ~20 retractions across the fleet, and the answer
(`skill: linux-host`) sat declared in the primary record the whole time. **Before inferring a fact
about a peer, re-read the primary record.** And: **disjoint slices do not protect you when the
collection method is uniformly wrong — they corroborate the artefact and dress it in a
corroboration count.**
