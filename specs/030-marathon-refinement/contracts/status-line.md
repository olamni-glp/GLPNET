# Contract — Status line grammar (FR-019)

A single mechanically-parseable line, emitted at every stage boundary and on demand. Grammar (fixed,
pipe-delimited, adopted from the sibling and reconciled with the dynamic total):

```
marathon <run_id> | done=<d>/<n> | open=<k> | budget=<b>[<unit>] | next=<action>
```

- `done=<d>/<n>` — `<n>` is the **current** total stage count (grows with append/capture), never the
  registration-time count (FR-003, SC-002).
- `open=<k>` — count of open `issue` rows.
- `budget=<b>[<unit>]` — `budget_spent` and the run's `budget_unit` (e.g. `41000tokens`); `budget=0` when
  unset.
- `next=<action>` — the single next action string from the resume position ([`resume-position.md`](./resume-position.md)).

Example:
```
marathon 030-marathon-refinement | done=2/7 | open=1 | budget=41000tokens | next=run mini-plan for item-3
```

Parse contract: split on ` | `, then `key=value`. The four fields after `marathon <run_id>` always appear in
this order. A parity/grammar test pins the format.
