> **SUPERSEDED 2026-05-04** — This file is from the prior ch07 implementation (`26e01792` / `f094f9db`) that was rejected by the project owner. Preserved per the no-removal directive but **not part of this chapter's runnable content**. The current ch07 (one-play-per-exercise REPL walkthroughs) is at the [chapter signpost](../ch07_tutorial.md). Content below is the prior implementation, kept as record.

---

# Exercise 12 — Cluster B CSSG plays in Flutter (per Q4a locked subset)

Welcome to chapter 7, exercise 12 — the final exercise of cluster B and
the chapter as a whole.  This is the **CSSG-in-Flutter** pairing: the
cluster B Flutter walkthrough that runs the Q4a-locked play subset
(`play1` + `play2` + `play3` + `play4` + `play5`) end to end inside
the multimodule Flutter app
`glp_multiagent/lib/main_olamni_ch07_cssg.dart`.  Where ex-06 walked
cluster A's simple-multimodule pairing through its three 3-agent
plays, ex-12 retargets the same Flutter framework to the larger
4-agent CSSG topology and exercises both halves of §7.7's use-case
set: the §7.3 cold-call introduction protocol (plays 1–3) and the
§7.7 parent-mediated child-introduction protocol (plays 4–5).

## Prerequisites

- ex-06 has been completed (the chapter's **single** Flutter setup
  walkthrough — Flutter SDK verification, recommended clean session,
  build + launch + log-file path conventions are all established
  there).  Per spec US4 acceptance scenario "ex-12 reuses the Flutter
  setup established in ex-06" — none of that material is repeated
  here.
- Cluster B's Flutter pairing
  (`glp_multiagent/lib/main_olamni_ch07_cssg.dart`) builds cleanly
  on the implementing host (verified at /speckit-implement T032).
- ex-07 through ex-11 are all approved (the within-cluster pairwise
  gates from `ch07_tutorial.md`'s status block — the prior 5 cluster
  B REPL exercises must be approved before ex-12 work begins).

## What you'll learn

1. **Cluster B's CSSG plays running in Flutter.**  The same five plays
   you saw run as REPL goals in ex-08 (plays 1–3, cold-call) and
   ex-09 (plays 4–5, parent-mediated child intro) running in the
   Flutter window — with one panel per agent and live tagged-output
   streaming as the protocol executes.
2. **The parent-mediated child-introduction protocol's UI flow.**
   Plays 4 and 5 are the §7.7 distinguishing scenario: each parent
   must explicitly approve the proposed child befriending before
   the children can connect.  In the Flutter app you can watch this
   approval gate fire across the 4 panels live.
3. **The multi-actor 4-panel layout (Alice / Carol / Bob / Dave).**
   Cluster B's panel layout is fixed at 4 columns: Alice (parent) /
   Carol (child) / Bob (parent) / Dave (child) — grouped by family.
   This is identical to canonical
   `main_cssg_mad_modules.dart`'s `_agentInfos`; cluster A's pairing
   has a different (3-agent) panel layout.
4. **The Q4a locked 5-play subset.**  Plays 1–5 cover all three of
   §7.7's use cases (cold-call befriending; friend-mediated
   introduction with accept and reject; parent-mediated child intro
   with accept and Bob-rejects).  Plays 6 and 7 (Carol-rejects-child
   intro and Dave-rejects-child intro variants) are demonstrated in
   cluster B REPL ex-10, NOT in Flutter ex-12.

## Setup reference

This exercise does NOT repeat the Flutter pre-flight from ex-06.
See `olamni/tutorial/ch07/exercise-06/ex-06-tutorial.md` for:

- Flutter SDK version verification (`flutter --version`,
  `flutter doctor`).
- The recommended clean-session block (`pkill` + `flutter clean` +
  `flutter pub get`) — apply it here too if you have a stale build
  cache or environment drift since ex-06.
