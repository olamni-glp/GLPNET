# Sources — dbos

The AIGRID prereq-patterns catalog is glpnet's consolidating upstream for this pattern. Its `prereq-patterns/dbos/` index points at the working hatzinor ulpani-LMS DBOS-on-pglite wiring (a single ~278-line module) plus two integration tests that encode the durability and refusal-to-start invariants. Glpnet has no own implementation today; the AIGRID-side citations reach into hatzinor (`olamni-research/hatzinor_ai-data-driven-publishing@develop`) for the actual code.

## Index

| Path | Upstream | Action | Summary |
|---|---|---|---|
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/dbos/description.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's pattern description — consumer-class behaviour and pglite-substrate constraints. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/dbos/applicability.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's consumer-class adaptation notes for workflows, steps, and hybrid flows. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/dbos/sources.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's underlying-source citations into hatzinor: `ulpani_lms_dbos.py`, the workflow-admin and schema-missing tests, plus the pglite substrate cross-reference. |

## Per-source notes

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/dbos/description.md`

- Substrate constraints (`sys_db_pool_size=1`, `use_listen_notify=False`, the `uuid-ossp` migration strip) are the load-bearing claims a glpnet adopter MUST honour when wiring DBOS against the merged pglite bridge from this catalog's pglite pattern.
- The "durability contract: durable or not at all" framing in the substrate-constraints section is the test bar — `DbosSchemaMissingError` refusal-to-fall-back is what makes the contract enforceable.

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/dbos/applicability.md`

- Per-consumer H3s cover four sub-cases: agent-action workflows (`@DBOS.workflow()` decorator on the entry function; non-serialisable handles like SQLAlchemy sessions are NOT valid workflow inputs); agent-action Python tool calls (`@DBOS.step()` with idempotency or dedup keys); hybrid agent/code flows (LLM-call wrapping; no streaming, since DBOS persists step output as one shot); and the pglite substrate (the `pglite_engine_kwargs(application_name='dbos_transact')` call shape).

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/dbos/sources.md`

- Cites `D:/BSTDEV/lang/hatzinor_ai-ddp/src/ulpani_lms_dbos.py@develop` (`olamni-research/hatzinor_ai-data-driven-publishing@develop`, Action: Copy) — the DBOS-on-pglite wiring entry point. `bootstrap_dbos_schema()`, `get_dbos_app()`, the `_apply_pglite_compat_patch()` monkey-patch, and the `DbosSchemaMissingError` refusal class all live here.
- Cites two integration tests — `test_dbos_workflow_admin.py` and `test_dbos_schema_missing_refuses.py` — both Action: Read. The first is the executable specification of the durability contract; the second proves the refusal-to-start path.
- Cites the pglite substrate's description.md for the `QueuePool of one` invariant rationale that makes DBOS's `sys_db_pool_size=1` a forced move rather than an arbitrary knob.
