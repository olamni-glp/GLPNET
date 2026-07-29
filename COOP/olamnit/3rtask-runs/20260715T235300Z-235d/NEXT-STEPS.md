# NEXT STEPS — run `20260715T235300Z-235d` (method FROZEN, execution phase not yet started)

Written 2026-07-16 as a safe-restart point. This file is **notes to the next session**, not an artifact
the 3rtask CLI reads.

## State

- Method **FROZEN**: `method-20260715T235300Z-235d`, **20 elements**, 19 CONFIRM + 1 accepted ESCALATE (M-34).
- 5 builders · 5 slices · critic=**codex** · min_cycles=**2**, max_cycles=3 · token_budget 600k.
- `run.json` shows `verdict=aborted, cycles=0` — that is the **pre-verdict default for an open run**, not a failure.
- Previous run `20260715T152146Z-0455` is **CLOSED**: `verdict=halted`, `halted_at=cycle-1-curator-stop`.

## Environment (nothing is on PATH)

```powershell
$env:PYTHONPATH = "C:\Users\smbuser\AppData\Local\buildkit\deploy-home\versions\2026.07.10.1\src"
# ^ ONLY this version has the `threerole` module. Always pass --project-root <the worktree>.
$PR = "D:\bstdev\research\olamnit-wt-ring"
$RUN = "20260715T235300Z-235d"
```

## Step 1 — compose the execution briefs (ONE compose per phase per run — append-only)

```powershell
C:\Python314\python.exe -m buildkit_cli.threerole brief --project-root $PR --run $RUN --phase execution
```

Then **`audit-independence`** (read-only SC-003 check) before running any Builder:

```powershell
C:\Python314\python.exe -m buildkit_cli.threerole audit-independence --project-root $PR --run $RUN
```

## Step 2 — run the 5 blind Builders

Each Builder's input is `roles/builder-N/input.md` — its brief + **its own slice ONLY**.
Builders are BLIND: do not give a Builder the repo at large, and do not let one see another's claims.

Claims JSON per Builder (schema as recorded in cycle 1):

```json
{"claims":[{"claim":"...","source_citation":"path:line at pin 02bcc20","confidence":0.0,
            "tag":"feasibility|completeness|risk","builder_id":"builder-N",
            "slice_id":"<its slice>","negates":null}]}
```

- `confidence` MUST be a **float 0..1** (not "high"/"medium").
- **M-20 IS NOW BINDING**: an absence claim must be **scoped verbatim to the slice**
  ("no X in \<these sources\>") and must **carry the search vocabulary used**. A repo-scope absence from a
  Builder is a DEFECT in the claim. If a Builder wants to assert one, it emits an OPEN QUESTION instead.

```powershell
C:\Python314\python.exe -m buildkit_cli.threerole record-output --project-root $PR --run $RUN --cycle 1 --role builder-N --claims <file>
# record-output is APPEND-ONLY — no re-record. Get it right first time.
```

## Step 3 — merge (mechanical set-ops; never a judgment call)

```powershell
C:\Python314\python.exe -m buildkit_cli.threerole merge --project-root $PR --run $RUN --cycle 1
```

Remember: `0 corroborated / N singleton` is a **phrasing artifact** (merge hashes normalized claim TEXT),
NOT weak evidence. Do not report it as agreement failure.

## Step 4 — the Critic, WITH THE WILDCARD REPO SCOPE (M-21)

```powershell
Get-Content <prompt> -Raw | codex exec - -C $PR -s read-only --skip-git-repo-check `
  --output-schema <schema> -o <out.json>
```

- **MUST use `-o`.** Piping codex to `Tee-Object`/console **wraps and CORRUPTS** the JSON mid-string.
- **M-21**: the Critic reads the REPO at the pin and MUST re-check EVERY absence claim at repo scope.
  "I'd have to read some code" is NOT grounds to ESCALATE — that is grounds to read it. ESCALATE is only
  for what no repo read settles (design intent, an unmeasured number, a prediction, a judgement).
- **codex SLIPS `claim_id`s in long lists.** Validate id coverage every time: exactly one entry per real
  id. Re-adjudicate any id with ≠1 entry in a **focused pass**; **never** map an unknown id by similarity;
  drop unknown ids. Then run a **file-overlap check** (a decision citing no file named in its claim) — in
  cycle 1 that caught one surviving false CONFIRM. For REFUTEs, no-overlap is EXPECTED and fine.
- Batch per-Builder (~26-31 claims) to keep the id-slip rate down.

```powershell
C:\Python314\python.exe -m buildkit_cli.threerole adjudicate --project-root $PR --run $RUN --cycle 1 --decisions <file>
# APPEND-ONLY: one batch per cycle. Assemble + validate 100% coverage BEFORE calling this.
```

## Step 5 — cycle 2 (min_cycles=2 — this run is EXPECTED to run it)

Cycle 1 of the previous run stopped early and that was recorded as a **halt**, not a pass. Do not repeat
that unless the evidence again says the subject is refuted — and if so, **name the halt**.

## Step 6 — verdict

```powershell
C:\Python314\python.exe -m buildkit_cli.threerole verdict --project-root $PR --run $RUN --report <curator.md> --terminal-reason "..."
```

- `--halted-at <gate>` **FORCES** verdict=halted. **Without it, `converged=false` computes `budget_stop`**
  — which is a LIE if no budget was exhausted. Always name a halt.
- `trace --decision` is a **CLOSED set** (CONFIRM/REFUTE/ESCALATE or accept/reject); narrative goes in
  `--evidence`.

## Rulings OWED BY THE ENGINEER (do not manufacture either)

1. **M-29 — the E-B ruling.** Does a next-hop-signed ACK satisfy the shipped corroboration contract?
   Security-sensitive (gates minting). Its old premise ("independence can never fire by construction") was
   **REFUTED** — `wallet_id` is an arbitrary string via `ActorWalletBindingRegistry.BindAsync` — so it must
   be re-argued from code, per M-36 (agreement is not verification). Builders establish the FACTS
   (slice-mint-authorization); the RULING is the engineer's.
2. **M-34 — the coin straddle.** `Olamnit.Coin` → BOTH `Olamnit.Kernel` (host) AND `Olamnit.Shared` (MAUI).
   Forbidden straddle to factor, or de-facto L1? **Blocks implementation. No slice owns it** — named as an
   UNCOVERED GAP, never faked as coverage. Verify by reading **ProjectReference entries**, never by
   inferring the graph from `using` statements (that is exactly how this was got wrong before).

## Traps

- **NEVER read the main clone** (`D:\bstdev\research\olamnit`) as evidence — automation drives it
  (branch `060-extract-dsdv-shared-package`, uncommitted DSDV refactor). **Never commit that work.**
- **Do not prune the worktree** `D:\bstdev\research\olamnit-wt-ring` @ detached `02bcc20`.
- **`.specify/3rtask/` is GITIGNORED** (`.gitignore:420`) ⇒ these runs live on disk ONLY.
  Backup: `G:\BSTDEV\research\glp\glpnet\COOP\olamnit\3rtask-runs\` (70 files, refreshed 2026-07-16).
- `compose_subject_brief` puts **ALL slice descriptions in EVERY Builder's brief** ⇒ never write an answer
  (or a "believed to" hypothesis) into a slice description.
- JSON inputs must be **BOM-free** (node writes clean UTF-8; PS 5.1 `Out-File utf8` adds a BOM).
- COOP: write ONLY `G:\...\COOP\olamnit\` — the `D:\bstdev\glp\GLPNET\COOP` copy is a DEAD LOCAL DECOY.
