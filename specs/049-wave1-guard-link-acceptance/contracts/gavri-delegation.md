# Contract — gavri delegation (US2 + US3)

Traces: FR-009..FR-011, FR-016; SC-005, SC-006. Per recorded clarification (2026-07-08), US2 +
US3 execute primarily on the **gavri** host; this contract binds the delegation artifact
`gavri-task-prompt.md` and the feed-back loop.

## D1. Branch & push scope
The gavri session works on its own branch **off `049-wave1-guard-link-acceptance`**
(suggested: `049-wave1-gavri-profile-c`), against `github.com/olamni-glp/GLPNET`. It pushes
ONLY its own branch (sessions get HTTP 403 elsewhere); it never rebases or force-pushes.

## D2. Scope of work on gavri
1. **Environment discovery** — record OS, toolchains (Erlang/OTP, rebar3, gleam, cmake, C/C++
   compiler), LAN address; commit the record to `evidence/gavri/environment.md` first.
2. **Profile C provisioning (T032)** — build `quicer` (MsQuic NIF) per
   `gleam_quic/profile_c/README.md` §"To complete Profile C later"; wire the `quic_link` module
   mirroring the C# `QuicTransport` contract; flip `GleamStackAdapter(profile="c")` capabilities
   to `in_process`. Document every provisioning step (FR-010 reproducibility).
3. **In-process conformance (US2)** — run the 036 conformance flow with the BEAM client
   in-process; pass criteria equal to the Profile A baseline (connect, TLS pin, full-duplex).
4. **Two-host LAN run (US3, T040)** — paired with this host (Olamnit, server side,
   `--addr 192.168.0.143` per 036 quickstart §7): cert material distributed out-of-band per the
   036 trust model unchanged; UDP port opened; full quickstart criteria incl. ≥4-client mesh.
5. **FR-015 #7 verification** (if the erlang test cannot run on Olamnit): run the length-framed
   read regression on gavri.

## D3. Evidence feed-back (early + continuous)
All evidence lands under `specs/049-wave1-guard-link-acceptance/evidence/gavri/` in the
`acceptance-evidence.md` format, committed and **pushed early and continuously** — never a
single end-of-task dump. The Olamnit session integrates by pulling the gavri branch (pull from
any branch is allowed).

## D4. Blockers
Any blocker (toolchain, LAN, cert) is recorded with evidence (what was attempted, what is
missing) as a BLOCKED record and escalated to Gabi; the ship gate stays closed. No silent skip,
no unilateral deferral (FR-008/FR-010 style; ship gate is all-four-stories).

## D5. Done criteria
US2 acceptance scenarios 1 (or 2-as-BLOCKED) and US3 scenarios 1 (or 2-as-BLOCKED) each have a
PASS/BLOCKED evidence record on the pushed branch; PASS on both closes D2 items 2–4.