- The platform log file path convention (`%TEMP%\glp_multiagent_trace.log`
  on Windows; `/private/tmp/glp_multiagent_trace.log` on macOS;
  `/tmp/glp_multiagent_trace.log` on Linux).

If ex-06 ran successfully on this host, the same Flutter SDK and the
same clean-session machinery work for ex-12.  The only difference is
the `-t` target file passed to `flutter build`.

## Build cluster B Flutter pairing

```bash
cd D:/bstdev/research/GLP/GLP/glp_multiagent
/c/Users/gavri/flutter/bin/flutter.bat build windows -t lib/main_olamni_ch07_cssg.dart
```

Expected: `flutter build windows` completes with `Built build\windows\x64\runner\Release\glp_multiagent.exe` (or the
platform-equivalent line on macOS / Linux).  Cross-check: trace's
**Phase B**.  If the build fails, halt per FR-013 and report — do
NOT silently substitute another build target.

## Launch cluster B Flutter pairing

```bash
./build/windows/x64/runner/Release/glp_multiagent.exe
```

Expected: the Flutter window opens, the app bar reads
`ch07 cluster B — cssg-modules`, and the control bar shows seven
buttons (Play 1 through Play 7) above an initially empty panel area
with the message `Click a Play button above to run a scenario.` and
the bottom log line `Using cluster B cssg-modules project (byte-exact
from programs/cssg_modules/).`  Cross-check: trace's **Phase C**.

## The locked play subset (Q4a)

Per Q-amendment Q4a (spec.md Clarifications), ex-12 covers plays
1–5 (the locked 5-play subset out of the canonical 7) so that all
three of §7.7's use cases are demonstrated in Flutter:

- **Play 1** (cold-call, both accept) — cluster B's
  Alice / Bob / Charlie introduction protocol via single-isolate
  `fplay1`.  Both Alice and Charlie accept the friend-mediated
  introduction; the protocol runs to its happy-path completion.
- **Play 2** (cold-call, asymmetric) — Alice connects to Bob; Bob
  accepts the cold-call; Charlie rejects the friend-mediated
  introduction from Bob.  Alice waits on
  `[rejected(charlie)|_]` and the actor scripts terminate.
- **Play 3** (cold-call, both reject) — Alice rejects the
  friend-mediated introduction proposed by Bob (note: in this play
  it is Alice who rejects, mirroring play2's Charlie-rejects
  branch).  Charlie also rejects on his own side.
- **Play 4** (parent-mediated child intro, all 4 accept) — Alice's
  child Carol meets Bob's child Dave through both parents'
  approval gates.  Alice issues `child_introduce(carol, bob, dave)`;
  Bob's `bob4_wait_child_intro` issues
  `approve_child_intro(carol, dave, ReqId?)`; Carol and Dave both
  accept on their child channels; the children exchange greetings.
- **Play 5** (parent-mediated child intro, Bob rejects) — Alice
  again issues `child_introduce(carol, bob, dave)`, but this time
  Bob's `bob5_wait_child_intro` issues
  `reject_child_intro(carol, ReqId?)`.  Carol's
  `carol5_wait_rejected` matches `[rejected(dave)|_]` and Carol's
  panel terminates; Dave's `dave5(ch(_, []))` clause means Dave's
  panel never receives any messages.

Plays 6 and 7 (Carol-rejects-child-intro and Dave-rejects-child-intro
variants) are demonstrated in cluster B REPL ex-10, NOT in Flutter
ex-12.  The cluster B Flutter pairing's UI exposes Play 6 and Play 7
buttons for completeness (matching the canonical
`main_cssg_mad_modules.dart`), but they are out of scope for this
exercise.

## Run the 5 plays

For each play in the locked subset, click the corresponding Play
button in the Flutter app's control bar and observe the per-agent
panels.  Cluster B's panel layout is fixed at 4 columns: **Alice
(Parent)** / **Carol (Child)** / **Bob (Parent)** / **Dave (Child)**.

