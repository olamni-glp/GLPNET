# Gavri sub-feature task prompt — 049 Wave 1, US2 + US3 (post this verbatim in a Claude Code session on gavri)

> Feature artifact per spec 049 FR-016. The engineer posts the block below in a fresh
> Claude Code session on the **gavri** host. It delegates User Story 2 (Profile C
> in-process QUIC on the full BEAM) and User Story 3 (two-host LAN acceptance) of
> `specs/049-wave1-guard-link-acceptance/spec.md`, with early, continuous feedback
> pushed to this repo.

---

## PROMPT — copy everything between the rules

You are executing a delegated sub-feature task on the host **gavri** for feature
`049-wave1-guard-link-acceptance` of the repo `https://github.com/olamni-glp/GLPNET.git`
(primary host: Olamnit, Windows). Your scope is EXACTLY User Story 2 (Profile C) and
User Story 3 (two-host LAN acceptance) of that feature — nothing else.

SETUP (do first):
1. Clone the repo (or `git fetch` if present), check out `049-wave1-guard-link-acceptance`,
   then create YOUR OWN branch off it: `049a-gavri-us2-us3`. You may push ONLY this branch.
   Never commit to any other branch; never use `git add -A` (stage files by name).
2. Read, in order: `CLAUDE.md` (root), `specs/049-wave1-guard-link-acceptance/spec.md`
   (esp. US2, US3, FR-009..FR-013, FR-015), `specs/036-http3-quic-ws-link/quickstart.md`,
   `specs/036-http3-quic-ws-link/followup-full-acceptance-brief.md`, and
   `gleam_quic/profile_c/README.md`.
3. Environment discovery (record in your first evidence commit): OS + arch, and versions of
   erlang/OTP, gleam, rebar3, cmake, C compiler, dotnet, python3. Install what is missing
   for the tasks below (prefer the platform package manager; record every install).

TASK A — US2, Profile C (in-process QUIC on the full BEAM):
1. Provision the `quicer` NIF: try a prebuilt/hex artifact for your OTP version first; if
   none fits, build from source (cmake + C toolchain; on Linux/macOS this builds where
   Olamnit's missing MSVC blocked it). Document the exact provisioning path (FR-010).
2. Build the Gleam stack: `cd gleam_quic && gleam build`, plus the Profile C pieces per
   `gleam_quic/profile_c/README.md`.
3. Build the local endpoints you need: `py/python3 -m venv glp_quick/.venv`,
   `pip install -e glp_quick[dev]`, and `dotnet build csharp/glp_quick_host/glp_quick_host.csproj -c Debug`
   (the C# host is the conformance reference server; run it locally on gavri for this task).
4. Generate a local cert for same-host runs: `glp-quick cert generate --out ./glpquick-cert`.
5. Baseline first: `glp-quick demo --addr 127.0.0.1 --port 8443 --cert ./glpquick-cert --stack gleam --profile a --clients 3`
   must pass (SC-001..SC-006) — this is your Profile A reference.
6. Profile C acceptance: the same demo with `--profile c` must pass with results equal to
   the Profile A baseline, with QUIC running IN-PROCESS on the BEAM via quicer (no C#
   side-process for the client data plane). If it cannot pass, record the exact blocker
   with evidence — do NOT fake, skip, or soften the result; the ship gate is hard.

TASK B — US3, two-host LAN acceptance (paired with Olamnit):
1. The shared certificate comes from Olamnit OUT-OF-BAND (the 036 trust model — manual pin,
   no CA). Ask the engineer to place the `glpquick-cert` dir (pem + fingerprint + pfx) on
   gavri, or confirm its path if already copied. Never commit certificate material.
2. Olamnit runs the server: `glp-quick --server --addr 192.168.0.143 --port 8443 --cert ./glpquick-cert --max-clients 4`.
   When the engineer confirms it is up, connect:
   `glp-quick --client --addr 192.168.0.143 --port 8443 --cert ./glpquick-cert --retry`
   (machine-name addressing `--addr Olamnit` is an acceptable variant; open the UDP port in
   gavri's firewall first).
3. Verify: genuine cross-host QUIC handshake + full-duplex exchange (type messages both
   ways), ≥4-client mesh with additional clients (extra clients may run on either host),
   kill-one-client resilience, and SPKI pin acceptance. Capture on-wire UDP evidence
   (tcpdump/wireshark snippet) to prove it is not loopback. Expected failure tokens if
   something is wrong: udp_blocked / cert_mismatch / alpn_version_mismatch / server_not_ready
   (see quickstart §Failure modes — a silent hang is a defect, report it).

EVIDENCE + FEEDBACK PROTOCOL (early and continuous — this is mandatory):
- Write evidence as markdown under `specs/049-wave1-guard-link-acceptance/evidence/gavri/`
  (e.g. `00-environment.md`, `10-profile-c.md`, `20-two-host.md`): every command, its
  output (trimmed to the relevant lines), and a per-criterion PASS/FAIL verdict table
  mapped to spec SC-005 (Profile C) and SC-006 (two-host).
- Commit + push to `049a-gavri-us2-us3` at every milestone (environment done, quicer
  provisioned, Profile A baseline, Profile C verdict, two-host verdict) — single-line
  commit messages, files staged by name. Do not wait for the end: the primary session
  integrates your evidence as it lands.
- If you fix code to make acceptance pass (e.g. the carried 036 findings #3/#5/#6/#7 in
  `glp_quick`, `glp_quick_host`, or `gleam_quic` — see the follow-up brief), keep fixes
  minimal, on your branch, each with a regression test, and flag them prominently in the
  evidence file. Do NOT touch core GLP (`glp_runtime/lib` outside multiagent), `programs/`,
  or any `.glp` file — those are out of your scope.
- When both tasks have final verdicts (or a hard blocker), write
  `specs/049-wave1-guard-link-acceptance/evidence/gavri/90-summary.md` with the overall
  result and push. That is your completion signal.

RULES: spec-first (quote the spec section before implementing anything); no scope creep;
no workarounds for spec-level problems — report and stop; if the spec, quickstart, and
reality disagree, STOP and report the discrepancy in the evidence file and to the engineer.

---

## Olamnit-side pairing checklist (for the primary session, not for gavri)

- Generate/locate the shared cert dir; the engineer copies it to gavri out-of-band.
- Start the server for TASK B when gavri signals ready:
  `glp-quick --server --addr 192.168.0.143 --port 8443 --cert ./glpquick-cert --max-clients 4`
  (firewall: UDP 8443 inbound open).
- Pull `049a-gavri-us2-us3` regularly; integrate evidence + any flagged fixes into
  `049-wave1-guard-link-acceptance` via merge after review.
