> **SUPERSEDED 2026-05-04** — This file is from the prior ch07 implementation (`26e01792` / `f094f9db`) that was rejected by the project owner. Preserved per the no-removal directive but **not part of this chapter's runnable content**. The current ch07 (one-play-per-exercise REPL walkthroughs) is at the [chapter signpost](../ch07_tutorial.md). Content below is the prior implementation, kept as record.

---

# ex-12 — Flutter trace (cluster B cssg-modules)

**Status**: TODO — pending manual Flutter test by project owner per spec FR-017 + Q4a locked subset.

The flutter-trace-format contract requires this file be byte-equal to a captured Flutter session from a manually-verified run. Per FR-017:

> The implementer MUST manually test the Flutter app + capture the trace BEFORE writing the tutorial.md. NO synthesised traces.

The cluster B Flutter pairing (`glp_multiagent/lib/main_olamni_ch07_cssg.dart`) was created and verified to BUILD by the implementing session. The actual run + trace capture is deferred to the project owner.

## Manual test procedure (per Q4a locked subset)

1. Verify Flutter (see ex-06 setup walkthrough for the chapter's primary Flutter pre-flight).
2. Build: `cd D:/bstdev/research/GLP/GLP/glp_multiagent && /c/Users/gavri/flutter/bin/flutter.bat build windows -t lib/main_olamni_ch07_cssg.dart`.
3. Launch: `./build/windows/x64/runner/Release/glp_multiagent.exe`.
4. Click Play 1, observe cold-call both-accept (Alice/Bob/Charlie via single isolate; cluster B's _agentInfos has only Alice/Carol/Bob/Dave panels — Charlie's output may not appear, this is documented; see ex-12-tutorial.md).
5. Click Play 2, observe cold-call asymmetric (Charlie rejects).
6. Click Play 3, observe cold-call both reject.
7. Click Play 4, observe CSSG parent-mediated all-four-accept (Alice/Carol/Bob/Dave panels all populated via 4-isolate parent_init + child_init).
8. Click Play 5, observe CSSG Bob-rejects.
9. Capture per-agent panel content + platform log file (`%TEMP%\glp_multiagent_trace.log` on Windows).
10. Replace this file's content with the structured trace per `specs/008-tutorial-ch07/contracts/flutter-trace-format.md` (Phase A pre-flight + Phase B build + Phase C launch + Phase D per-play 1..5 + back-reference to ex-06's clean-session block).

## See also

- `ex-12-tutorial.md` — the learner-facing step-through with build/launch/per-play behavior.
- `ex-06-tutorial.md` — the chapter's primary Flutter setup walkthrough (single source for setup; ex-12 references back to it).
- `specs/008-tutorial-ch07/contracts/flutter-trace-format.md` — the trace contract.
