# Contract — Library API (importable surface)

The refined harness is drivable as a library; the CLU ([`cli.md`](./cli.md)) is a thin 1:1 wrapper (FR-025
parity). The library is the contract; the CLI is glue. All functions accept an optional `env: MarathonEnv`
(per-run store root, engine, repo dir) and resolve a default when omitted.

## Run lifecycle (`stages.py`, `checkpoint.py`)
```python
def register_run(run_id: str, *, stages: list[str] | None = None,
                 title: str | None = None, budget_ceiling: int | None = None,
                 budget_unit: str | None = None, env: MarathonEnv | None = None) -> MarathonRun
    # Write the run + its initial ordered stage list (origin='registered').
    # Empty stage list is legal; resume then reports "register stages" (edge case).
    # NOTE: declarative manifest-based registration is deferred to a follow-on (no FR in scope).

def append_stage(run_id: str, name: str, *, env: MarathonEnv | None = None) -> StageRow
    # Append a dynamically-discovered stage; stage_index = max+1; total grows (FR-002/003).

def start_stage(run_id: str, name: str, *, env: MarathonEnv | None = None) -> StageRow
    # Flip status pending→running, set started_at. A started-not-complete stage is NOT counted done (FR-004).

def checkpoint(run_id: str, name: str, *, completed_units=None, remaining_units=None,
               wip_unit: str | None = None, budget_delta: int = 0, committed_paths=None,
               issues: list[str] | None = None, message: str | None = None,
               env: MarathonEnv | None = None) -> CheckpointRow
    # Append a checkpoint; remaining_units==[] flips the stage complete; records budget delta.
    # If the stage is a mini 'mini_analyze' completing, mark its parent item done (D4).

def finalize(run_id: str, *, env: MarathonEnv | None = None) -> MarathonRun
    # status→finalized ONLY when every stage is complete; re-opens cleanly if a later append occurs.

def resume(run_id: str, *, env: MarathonEnv | None = None) -> ResumePosition  # see resume-position.md
```

## Emergent work (`intake.py`)
```python
def capture_item(run_id: str, *, kind: str, title: str, description: str | None = None,
                 blocks_stage: str | None = None, env: MarathonEnv | None = None) -> Item
    # kind ∈ {latent-requirement, issue, bug, missing-prerequisite} (FR-005).
    # Appends the 5 mini-stages (mini-specify…mini-analyze) (FR-006, D4).
    # If kind == missing-prerequisite AND blocks_stage set → fractional order_keys place the mini-stages
    #   strictly BEFORE the blocked stage (FR-010); else after current max order_key.
    # Creates <store_root>/items/<id>/ (FR-008). Never auto-advances (FR-009, default-deny).
    # If blocks_stage is already complete → raise/escalate prereq_against_completed_stage (edge case).
```

## Keeper (`keeper.py`) — see [`keeper-lifecycle.md`](./keeper-lifecycle.md)
```python
def start_keeper(run_id: str, *, env: MarathonEnv | None = None) -> Endpoint
def stop_keeper(run_id: str, *, env: MarathonEnv | None = None) -> None
def recover_keeper(run_id: str, *, env: MarathonEnv | None = None) -> Endpoint
def engine_for(run_id: str, *, env: MarathonEnv | None = None) -> Engine   # auto-starts keeper if none live
```

## Preserved 024 strengths (ported)
```python
# gate.py
def present_gate(store, stage_id, plan_ref) -> str             # idempotent; returns approval_state
def record_decision(store, stage_id, *, outcome, decided_by, plan_ref) -> Approval
def approval_state(store, stage_id) -> str | None              # approved|changed|awaiting|None (FR-020)

# orchestrate.py
class Budget: ...                                              # ceiling, spent(), remaining(), add() raises on exceed
def advance_budget_or_halt(store, run_id, budget, tokens, *, stage_id, ...) -> dict   # 0 overruns (FR-022)
def rerun_block(store, stage_id, *, all_units=None) -> dict    # resume from last checkpoint (FR-021)
def rerun_subagent(store, stage_id, subagent) -> dict          # isolated; reports untouched siblings (FR-021)
def require_workflow_optin(run) -> None                        # raises WorkflowOptinNotGranted

# trace.py
def write_trace(store, run_id, *, subject, experiment_input, metric_score, decision, refine_seq=None) -> Trace
def list_traces(store, run_id, *, subject=None) -> list[dict]  # ordered by (subject, refine_seq) (FR-023)

# repository.py
def reconcile(run_id) -> ReconcileResult                       # PGLite ↔ JSON mirror; fast-forward|fork|in_sync (FR-024)

# status.py
def status_line(run_id, *, env=None) -> str                    # see status-line.md (FR-019)
def emit_status(run_id, *, env=None) -> StatusReport
```

## Parity invariant
A parity test (`test_marathon_cli_parity`) asserts every CLI subcommand maps to exactly one public library
function and vice-versa (FR-025). The library functions are importable and unit-tested **bridge-free**
(pure derivations: `derive_position`, `approval_state`, `Budget`, reconciliation comparison).
