# SUPERSEDED 2026-05-04

This directory was created by the prior ch07 implementation at `26e01792` (2026-05-02) as a "cluster B" copy of `programs/cssg_modules/`, claimed byte-exact but actually with a 6-line header prepended to each `.glp` file. The Section R test mirror in `test/run_all_tests.sh` worked around this by stripping the first six lines via `tail -n +7` before diffing — a workaround that contradicted the byte-exact claim in the file headers.

The cluster A/B split was rejected by the project owner. The current ch07 implementation (v2026.05.04) uses the canonical `programs/cssg_modules/` project directly — no derivative copies. See [`../ch07_tutorial.md`](../ch07_tutorial.md) for the chapter's current shape.

**This directory is preserved per the no-removal directive but is not part of the chapter's runnable content.** The Flutter pairing at `glp_multiagent/lib/main_olamni_ch07_cssg.dart` still targets this directory and is also preserved on disk.
