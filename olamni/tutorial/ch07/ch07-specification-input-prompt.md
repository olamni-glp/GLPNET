# Chapter 7 — Module System tutorial requirement

Build a tutorial for chapter 7 of the GLP book — "Module System."

Adhere to the style and format of the REPL exercises already established for ch01..ch06. ch07 is fundamentally different from chs 1–6 because it is the first chapter whose runnable example is a complete *set* of modules (multiple `.glp` files) loaded from a root module via the project-loader.

The complete loading — root module + all dependencies via implicit ancestor scoping + cross-module imported procedures — must be accessible to the learner in BOTH the traditional REPL-based exercise format AND the new Flutter+play+boot environment used from chs 7 onward.

Tests that mirror the chapter content must be defined.

The chapter has TWO multimodule clusters:

- Cluster A: the simpler §7.3 social/agent example at the start of the chapter.
- Cluster B: the §7.7 CSSG (Child-Safe Social Graph) validation example in the second part.

Total exercises ≤ 15, with each cluster getting ≥ 5 and ≤ 8.

REPL exercises must contain copy-pastable load lines and goal prompts that the learner can lift verbatim into a REPL session.

ONE Flutter exercise must walk the learner through the full Flutter+play+boot setup — manually verified, copy-pastable terminal commands, including a recommended clean session if needed before launching Flutter with play+boot for either cluster's example.

Cluster A's project source is derived from the canonical CSSG project (`programs/cssg_modules/`) — minimised so the §7.1–§7.6 module-system mechanics are demonstrable on a smaller footprint than the full §7.7 validation example.

Cluster B's project is the byte-exact CSSG project as it ships in the repo — the §7.7 validation example used to demonstrate the module system at scale.

Tests in `test/run_all_tests.sh`: a new dedicated section that (a) loads cluster A's project via the REPL's project-loading mode and runs its locked play sequence, and (b) verifies that cluster B's tutorial-side copy of the project files is byte-exact-equivalent to the canonical `programs/cssg_modules/` source — surfacing any drift as a test failure with a diagnostic naming the offending file.

Cluster A's coverage of §7.x mechanics: project structure / load demo (§7.1–§7.2); procedure declarations — private vs exported vs imported (§7.3); ancestor-scoped types (§7.4); procedure renaming + entry-point aliases (§7.5); end-to-end play run (§7.6 dynamic linking referenced in headers).

Cluster B's coverage of §7.7 use cases: project structure walkthrough (40 types in `self.glp`, 13 private procs in `agent.glp`, exported actors in `ui/actors.glp`, 7 plays in `boot.glp`); cold-call befriending (3-agent plays); friend-mediated/CSSG accept + reject; parent-mediated child introduction with each-party approval-gate variants; cross-module-call inspection.

The chapter signpost at `olamni/tutorial/ch07/ch07_tutorial.md` carries the chapter intro, the build instructions for both REPL and Flutter, the two-cluster pedagogy paragraph, the test-integration paragraph, links to all locked exercises with one-line summaries, and a date-stamped status block (with cluster-A boundary line) tracking exercise approval state.

The top-level `olamni/tutorial/tutorial.md` is updated incrementally — ch07's row flips from `planned` to `pending review (YYYY-MM-DD)` once any cluster A exercise lands, then to `implemented YYYY-MM-DD` once all locked exercises (across both clusters) are approved. The "How to use this tutorial" section's mention of "use-case-driven from chapter 7 onward" gains a footnote pointing at ch07 as the concrete transition example.

Each cluster pairs with a Flutter `glp_multiagent/lib/main_olamni_ch07_<cluster>.dart` file cloned from the canonical `glp_multiagent/lib/main_cssg_mad_modules.dart` template with `_projectDir` retargeted at the cluster's tutorial-side project subdir; this is the charter §2.2 pattern from ch07 onward.

Approval gates: pairwise WITHIN each cluster (ex-(N+1) work begins after ex-N is approved within the same cluster) PLUS a single cluster-boundary gate (ALL cluster B work begins after ALL cluster A exercises are approved AND the chapter signpost records `cluster-A: approved YYYY-MM-DD`).

Manual-test-first discipline for Flutter: the implementing session manually tests + captures the Flutter trace BEFORE writing the Flutter exercise's tutorial; no synthesised Flutter traces.

Halt-and-amend posture: any discrepancy between chapter content and as-loaded REPL or Flutter behaviour halts the implementer and goes through a documented Q-amendment (per the ch02–ch06 precedent), rather than silent spec rewrite or workaround.
