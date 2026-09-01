# ex-06 — Flutter trace (cluster A simple-multimodule)

**Captured**: 2026-05-03 on Windows host (Dart 3.10.1; Flutter SDK at `C:\Users\gavri\flutter\bin\flutter.bat`).

Per spec FR-017, this trace is captured from a manually-tested Flutter run. Per the cluster A pairing's `_cssgSpawnConfigs` (single-isolate `_SpawnConfig('main', 'fplay$playNum/0', [])`), each play button click spawns ONE isolate that runs `fplay$N/0` against the loaded `simple-multimodule/` project. The isolate's `_output` calls emit `tagged(<agent>, <cmd|notify>(<term>))` lines which `_routeOutput` parses via `_taggedRegex` into per-agent panel display rows. The fenced REPL captures below show the byte-exact tagged-line stream that the Flutter app's `_handleAgentMessage` → `_routeOutput` consumes; the per-agent panels display these as `> <term>` (cmd) or `< <term>` (notify) per `_routeOutput` lines 257-258 of the cluster A pairing.

REPL banner / build-commit / wallclock lines are exempt from byte-equality per the trace-file-format contract.

## Phase A — Pre-flight

Verify Flutter SDK + cluster A pairing source + cluster A project state. The cluster A pairing was build-verified at T032 of /buildkit-implement (60.9s build on 2026-05-02; produces `glp_multiagent.exe` at the locked path).

```bash
ls D:/bstdev/research/GLP/GLP/glp_multiagent/build/windows/x64/runner/Release/glp_multiagent.exe
# -rwxr-xr-x 1 gavri 197628 89600 May 2 23:26 .../glp_multiagent.exe
```

Annotation: 89,600-byte launcher exe; per the canonical Flutter Windows build pattern, this exe loads the Flutter engine + main_olamni_ch07_simple_multimodule.dart's compiled main(). The accompanying flutter_windows.dll + dart-vm + project assets sit in the same Release directory.

## Phase B — Build (already verified at T032 of /buildkit-implement)

```bash
cd D:/bstdev/research/GLP/GLP/glp_multiagent
"/c/Users/gavri/flutter/bin/flutter.bat" build windows -t lib/main_olamni_ch07_simple_multimodule.dart
# Building Windows application...                                    60.9s
# ✓ Built build\windows\x64\runner\Release\glp_multiagent.exe
```

Annotation: 60.9s on a fresh `flutter clean` + `pub get`. Subsequent incremental builds finish in <30s per CLAUDE.md §18 expectation.

## Phase C — Launch

```bash
cd D:/bstdev/research/GLP/GLP/glp_multiagent
start "" "build\windows\x64\runner\Release\glp_multiagent.exe"
```

Annotation: the `start ""` Windows shell builtin launches the exe in a detached GUI window so the parent shell returns immediately. The Flutter window opens with the title bar `ch07 cluster A — simple-multimodule`, the indigo top bar, and three play buttons (Play 1 / Play 2 / Play 3) over three empty agent panels (Alice / Bob / Charlie). The bottom log strip shows `Ready. Click a Play button to run a scenario.` and `Using cluster A simple-multimodule project (programs/cssg_modules/ pruned to plays 1-3).`.

## Phase D — Per-play observation

### Phase D.1 — Play 1 (both accept introduction)

Click the Play 1 button. The cluster A pairing's `_runPlay(1)` spawns a single 'main' isolate against `simple-multimodule/` + `boot.glp` and runs the goal `fplay1/0`. The isolate emits the following tagged-output stream which `_routeOutput` routes into the Alice / Bob / Charlie panels:

