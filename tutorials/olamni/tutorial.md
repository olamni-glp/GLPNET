# Olamni Tutorial — *The Art of Grassroots Logic Programming*

A self-paced tutorial accompanying Shapiro (2025). Each chapter has 1–N runnable exercises that you load in the GLP REPL and step through with the help of an `ex-NN-tutorial.md` guide and an `ex-NN-repl-trace.md` known-good capture.

## Build the REPL once

```bash
dart compile exe glp_runtime/bin/glp_repl.dart -o glp_runtime/glp_repl.exe
```

This produces a single binary (`.exe` on Windows; unsuffixed on Linux/macOS) at `glp_runtime/glp_repl.exe`. Dart SDK requirement: `^3.9.4`.

## Chapter status

| # | Chapter | Tutorial entry | Status |
|---|---|---|---|
| 1 | Introduction (Fair Stream Merger) | [`ch01/ch01_tutorial.md`](ch01/ch01_tutorial.md) | implemented 2026-04-28 |
| 2 | Logic Programs and Linear Logic | [`ch02/ch02_tutorial.md`](ch02/ch02_tutorial.md) | implemented 2026-04-29 |
| 3 | GLP Core | [`ch03/ch03_tutorial.md`](ch03/ch03_tutorial.md) | implemented 2026-04-30 |
| 4 | Basic Concurrent Programming | [`ch04/ch04_tutorial.md`](ch04/ch04_tutorial.md) | pending review (2026-04-30) |
| 5 | Types and Modes | [`ch05/ch05_tutorial.md`](ch05/ch05_tutorial.md) | implemented 2026-05-01 |
| 6 | Typed Programming | [`ch06/ch06_tutorial.md`](ch06/ch06_tutorial.md) | implemented 2026-05-01[^ch06-synth] |
| 7 | Module System | [`ch07/ch07_tutorial.md`](ch07/ch07_tutorial.md) | implemented 2026-05-04[^ch07-remediation] |
| 8 | The Grassroots Social Graph | [`ch08/ch08-sources.md`](ch08/ch08-sources.md) | planned |
| 9 | Social Networks | [`ch09/ch09-sources.md`](ch09/ch09-sources.md) | planned |
| 10 | Interlaced Streams | [`ch10/ch10-sources.md`](ch10/ch10-sources.md) | planned |
| 11 | Grassroots Cryptocurrencies | [`ch11/ch11-sources.md`](ch11/ch11-sources.md) | planned |
| 12 | Constitutional Consensus | [`ch12/ch12-sources.md`](ch12/ch12-sources.md) | planned |
| 13 | (bonus, Python actors) | [`ch13/ch13-sources.md`](ch13/ch13-sources.md) | planned (scenario TBD) |

## Prerequisites

- A copy of *The Art of Grassroots Logic Programming* (Shapiro, 2025) PDF for cross-reference (this repo's `GLP_ART.pdf`).
- Dart SDK `^3.9.4` on `PATH` (`dart --version` to check).
- The GLP REPL built from this repo (one-time, see above).
- Working knowledge of Markdown rendering (any modern editor or GitHub view works).

## How to use this tutorial

The tutorial is **section-driven for chapters 1–6**: each substantial Program from the book has its own `.glp` file, with `%%` paraphrase comments tied to the surrounding prose paragraphs. From chapter 7 onward, it's **use-case-driven**[^ch07-transition]: each chapter has one or more project subdirectories with the multi-actor `{self.glp, agent.glp, network.glp, actors.glp, boot.glp}` shape paired with a Flutter `main_olamni_chNN_<use-case>.dart` entry point.

Per-chapter tutorial pages (`chNN_tutorial.md`) signpost the exercises within their chapter and carry a date-stamped status block tracking approval state per exercise. Each exercise lives in its own folder (`exercise-01/`, `exercise-02/`, …) and contains a `.glp` source file, a step-through guide, and a captured REPL trace.

The full design rationale lives in [`charter.md`](charter.md). The project Constitution's Principle VI (Tutorial Charter Compliance) governs all tutorial work — divergences from the charter are amended into the charter first, then the work proceeds.

## Status legend

- **implemented** — exercise files exist and the project owner has approved them.
- **pending review** — exercise files exist; awaiting project-owner approval.
- **planned** — sources file present; exercise not yet implemented.
- **stub in PDF** — the book chapter itself has only headings; tutorial cannot proceed until source content is available.

[^ch06-synth]: ch06 content is synthesised from ch01–ch05 sources per /buildkit-clarify Q1 — the ch06 PDF chapter (book p 53) is a stub containing only the chapter title, a one-line intro, and the five §6.x section headings (no body text, no native Programs). See `ch06/ch06_tutorial.md` for the per-exercise synthesis source map.

[^ch07-remediation]: ch07 was implemented 2026-05-04 as seven exercises, one per fplay (`fplay1..fplay7`), against the canonical `programs/cssg_modules/` project. Each exercise is a multi-goal interactive REPL walkthrough that recreates each component of the play's body individually, observes the bindings, then runs the full play. The prior implementation at `26e01792` (2026-05-02) and `f094f9db` (2026-05-03) used a confabulated cluster A/B split with synthesised content; it was rejected by the project owner and replaced. The prior commits are preserved in git history; the cluster A/B subdirectory copies + Flutter pairings + Section R test mirror are preserved on disk per the no-removal directive but are not part of the chapter's runnable content.

[^ch07-transition]: ch07 is the concrete transition example from single-file exercises (chapters 1–6) to multimodule projects (chapter 7 onward). The runnable substrate is the §7.7 CSSG validation example at `programs/cssg_modules/` — four modules (`agent.glp`, `ui/mediator.glp`, `ui/actors.glp`, `boot.glp` + `self.glp`) implementing seven plays. From ch07 onward each chapter's exercises step through one play at a time as multi-goal interactive REPL walkthroughs.