### Play 1 — Cold-call, both accept (single-isolate `fplay1`)

Click **Play 1**.  Cluster B's spawn config for plays 1–3 uses a
**single-isolate** spawn (per
`main_olamni_ch07_cssg.dart`'s `_cssgSpawnConfigs(playNum)` clause:
`if (playNum >= 1 && playNum <= 3)` returns a single
`_SpawnConfig('main', 'fplay$playNum/0', [])`).  The single isolate
runs `fplay1/0` which wires `alice1` + `bob1` + `charlie1` through
`network3/3`.

Expected on-screen behaviour:

- **Alice panel**: `> connect(bob)` then `> send(bob, hello)` then
  `> accept_intro(charlie, ReqId?)` then `> send(charlie, 'Hi
  Charlie')`.  Notify lines (`<`-prefixed) for `connected(bob)`,
  `befriend_intro(bob, charlie, ReqId)`, `connected(charlie)`,
  `received(charlie, 'Hi Alice')`.
- **Bob panel**: `> decision(yes, alice, ReqId?)` then `>
  connect(charlie)` then `> introduce(alice, charlie)`.  Notify
  lines for `befriend(alice, ReqId)`, `connected(alice)`,
  `received(alice, hello)`, `connected(charlie)`,
  `received(charlie, 'Hi Charlie')`.
