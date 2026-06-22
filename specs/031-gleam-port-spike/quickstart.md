# Quickstart: Running & Reading the Gleam Spike

**Feature**: 031-gleam-port-spike | **Date**: 2026-06-22

This is the reproducible **setup → build → run → read** path. It exists so a *second person on a clean checkout* can reproduce the smoke and act on the dossier (SC-002, SC-001). During `/bk-implement` the exact commands + observed outputs are filled into the toolchain inventory and the smoke's `README.md`; the shapes below are the contract those recorded commands satisfy.

> All durable outputs live under `docs/research/gleam-atomvm/`. The spike creates **no** `glp_gleam/` subtree and changes **no** GLP runtime/programs/roadmap (FR-011).

## 1. Stand up the toolchain (FR-003)

Windows-first; fall back to WSL/Linux (or sibling Mac) if AtomVM bring-up — or anything — fails on Windows, and **record which environment was used** (research R1).

```text
# Pin and record EXACT versions (no "latest"):
gleam --version
erl +V            # Erlang/OTP version
rebar3 version
# AtomVM: prefer a prebuilt/generic host release; record its tag.
```

## 2. Build & run the hello-GLP-term smoke on Erlang/BEAM (FR-004, US2)

```text
cd docs/research/gleam-atomvm/hello-glp-term
gleam build               # compile to BEAM
gleam run                 # run on Erlang; observe the term + the bound value
```

Expected observable result: the representative GLP term (≥1 compound/structure + 1 unbound-variable analogue) printed in its documented representation, **and** the single unbound→bound transition's bound value as seen by the reader process. Record the verbatim output as BEAM evidence.

## 3. Attempt the smoke on AtomVM (FR-005, US3) — effort-bounded

```text
# 1) Prefer a prebuilt/generic AtomVM host build; run the smoke's .beam/.avm on it.
# 2) Only if no prebuilt runs: a TIME-BOXED source build of the generic_unix host.
# 3) If neither succeeds within budget: record the bring-up BLOCKER as the
#    AtomVM matrix row's evidence (not just a subset guess).
```

Record the outcome (success / partial / failure-with-output, or the blocker). No embedded hardware is in scope — this is a host/generic build.

## 4. (Optional) JavaScript backend for the matrix's JS row (US4)

```text
gleam build --target javascript
gleam run --target javascript     # observe / or cite authoritative docs
```

## 5. Read the dossier and act (SC-001, US1)

Open `docs/research/gleam-atomvm/dossier.md`. From it **alone** you can:
- state the recommended **source basis** (Dart / C# / file-by-file) and its one-sentence rationale;
- read the **criteria table** that produced it;
- read the **architectural-fit** findings (incl. the mutable-heap/immutability mismatch backed by the smoke);
- read the **build-target matrix** (every cell has a verdict + evidence);
- act on the single **go / no-go / go-with-revisions** verdict.

## Reproducibility check (SC-002)

A second person, on a clean checkout, following §1–§2, sees the **same** observed BEAM result. If not, the toolchain inventory's command/version block is incomplete — fix it, don't hand-wave.

## Done-when (maps to Success Criteria)

| Done-when | SC |
|---|---|
| Source basis statable from the dossier alone, in one sentence | SC-001 |
| Smoke reproduces for a second person from recorded commands | SC-002 |
| Every matrix target has a verdict + ≥1 evidence; no unexplained "unknown" | SC-003 |
| F2/F3 can start without re-opening the source question | SC-004 |
| Exactly one go/no-go verdict; every affected heavy feature named with re-scope/"unchanged" | SC-005 |
| Architectural-fit names ≥ the two required findings; mutable-heap finding smoke-backed | SC-006 |
