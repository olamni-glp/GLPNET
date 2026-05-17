# Quickstart — codeconv-builder (018)

Prereq: `--data-dir C:/pglite/research/glpnet` on this checkout (pass
proactively per CLAUDE.md). venv: `codeconv/.venv`.

## Flow B — durable end-to-end, with kill/resume

```
codeconv --data-dir C:/pglite/research/glpnet migrate          # single head 0005 (FR-015)
codeconv --data-dir C:/pglite/research/glpnet init              # one unified workspace (016 reused)
/codeconv-builder                                               # skill: durable orchestration loop
#   → discover → depgraph order → scaffold → convspec → plan, per file in 015 topo/SCC order
#   (each (file,stage) a DBOS step; agent work spawned on NeedsAgentWork)
# kill the run anytime (Ctrl-C / reboot / bridge restart)
/codeconv-builder                                               # re-run: resumes; 0 completed files redone (SC-002)
```

## convspec (per file)

On `NeedsAgentWork`, the skill spawns the analysis sub-agent (+ a *separate*
research sub-agent only on an idiom-KB miss). Output: checked-in
`.codeconv/conversion-specs/<rel>.dart.md` (structured block + human
rationale/provenance, **no C#** — FR-023). Recurring constructs reuse
`conversion_idioms` (no re-research — FR-012/FR-024). Undecidable / conflict ⇒
escalation in `.codeconv/conversion-idioms/_escalations-report.md`
(FR-013/014), conversion blocked for that file only.

## Observe & recover

```
codeconv builder status                 # per-file state + counts, <5 s (FR-017/SC-009)
codeconv builder trace --file lib/x.dart # DBOS step history (debug/plan — D1=a)
codeconv builder retry --file lib/x.dart # one file, others undisturbed (FR-018)
codeconv builder aggregate-escalations   # single report (FR-013/014)
```

## Acceptance smoke (maps to SC-00x)

1. fresh cluster → `migrate` → one head, 0 dup/multi-head (SC-004).
2. full run over the subtree → every file processed once in dep order (SC-003).
3. kill after K/N → re-run → files 1..K not redone, final == uninterrupted
   (SC-002).
4. file with `Stream`/async + no idiom → spec cites analysis + official-doc
   research, records idiom; second file reuses idiom, not re-researched
   (SC-006/SC-007).
5. undecidable construct → escalation, 0 silent guesses (SC-008).
6. empty subtree → "nothing to convert", exit 0 (FR-020).
7. every 015/016/017 entrypoint still reachable (SC-005).
