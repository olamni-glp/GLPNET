---
path: test/srsw_test.dart
name: srsw_test.dart
purpose: Verifies the GLP compiler enforces the SRSW (single-reader/single-writer) variable discipline at compile time.
key_idea: 'Four GlpCompiler.compile() cases: repeated var same(f(X,X)) throws; anonymous _ writer with no reader compiles (ops>0); named Result with no reader throws while _ passes; a guard-only reader X under `otherwise` (no groundness) throws.'
dependencies:
- lib/compiler/compiler.dart
callers: []
mtime: '2026-05-21T12:38:15.362Z'
sha256: 651ad3d1b41dabc4cf7d9d2bff2c273d81d7020400a6429734be6bd1b08f240d
topo_level: 8
cycle_group_id: 119
status: pending
target_path: test/srsw_test.cs
plan_started_at: '2026-05-21T16:43:59Z'
plan_completed_at: '2026-05-21T16:49:16Z'
plan_path: .codeconv/conversion-plans/test/srsw_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies the GLP compiler enforces the SRSW (single-reader/single-writer) variable discipline at compile time.