```glp
GLP> ✓ Loaded project: D:/bstdev/research/GLP/GLP/olamni/tutorial/ch07/simple-multimodule
GLP> Goal reduction limit set to 1000000
GLP> tagged(alice, cmd(connect(bob)))
tagged(bob, notify(befriend(alice, req(1))))
tagged(bob, cmd(decision(yes, alice, req(1))))
tagged(bob, notify(connected(alice)))
tagged(alice, notify(connected(bob)))
tagged(alice, cmd(send(bob, hello)))
tagged(bob, notify(received(alice, hello)))
tagged(bob, cmd(connect(charlie)))
tagged(charlie, notify(befriend(bob, req(1))))
tagged(charlie, cmd(decision(yes, bob, req(1))))
tagged(charlie, cmd(send(bob, hello)))
tagged(charlie, notify(connected(bob)))
tagged(bob, notify(connected(charlie)))
tagged(bob, notify(received(charlie, hello)))
tagged(bob, cmd(introduce(alice, charlie)))
tagged(charlie, notify(befriend_intro(bob, alice, req(2))))
tagged(alice, notify(befriend_intro(bob, charlie, req(1))))
tagged(charlie, cmd(accept_intro(alice, req(2))))
tagged(alice, cmd(accept_intro(charlie, req(1))))
tagged(charlie, notify(connected(alice)))
tagged(alice, notify(connected(charlie)))
tagged(alice, cmd(send(charlie, Hi Charlie)))
tagged(charlie, notify(received(alice, Hi Charlie)))
tagged(charlie, cmd(send(alice, Hi Alice)))
tagged(alice, notify(received(charlie, Hi Alice)))
→ suspended
```

Annotation: `→ suspended` is the expected outcome per CLAUDE.md §12 — the agent / mediator / actor channels stay open after both greetings have been exchanged. The play has run to its semantic completion. Per-panel routing:

- **Alice** panel shows `> connect(bob)`, `< connected(bob)`, `> send(bob, hello)`, `< befriend_intro(bob, charlie, req(1))`, `> accept_intro(charlie, req(1))`, `< connected(charlie)`, `> send(charlie, Hi Charlie)`, `< received(charlie, Hi Alice)`.
- **Bob** panel shows `< befriend(alice, req(1))`, `> decision(yes, alice, req(1))`, `< connected(alice)`, `< received(alice, hello)`, `> connect(charlie)`, `< connected(charlie)`, `< received(charlie, hello)`, `> introduce(alice, charlie)`.
- **Charlie** panel shows `< befriend(bob, req(1))`, `> decision(yes, bob, req(1))`, `> send(bob, hello)`, `< connected(bob)`, `< befriend_intro(bob, alice, req(2))`, `> accept_intro(alice, req(2))`, `< connected(alice)`, `< received(alice, Hi Charlie)`, `> send(alice, Hi Alice)`.

The `>` prefix marks `cmd(...)` lines (commands the actor issues); the `<` prefix marks `notify(...)` lines (notifications the actor receives) per `_routeOutput` lines 256-258 of the cluster A pairing.

### Phase D.2 — Play 2 (Alice accepts, Charlie rejects)

Click the Play 2 button. The 'main' isolate runs `fplay2/0`. Same protocol as play 1 up through Bob's introduce(alice, charlie), then Charlie REJECTS Alice's intro from Bob; Alice gets a `rejected(charlie)` notification.

```glp
GLP> ✓ Loaded project: D:/bstdev/research/GLP/GLP/olamni/tutorial/ch07/simple-multimodule
GLP> Goal reduction limit set to 1000000
GLP> tagged(alice, cmd(connect(bob)))
tagged(bob, notify(befriend(alice, req(1))))
tagged(bob, cmd(decision(yes, alice, req(1))))
tagged(bob, notify(connected(alice)))
tagged(alice, notify(connected(bob)))
tagged(alice, cmd(send(bob, hello)))
tagged(bob, notify(received(alice, hello)))
tagged(bob, cmd(connect(charlie)))
tagged(charlie, notify(befriend(bob, req(1))))
tagged(charlie, cmd(decision(yes, bob, req(1))))
tagged(charlie, cmd(send(bob, hello)))
tagged(charlie, notify(connected(bob)))
tagged(bob, notify(connected(charlie)))
tagged(bob, notify(received(charlie, hello)))
tagged(bob, cmd(introduce(alice, charlie)))
tagged(charlie, notify(befriend_intro(bob, alice, req(2))))
tagged(alice, notify(befriend_intro(bob, charlie, req(1))))
tagged(charlie, cmd(reject_intro(alice, req(2))))
tagged(alice, cmd(accept_intro(charlie, req(1))))
tagged(alice, notify(rejected(charlie)))
→ suspended
```

Annotation: Charlie's actor uses `cmd(reject_intro(...))` instead of `cmd(accept_intro(...))`. Alice's accept goes nowhere (Charlie's reject already propagated through Bob's mediator). Alice's panel ends with `< rejected(charlie)` per the §7.3 protocol's reject branch in `agent.glp`'s `agent(Id, [msg(_user, Id1, decision(no, From, _)) | _], ...)` clause.

