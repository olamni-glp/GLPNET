# T029 — Cross-runtime FE/BE smoke, both directions (TEXT kinds)

Date: 2026-08-03 · Host: OLAMNIT (ariellas) · Branch: 064-post-wave-gap-closure
Contract: `specs/064-post-wave-gap-closure/contracts/febe-split.md` acceptance 2.
Ports used: 7491 (aborted first attempt), 7492 (C# BE), 7493 (Gleam BE). All
processes started for this smoke were killed afterward; ports verified free.

Builds under test:

- C# — `dotnet build -c Release` of `csharp/glp_engine_host/GlpEngineHost.csproj`
  and `csharp/glp_repl_client/GlpReplClient.csproj` (net10.0, 0 errors).
- Gleam — WSL clean rebuild (`rm -rf build && gleam build`) with the user-space
  OTP 25.3.2.8 on PATH (per CLAUDE.md smoke-gate note), for the BE; plus a
  Windows-side build (winget Gleam 1.17.0 + Windows Erlang OTP) of a scratchpad
  **copy** of `glp_gleam/` sources for the FE (see the environment constraint
  below — no repo tree was modified).

## Environment constraint (host, not protocol): WSL2 localhost asymmetry confirmed

- **Windows → WSL dials WORK** (verified live: the C# client on Windows reached
  the Gleam BE bound to `127.0.0.1:7493` inside WSL via WSL localhost
  forwarding).
- **WSL → Windows dials are BLOCKED on this host.** The Hyper-V firewall's
  `DefaultInboundAction` is `Block` on all profiles (`Get-NetFirewallHyperVProfile`),
  and raw dials from WSL to a listening C# engine host failed against both the
  vEthernet (WSL) adapter IP `172.28.208.1` and the LAN IP `192.168.0.142`
  (host listening on `0.0.0.0:7492`; `bash /dev/tcp` probes: `VETH_FAIL`,
  `LAN_FAIL`). Adding a firewall rule was out of scope for this session
  (permission denied, not attempted further).
- **Consequence:** direction 1 could not be run with the FE inside WSL. It was
  run instead with the Gleam FE **on Windows** (winget Gleam 1.17.0 + Windows
  Erlang, built in a scratchpad copy of `glp_gleam/`) dialing the C# BE over
  Windows loopback. This still exercises exactly the contract's consequence —
  the Gleam FE driving the C# BE over the shared split-protocol frames — just
  not across the WSL boundary. Recorded here so nobody mistakes the transport
  workaround for a protocol result.

## Direction 1 — Gleam FE ↔ C# BE (text kinds)

### Commands

```
# BE (Windows):
csharp/glp_engine_host/bin/Release/net10.0/glp_engine_host.exe --listen 0.0.0.0:7492
#   (0.0.0.0 was chosen while probing the WSL leg; it triggers the expected loud
#    non-loopback warning. Loopback-only would have sufficed for the final run.)

# FE (Windows, from the scratchpad copy of glp_gleam/):
printf 'load test/glp_split_output_fixture.glp\nemit(hello).\nload D:/bstdev/research/glp/glpnet/programs/tests/typed/struct_demo.glp\nbuild_person(P).\nmake_person(bob, forty, boston, Q).\n:status\n:quit\n' \
  | gleam run -m glp/fe/client -- --connect 127.0.0.1:7492
```

### Transcript (FE side, verbatim)

```
Connected to engine at 127.0.0.1:7492
Input: load <file.glp> to load, goal. to execute; :status, :quit

GLP> ✓ Loaded: test/glp_split_output_fixture.glp
GLP> hello
→ succeeds

GLP> ✓ Loaded: D:/bstdev/research/glp/glpnet/programs/tests/typed/struct_demo.glp
GLP> P = "person(alice, age(thirty), city(seattle))"
→ succeeds

GLP> Q = "person(bob, age(forty), city(boston))"
→ succeeds

GLP> state=serving engine=engine-7492 loaded_programs=3 pending_snapshot=none last_snapshot_seq=none
GLP> Goodbye!
EXIT=0
```

### Verdicts

| Step | Verdict |
|---|---|
| Connect (dial, banner) | PASS |
| `load` fixture → ACK `✓ Loaded` | PASS |
| `emit(hello).` → captured `_output` blob renders (`hello`), `→ succeeds` | PASS |
| `load` struct_demo → ACK | PASS |
| `build_person(P).` / `make_person(...)` — goal runs, status correct | PASS |
| Binding rendering | **DIVERGENCE** — `P = "person(alice, age(thirty), city(seattle))"` (whole term wrapped in string quotes; single-process reference prints `P = person(alice, age(thirty), city(seattle))`) |
| `:status` — C# field shape renders | PASS |
| `:quit` → `Goodbye!`, exit 0 | PASS |

### Divergence mechanism (recorded, not fixed)

The known 038-envelope divergence, seen from the FE side: the **C# BE renders
bindings engine-side and ships each as `ConstTerm(ConstString(<display text>))`**
(R6, pre-rendered display strings). The Gleam FE renders envelopes through
`glp/repl/results.format_term`, which deliberately re-adds surrounding quotes to
`ConstString` (US5 T044 cross-runtime display parity for *genuine* string
constants — `glp_gleam/src/glp/repl/results.gleam:43`). A pre-rendered display
string is indistinguishable from a genuine string constant in the envelope, so
the whole term arrives quoted. Both envelopes are legal; content is otherwise
byte-identical to the reference rendering.

## Direction 2 — C# thin client ↔ Gleam BE (text kinds)

### Commands

```
# BE (WSL, from glp_gleam/ package root; OTP 25 pin per CLAUDE.md):
wsl bash -c 'export PATH="$HOME/otp-25.3.2.8/bin:$PATH"; cd /mnt/d/bstdev/research/glp/glpnet/glp_gleam && gleam run -m glp/be/server -- --listen 127.0.0.1:7493'

# Client (Windows, from the repo root; text path is the default):
printf 'load ./glp_gleam/test/glp_split_output_fixture.glp\nemit(hello).\nload ./programs/tests/typed/struct_demo.glp\nbuild_person(P).\nmake_person(bob, forty, boston, Q).\n:status\n:snapshot\n:quit\n' \
  | csharp/glp_repl_client/bin/Release/net10.0/glp_repl_client.exe --connect 127.0.0.1:7493
```

### Transcript (client side, verbatim)

```
Connected to engine at 127.0.0.1:7493
Input: load <file.glp> to load, goal. to execute; :status, :snapshot, :quit

GLP> ✓ Loaded: ./glp_gleam/test/glp_split_output_fixture.glp
GLP> hello
→ succeeds

GLP> ✓ Loaded: ./programs/tests/typed/struct_demo.glp
GLP> P = person(ConstTerm(ConstAtom('alice')),age(ConstTerm(ConstAtom('thirty'))),city(ConstTerm(ConstAtom('seattle'))))
→ succeeds

GLP> Q = person(ConstTerm(ConstAtom('bob')),age(ConstTerm(ConstAtom('forty'))),city(ConstTerm(ConstAtom('boston'))))
→ succeeds

GLP> state=serving engine=engine-7493 loaded_programs=2 pending_snapshot=none last_snapshot_seq=none
GLP> !! protocol error: engine error: SNAPSHOT is not served by the Gleam BE (no snapshot store; 064 T026 text-kind scope)
GLP> Goodbye!
EXIT=0
```

(Windows→WSL localhost forwarding carried the dial, as predicted for this host.)

### Verdicts

| Step | Verdict |
|---|---|
| Connect (Windows client → WSL BE over 127.0.0.1) | PASS |
| `load` fixture / struct_demo → ACK `✓ Loaded` | PASS |
| `emit(hello).` → captured blob renders, `→ succeeds` | PASS |
| Goals run, status lines correct | PASS |
| Binding rendering | **DIVERGENCE** — `P = person(ConstTerm(ConstAtom('alice')),…)` (debug `ToString()` of the structured term; reference prints `P = person(alice, age(thirty), city(seattle))`) |
| `:status` — same field shape as C# BE | PASS (see loaded_programs note below) |
| `:snapshot` → loud typed refusal, client keeps going | PASS (per T026 scope: `PROTOCOL_ERROR`, engine keeps serving; never a silent no-op) |
| `:quit` → exit 0; BE engine survives the disconnect | PASS |

### Divergence mechanism (recorded, not fixed)

The same known divergence, mirror image: the **Gleam BE ships bindings as
deep-resolved structured codec terms** (`StructTerm`/`ConstTerm` trees — legal
038 envelope). The C# client's renderer special-cases only
`ConstTerm { Value: ConstString }` (its own BE's pre-rendered form) and falls
back to `Term.ToString()` for everything else
(`csharp/glp_repl_client/Program.cs` `RenderEnvelope`, ~line 394) — and the
codec terms' `ToString()` overrides are debug spellings
(`ConstTerm(ConstAtom('alice'))`, `csharp/glp_result_codec/ResultEnvelope.cs`).
Result: structurally correct, display-hostile output.

## Interop gaps found (feed DEFERRALS or fixes — lead's call)

1. **Bindings render wrong in BOTH cross pairings; wire is fine.** Everything
   interoperates at the protocol level (frames, request-id echo, ACK/RESULT/
   PROTOCOL_ERROR kinds, captured-output blob, status text, exit taxonomy). The
   gap is purely the two runtimes' opposite RESULT-binding conventions
   (C# BE: pre-rendered `ConstString` display strings · Gleam BE: structured
   deep-resolved terms) meeting renderers that each assume their own BE:
   - Gleam FE + C# BE ⇒ bindings wrapped in spurious quotes.
   - C# client + Gleam BE ⇒ bindings printed as debug `ToString()` spellings.
   Same-runtime pairings are unaffected. A convergence (pick one convention, or
   teach each renderer both forms) is a small, contained change — but per the
   task brief nothing was fixed here.
2. **`:status` `loaded_programs` counts differ**: after the identical 2 loads
   the C# BE reports `loaded_programs=3`, the Gleam BE `loaded_programs=2`
   (prelude counted vs not). Cosmetic; field shape is otherwise identical.
3. **Host-environment (not protocol): WSL→Windows loopback is blocked** by the
   Hyper-V firewall default-inbound Block on this host, so a WSL-resident Gleam
   FE cannot reach a Windows C# BE without a firewall rule (admin). Direction 1
   was therefore run with the FE on Windows loopback (details above). Any CI
   packaging of this smoke should co-locate FE and BE on one side of the
   boundary or provision the firewall rule.