- **Carol panel** (cluster B's 2nd column): **empty** for plays 1–3.
  This is a documented limitation — the 3-agent plays' Charlie has
  no Carol panel to route to (cluster B's `_agentInfos` is fixed at
  Alice / Carol / Bob / Dave, NO Charlie).  Charlie's tagged output
  is silently dropped by `_routeOutput` because no
  `_AgentState['Charlie']` exists.  See cluster A's ex-06 pairing
  (`main_olamni_ch07_simple_multimodule.dart`) for the 3-agent
  Alice / Bob / Charlie panel layout if you need to observe
  Charlie's flow.
- **Dave panel**: also **empty** for plays 1–3.

The visible mismatch (3-agent play running on a 4-panel layout) is
intentional: cluster B's pairing is primarily a 4-agent CSSG
demonstration; reusing it for the 3-agent plays simply means two
panels stay empty.  Cluster A's pairing handles the 3-agent plays
with an Alice / Bob / Charlie panel layout — see ex-06.

### Play 2 — Cold-call, asymmetric (Charlie rejects)

Click **Play 2**.  Same single-isolate `fplay2` as play 1 but the
actor scripts are `alice2` + `bob2` + `charlie2`.  The divergence is
in Charlie's actor: `charlie2_wait_intro` matches
`befriend_intro(bob, alice, ReqId)` and replies
`reject_intro(alice, ReqId?)`.

Expected on-screen behaviour:

- **Alice panel**: `> connect(bob)` then `> send(bob, hello)` then
  `> accept_intro(charlie, ReqId?)` (Alice still accepts on her
  side).  Notify lines for `connected(bob)`,
  `befriend_intro(bob, charlie, ReqId)`, `rejected(charlie)`.  After
  the rejection, `alice2_wait_rejected` matches `[rejected(charlie)|_]`
  and Alice's panel terminates.
- **Bob panel**: `> decision(yes, alice, ReqId?)` then `>
  connect(charlie)` then `> introduce(alice, charlie)`.  Notify
  lines for `befriend(alice, ReqId)`, `connected(alice)`,
  `received(alice, hello)`, `connected(charlie)`,
  `received(charlie, 'Hi Charlie')`.
- **Carol panel**: empty.
- **Dave panel**: empty.

### Play 3 — Cold-call, both reject

Click **Play 3**.  Same single-isolate `fplay3` as plays 1 and 2 but
the actors are `alice3` + `bob3` + `charlie3`.  Both Alice's
`alice3_wait_intro` and Charlie's `charlie3_wait_intro` issue
`reject_intro(...)` — neither accepts the friend-mediated
introduction.

Expected on-screen behaviour:

- **Alice panel**: `> connect(bob)` then `> send(bob, hello)` then
  `> reject_intro(charlie, ReqId?)`.  After the rejection
  `alice3_wait_intro` reduces to `true` and Alice's panel
  terminates; no `accept_intro` line, no `send(charlie, ...)` line.
- **Bob panel**: same flow as plays 1 and 2 (Bob acts identically
  in all three cold-call plays — the rejection is purely on the
  introducee side).
- **Carol panel**: empty.
- **Dave panel**: empty.

### Play 4 — CSSG, all 4 accept (4-isolate spawn)

Click **Play 4**.  Cluster B's spawn config for plays 4–7 uses
the canonical **4-isolate** spawn (per
`main_olamni_ch07_cssg.dart`'s `_cssgSpawnConfigs(playNum)` else
branch):
- isolate `alice` runs `parent_init/4` with extra args `['carol',
  '4']` (Alice is Carol's parent; play number 4).
- isolate `carol` runs `child_init/3` with extra arg `['4']`.
- isolate `bob` runs `parent_init/4` with extra args `['dave', '4']`.
- isolate `dave` runs `child_init/3` with extra arg `['4']`.

The `parent_init/4` and `child_init/3` clauses (defined in
cluster B's `mad_boot.glp` lines 65–88 and following) wire the
parent's `child_introduce(carol, bob, dave)` issued by `alice4`'s
actor through the parent-approval gate (`approve_child_intro(carol,
dave, ReqId?)` in `bob4_wait_child_intro`) into Carol's
`accept_child_intro(dave, ReqId?)` and Dave's
`accept_child_intro(carol, ReqId?)`.

Expected on-screen behaviour:

- **Alice panel**: `> connect(bob)` then `>
  child_introduce(carol, bob, dave)`.  Notify lines for
  `connected(bob)`.  Once the parent-approval gate completes,
  the child connection is forwarded to Carol via the parent's
  child-output channel.
- **Carol panel**: `> accept_child_intro(dave, ReqId?)` then `>
  send(dave, 'Hi Dave')`.  Notify lines for
  `child_befriend(alice, dave, ReqId)`, `connected(dave)`,
  `received(dave, 'Hi Carol')`.
- **Bob panel**: `> decision(yes, alice, ReqId?)` then `>
  approve_child_intro(carol, dave, ReqId?)`.  Notify lines for
  `befriend(alice, ReqId)`, `child_befriend(alice, carol, ReqId)`.
- **Dave panel**: `> accept_child_intro(carol, ReqId?)` then `>
  send(carol, 'Hi Carol')`.  Notify lines for
  `child_befriend(bob, carol, ReqId)`, `connected(carol)`,
  `received(carol, 'Hi Dave')`.

This is the §7.7 distinguishing scenario: the parent-approval gate
fires explicitly in Bob's panel (`approve_child_intro(carol, dave,
ReqId?)`) BEFORE Carol and Dave's panels see any child-channel
activity.  The four-panel synchronous-looking dance is actually
strictly asynchronous — each isolate runs its own actor + agent
+ mediator pipeline and the messages cross isolate boundaries
through the multi-isolate router.

### Play 5 — CSSG, Bob rejects parent-mediated child intro

Click **Play 5**.  Same 4-isolate spawn as play 4 but the actors
are `alice5` + `bob5` + `carol5` + `dave5`.  The divergence is in
Bob's actor: `bob5_wait_child_intro` matches
`child_befriend(alice, carol, ReqId)` and issues
`reject_child_intro(carol, ReqId?)` — Bob rejects the proposed
child befriending before any child-channel activity occurs on
Carol's or Dave's side.

Expected on-screen behaviour:

- **Alice panel**: `> connect(bob)` then `>
  child_introduce(carol, bob, dave)`.  Notify line for
  `connected(bob)`.  No further activity — once Bob rejects, Alice's
  alice5_wait_connected has already reduced to its terminal clause.
- **Carol panel**: `> accept_child_intro(dave, ReqId?)` (Carol
  optimistically accepts before learning of Bob's rejection).
  Notify lines for `child_befriend(alice, dave, ReqId)` and then
  `rejected(dave)`.  After the rejection
  `carol5_wait_rejected` matches `[rejected(dave)|_]` and Carol's
  panel terminates.
- **Bob panel**: `> decision(yes, alice, ReqId?)` then `>
  reject_child_intro(carol, ReqId?)`.  Notify lines for
  `befriend(alice, ReqId)` and `child_befriend(alice, carol,
  ReqId)`.  Bob's rejection is the last cmd line on his panel.
- **Dave panel**: **empty**.  Dave's actor `dave5(ch(_, []))` has
  an empty output stream — Dave never sends any commands and never
  receives any cross-isolate child-channel messages.

This is the §7.7 reject branch on the parent's side — the parent-
approval gate fires NEGATIVELY in Bob's panel and the rejection
propagates to Carol via the cross-isolate router; Dave's panel
never sees any activity because Bob's reject precedes any child-
channel setup.

## Cluster B Flutter pairing source

The Flutter pairing source is at
`glp_multiagent/lib/main_olamni_ch07_cssg.dart` (534 lines, cloned
from `glp_multiagent/lib/main_cssg_mad_modules.dart` per FR-015 +
FR-020 with `_projectDir` retargeted to the cluster B tutorial-side
project subdir).  Key constants quoted directly from the source:

```dart
/// Project directory for static linking (repo-relative from glp_multiagent/).
const _projectDir = '../olamni/tutorial/ch07/cssg-modules';

/// madGLP boot source — loaded on top of the linked project.
const _bootFileName = 'mad_boot.glp';

/// Panel order: Parent, Child, Parent, Child — grouped by family.
const _agentInfos = [
  _AgentInfo('Alice', 'Parent', Color(0xFF3949AB), Color(0xFFE8EAF6)),
  _AgentInfo('Carol', 'Child',  Color(0xFF7986CB), Color(0xFFF5F5FF)),
  _AgentInfo('Bob',   'Parent', Color(0xFF00897B), Color(0xFFE0F2F1)),
  _AgentInfo('Dave',  'Child',  Color(0xFF4DB6AC), Color(0xFFF5FFFE)),
];

/// Build spawn configs for a given play number.
List<_SpawnConfig> _cssgSpawnConfigs(int playNum) {
  if (playNum >= 1 && playNum <= 3) {
    // 3-agent cold-call plays use single-isolate with all 3 agents tagged
    return [_SpawnConfig('main', 'fplay$playNum/0', [])];
  }
  // CSSG plays 4-7 use 4 isolates with parent_init/child_init
  return [
    _SpawnConfig('alice', 'parent_init/4', ['carol', '$playNum']),
    _SpawnConfig('carol', 'child_init/3', ['$playNum']),
    _SpawnConfig('bob', 'parent_init/4', ['dave', '$playNum']),
    _SpawnConfig('dave', 'child_init/3', ['$playNum']),
  ];
}
```

`_projectDir` points at the **tutorial-side** copy of the CSSG
project (per FR-015's prohibition on Flutter pairings pointing
at canonical `programs/cssg_modules/`) — so any drift between the
two copies is exposed by the Flutter app as well as by Section R's
per-file diff test.  `_bootFileName` is `mad_boot.glp` (per
Q-amendment Q-FR003a — the canonical's top-level file used for
multi-isolate CSSG orchestration; NOT the project loader's
single-isolate `boot.glp`).

## Trace capture

**Status: PENDING MANUAL TEST.**  Per spec FR-017:

> The implementer MUST manually test the Flutter app + capture the
> trace BEFORE writing the tutorial.md.  NO synthesised traces.  If
> the Flutter app fails to launch or behave as expected, halt per
> FR-013 and report.

The cluster B Flutter pairing (`main_olamni_ch07_cssg.dart`) was
created and verified to BUILD by the implementing session (per
T032).  Capturing the actual trace requires a Flutter window-open
event, button click sequence, and per-panel + log-file content
capture — all of which are deferred to the project owner's manual
test run.

The placeholder trace file is at
`olamni/tutorial/ch07/exercise-12/ex-12-flutter-trace.md`; it
documents the manual-test procedure and contains the expected
phases A through D per the
`specs/008-tutorial-ch07/contracts/flutter-trace-format.md`
contract.

## Manual test checklist (for project owner)

1. **Flutter SDK verified.**  See ex-06 for the chapter's primary
   Flutter pre-flight (`flutter --version` + `flutter doctor`).
2. **Build cluster B pairing.**
   ```bash
   cd D:/bstdev/research/GLP/GLP/glp_multiagent
   /c/Users/gavri/flutter/bin/flutter.bat build windows -t lib/main_olamni_ch07_cssg.dart
   ```
3. **Launch.**
   ```bash
   ./build/windows/x64/runner/Release/glp_multiagent.exe
   ```
4. **Click Play 1, Play 2, Play 3, Play 4, Play 5 in sequence**, one
   at a time; wait for each play to settle (panels stop updating)
   before clicking the next.
5. **Capture per-agent panel content + log file.**  The per-platform
   log file path is documented in ex-06; on Windows it is
   `%TEMP%\glp_multiagent_trace.log`.  Save the relevant log
   excerpt + screenshots / panel-text dumps for each play.
6. **Write `ex-12-flutter-trace.md`** byte-equal to the captured
   session per the
   `specs/008-tutorial-ch07/contracts/flutter-trace-format.md`
   contract: Phase A pre-flight (back-reference to ex-06's full
   pre-flight rather than re-printing the entire `flutter --version`
   output) + Phase B build + Phase C launch + Phase D per-play
   1..5.  Phase E (recommended clean-session block) is NOT required
   for ex-12 — it is required only in ex-06 per the
   flutter-trace-format contract.

If the manual test fails (build error, launch error, or any divergence
from the expected per-play behaviour above), halt and report per
FR-013 — do NOT silently substitute a synthesised trace, do NOT skip
the failing play, do NOT relax the Q4a-locked subset.

## Multimodule-project-derivation note

ex-12's source canonical is `programs/cssg_modules/`.  Cluster B
inherits all six files BYTE-EXACT (see ex-07's
multimodule-project-derivation note for the full enumeration).
ex-12 is the second of the chapter's two Flutter exercises — ex-06
covered cluster A's simple-multimodule pairing on three 3-agent
plays; ex-12 covers cluster B's CSSG pairing on the Q4a-locked
5-play subset (3 cold-call + 2 parent-mediated child intro).

The Flutter pairing's source (`main_olamni_ch07_cssg.dart`) is
itself NOT in the cluster B multimodule project — it lives under
`glp_multiagent/lib/` per charter §2.2's Flutter-app-vs-tutorial
split.  The cluster B project on disk under
`olamni/tutorial/ch07/cssg-modules/` is what `_projectDir` points
at; the Flutter pairing's Dart code is the host that the multimodule
project runs inside.

## Next

ex-12 is the **last exercise of chapter 7**.  Once the manual
Flutter test is captured and `ex-12-flutter-trace.md` is finalized
(byte-equal to the captured session), the chapter signpost
(`olamni/tutorial/ch07/ch07_tutorial.md`) flips ex-12's status from
`pending review` to `approved YYYY-MM-DD` and the top-level
`olamni/tutorial/tutorial.md` flips chapter 7's row from
`pending review` to `implemented YYYY-MM-DD`.  At that point,
chapter 7 is complete and the chapter 8 spec workflow can begin per
the workflow memory at
`memory/olamni_tutorial_chapter_workflow.md`.