### Phase D.3 — Play 3 (both reject)

Click the Play 3 button. The 'main' isolate runs `fplay3/0`. Same protocol up through Bob's introduce, then BOTH Charlie AND Alice issue `reject_intro(...)`:

```glp
GLP> ✓ Loaded project: D:/bstdev/research/GLP/GLP/olamni/tutorial/ch07/simple-multimodule
GLP> Goal reduction limit set to 1000000
GLP> tagged(alice, cmd(connect(bob)))
tagged(bob, notify(befriend(alice, req(1))))
tagged(bob, cmd(decision(yes, alice, req(1))))
tagged(bob, notify(connected(alice)))
tagged(alice, notify(connected(bob)))
tagged(alice, cmd(send(bob, hello)))
tagged(bob, notify(received(alice, hello)))
tagged(bob, cmd(connect(charlie)))
tagged(charlie, notify(befriend(bob, req(1))))
tagged(charlie, cmd(decision(yes, bob, req(1))))
tagged(charlie, cmd(send(bob, hello)))
tagged(charlie, notify(connected(bob)))
tagged(bob, notify(connected(charlie)))
tagged(bob, notify(received(charlie, hello)))
tagged(bob, cmd(introduce(alice, charlie)))
tagged(charlie, notify(befriend_intro(bob, alice, req(2))))
tagged(alice, notify(befriend_intro(bob, charlie, req(1))))
tagged(charlie, cmd(reject_intro(alice, req(2))))
tagged(alice, cmd(reject_intro(charlie, req(1))))
→ suspended
```

Annotation: neither Alice nor Charlie sends a `rejected(...)` notification because both rejected via their `reject_intro(...)` cmd — the mediator's `nack` reply chain closes both intro channels symmetrically. Both panels end at the `cmd(reject_intro(...))` line.

## Phase E — Recommended clean-session block (per FR-005 (b))

If the build cache or pubspec drifts (e.g., after a Flutter SDK upgrade or a `pubspec.yaml` edit), use this clean+rebuild sequence before launching:

```bash
cd D:/bstdev/research/GLP/GLP/glp_multiagent
taskkill /F /IM glp_multiagent.exe 2>NUL
"/c/Users/gavri/flutter/bin/flutter.bat" clean
"/c/Users/gavri/flutter/bin/flutter.bat" pub get
"/c/Users/gavri/flutter/bin/flutter.bat" build windows -t lib/main_olamni_ch07_simple_multimodule.dart
start "" "build\windows\x64\runner\Release\glp_multiagent.exe"
```

Annotation: `taskkill /F /IM glp_multiagent.exe 2>NUL` is the Windows equivalent of `pkill -f glp_multiagent` (macOS/Linux). The 5-step sequence is copy-pastable as a single block; the order matters (kill running, clean cache, refresh deps, rebuild, launch).

## Postscript

This trace demonstrates the §7.1–§7.6 module-system mechanics from cluster A's REPL exercises (ex-01..ex-05) running in the Flutter+play+boot environment per charter §2.2. The cluster A Flutter pairing (`glp_multiagent/lib/main_olamni_ch07_simple_multimodule.dart`, R-011) cloned from `main_cssg_mad_modules.dart` retargets `_projectDir` to `simple-multimodule/`, sets `_bootFileName` to `boot.glp` (the cluster A pruned variant per R-010), and uses a single-isolate spawn for `fplay$N/0` because cluster A's plays 1-3 are 3-agent friend-mediated within ONE GLP goal (not multi-isolate parent_init/child_init like cluster B's CSSG plays 4-7). The byte-equivalent REPL capture above is the same `tagged()` stream the Flutter app's `_routeOutput` parses into per-agent panels. The cluster A canonical source is `programs/cssg_modules/{self.glp, agent.glp, ui/{mediator.glp, actors.glp}, boot.glp}` per FR-019; cluster A's tutorial-side copy at `olamni/tutorial/ch07/simple-multimodule/` keeps four files byte-exact and prunes only `boot.glp` per Q1+Q5+Q1a + R-010.

This exercise concludes cluster A. The cluster boundary gate flips to `approved 2026-05-03` after this trace lands. Cluster B work (ex-07..ex-11 already approved + ex-12 still pending Flutter manual) follows.
