---
path: test/circular_term_test.dart
name: circular_term_test.dart
purpose: 'Tests for circular term handling in GLP runtime.


  Circular terms can form through cross-goal communication when two goals

  share variables and bind them in ways that create cycles. These tests

  verify that the runtime handles such terms gracefully:

  - ground/1 guard terminates and correctly identifies ground circular terms

  - =?= equality terminates and correctly compares circular terms

  - copy_term/2 preserves cyclic structure in copies

  '
key_idea: 'Tests for circular term handling in GLP runtime.


  Circular terms can form through cross-goal communication when two goals

  share variables and bind them in ways that create cycles. These tests

  verify that the runtime handles such terms gracefully:

  - ground/1 guard terminates and correctly identifies ground circular terms

  - =?= equality terminates and correctly compares circular terms

  - copy_term/2 preserves cyclic structure in copies

  '
dependencies:
- lib/runtime/runtime.dart
- lib/runtime/system_predicates.dart
- lib/runtime/system_predicates_impl.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:15.245Z'
sha256: 13325b134ab40b28f0b298af90405dcdd2f608c084cecd20446828be4f7b8db2
topo_level: 6
cycle_group_id: 75
status: pending
target_path: test/circular_term_test.cs
plan_started_at: '2026-05-21T16:28:58Z'
plan_completed_at: '2026-05-21T16:33:25Z'
plan_path: .codeconv/conversion-plans/test/circular_term_test.dart.md
open_escalation_count: 0
---

Tests for circular term handling in GLP runtime.

Circular terms can form through cross-goal communication when two goals
share variables and bind them in ways that create cycles. These tests
verify that the runtime handles such terms gracefully:
- ground/1 guard terminates and correctly identifies ground circular terms
- =?= equality terminates and correctly compares circular terms
- copy_term/2 preserves cyclic structure in copies
