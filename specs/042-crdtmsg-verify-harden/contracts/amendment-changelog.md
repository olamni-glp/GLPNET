# Contract: Amendment Change Log (FR-011, SC-008)

Each of the three hardened documents gains a terminal section:

```markdown
## Change log — 042 verification pass (2026-07-XX)

> All amendments below were made by feature 042-crdtmsg-verify-harden; the finding ids
> resolve in docs/research/crdt-multiformat-messaging/verification-report-042.md.

| # | Section touched | Change | Why (finding id) | Baseline |
|---|---|---|---|---|
| 1 | §5 register row BB-ENC-7 | PROV → ACCEPTED/MVP-CORE, evidence added | PR-042-003 | HEAD(<commit>) |
| 2 | ... | | | |
```

## Rules

1. **1:1 mapping** — every in-place edit the pass makes to a document has exactly one row;
   no row describes an edit that was not made. This is what SC-008's 10/10 sampling verifies.
2. **finding_id join** — the Why column holds a finding id that exists in the verification
   report (§1–§8). No free-floating rationale.
3. **Baseline label** — every row carries `DELIVERY(<commit>)` or `HEAD(<commit>)` (FR-015);
   hardening edits are normally HEAD-baselined, ledger corrections DELIVERY-baselined.
4. **Zero silent edits** — formatting-only touches (e.g. adding the report reference for
   SC-009, adding this section itself) are also rows.
5. **Append-only within the pass** — rows are never rewritten after the report freezes;
   a correction is a new row.
6. The change-log section is the ONLY structural addition the pass makes to a deliverable
   besides the report reference and the amendments themselves — the docs remain the epic's
   single source of truth in their shipped shape (Principle VIII).
