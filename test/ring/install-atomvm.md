# Measuring AtomVM's subset for real — the second half of T017

**Engineer ruling `Q-GLPNETS17-01` (2026-09-04): options 1 AND 2, both critical.**
Option 1 (adopt the measured dossier boundary) is **done** — see `atomvm-unsupported.list` and
`check_atomvm_subset.sh`. This file is option 2: standing up a real AtomVM and measuring the
subset by observation rather than inference.

## Why this is still open

`atomvm-unsupported.list` is a **lower bound**, not the subset. It carries 8 constructs, all
tracing to one measured failure — `module proc_lib cannot be resolved` on AtomVM 0.6.6. Upstream
documents AtomVM's BEAM/OTP subset as substantially narrower than full OTP (ETS, several stdlib
modules, parts of the process API). **None of that was measured**, so none of it is in the list,
so the gate cannot refuse it. A pass from `check_atomvm_subset.sh` therefore means *"no construct
we have measured as unsupported is present"* — not *"this will run on AtomVM"*. Closing that gap
is what this runbook is for.

## Status on GAVRIELLA

| step | state | measured |
|---|---|---|
| WSL2 core | **installed** — `wsl --status` reports Default Version: 2 | 2026-09-04, after elevated `wsl --install --no-launch` |
| WSL distro | **pending** — `wsl --list` reports no installed distributions | 2026-09-04 |
| Reboot | **required** before the distro is usable | Windows optional-component install |
| AtomVM | **absent** — no `atomvm`, no `packbeam` | 2026-09-04 |

AtomVM publishes **no official Windows binary**; the host build is `generic_unix`. That is why
this needs WSL (or another Linux host) rather than a direct install.

## Runbook — after the reboot

```bash
wsl --install -d Ubuntu          # if no distro yet; sets up user + password interactively
wsl -d Ubuntu -- bash -lc 'sudo apt-get update && sudo apt-get install -y erlang elixir unzip curl'
```

Then fetch the same version the dossier measured, so the new numbers are comparable with the old:

```bash
wsl -d Ubuntu -- bash -lc '
  cd ~ && curl -sSLO https://github.com/atomvm/AtomVM/releases/download/v0.6.6/AtomVM-linux-x86_64-static-mbedtls-v0.6.6.tar.gz &&
  tar xzf AtomVM-linux-x86_64-static-mbedtls-v0.6.6.tar.gz && ./AtomVM --version'
```

⚠ **Verify that URL before trusting it.** It is transcribed from the dossier's build name, not
from a fetch that has been run here. If it 404s, take the asset name from the AtomVM releases page
rather than guessing a variant — a wrong build measures a different subset.

## What to measure, and what to do with it

1. **Re-run the dossier's own smoke first.** It must reproduce: `gleam_otp` actor build crashes
   with `module proc_lib cannot be resolved`; raw `erlang:spawn` + `gleam_erlang` Subjects runs.
   If it does not reproduce, **stop** — the list's provenance is broken and
   `atomvm-unsupported.list` must be re-derived, not extended.
2. **Then extend by observation**, one construct at a time, recording for each: the construct, the
   AtomVM version, the observed error text. Add to `atomvm-unsupported.list` in the same
   `<construct>\t<reason>` shape, with the error text quoted. Never add an entry sourced from
   documentation alone — that is the guess `research.md` R3 forbade, and the list's value is that
   every line is an observation.
3. **Run the pinned corpus on AtomVM** and emit a real C4 report. This is the step that turns the
   ring from `Unread` into `Measured`:
   ```bash
   bash test/ring/run_beam_ring_no_dart.sh --out test/ring/reports   # the BEAM comparison
   # then the AtomVM equivalent, writing test/ring/reports/atomvm.report
   ```
4. **Re-run the aggregate.** It refuses today on the unread ring; once atomvm reports `Measured`
   and green, it should go GREEN of its own accord. Do not edit the aggregate to make that happen.

## The one thing not to do

**Do not synthesize a stand-in host.** A local Erlang process standing in for the MAUI Blazor
Hybrid host would flip the report from `Unread` to `Measured` and produce evidence about the
stand-in, not about AtomVM — invisible in a report carrying only counts. `research.md` R4 forbids
it, `report_atomvm_unread.sh` re-measures both premises before emitting, and
`test_aggregate.sh::test_unread_ring_is_not_laundered` fails if an unread ring is ever counted as
agreement.

Note that even a fully green AtomVM corpus run leaves the **host-side** row of the coverage matrix
UNREAD: the MAUI host is target-side and absent from this repo. Per ruling `Q-GLPNETS17-02`, era
101 closes on the scope-bounded verdict, and that row stays honestly unread.
