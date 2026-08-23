<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Specification Quality Checklist: bk-onrestart per-host configurable auto-installable fleet resume

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-23
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [ ] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

**One open marker, deliberately retained for `/bk-clarify`.**

- **FR-029** — is distributing a host profile *to* peer hosts in scope, or is each host solely
  responsible for its own? Two reasonable readings with materially different scope, and the
  answer touches a recorded open block on authority for fleet-binding one-way actions
  (marathon discharge items Q6/Q50/N11 on `mrun-f5ef56dba3c1`). No reasonable default exists
  that the engineer has not reserved to themselves, so this is not guessed.

**Iteration 1 fixes applied before this pass:**

- Success criteria were initially phrased partly in terms of tabs and scheduled tasks;
  rewritten as groups/arrangements and automatic-resume state so SC-002/SC-007/SC-008 are
  technology-agnostic.
- FR-011 was initially "verify the launch succeeded", which is the exact ambiguity that lets a
  launcher trust its own success message; tightened to observing a live session.
- The reference-implementation details (`post-reboot-restart.ps1`, `-Layout`, `wt.exe`) were
  moved out of requirements and into Assumptions, where they are named as *reference behaviour
  to generalise* rather than as the deliverable.

**Grounding measured this session, not recalled** (evidence for the problem statement):

- `post-reboot-restart.ps1:62` — `[ValidateSet('Windows','Tabs')][string]$Layout = 'Windows'`,
  while every host in the fleet wants `Tabs`. The default is the wrong value.
- `post-reboot-restart.ps1:110` — `$Repos = @(…)`, a hardcoded array of 12 repo paths.
- `post-reboot-restart.ps1:~278` — the `Tabs` branch prints `"Launched 1 window with N tabs."`
  immediately after `Start-Process`, with no check that any session came up. This is the
  measured N-tabs-0-sessions failure mode, unguarded in the reference implementation.
