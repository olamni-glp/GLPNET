# GLP Canonical Channel/Stream Forms — REPL-verified (2026-06-06)

These forms were verified objectively by loading minimal programs in the GLP REPL
(`glp_runtime/glp_repl.exe`; load = SRSW → partial-eval → typecheck → compile). They are the
authoritative reference for fixing the feature-025 exemplars. **Where the
`contracts/glp-correctness-review.md` adversarial review disagrees with a form below, the REPL
wins** (the review was right on most findings but WRONG on the consumer-close idiom — see note).

Grounded additionally in `programs/self.glp:90-97` and `programs/typed_book/social_graph/play_alice_bob_simple.glp` (proven loadable).

## Verified forms

| # | Role | Canonical form | REPL |
|---|---|---|---|
| 1 | **Send / out-relay** (write one elem onto a channel's outbound, thread the channel) | `send(X, ch(In, [X?\|Out?]), ch(In?, Out)).` — arg2 has **writer `In`**, NOT `In?` | PASS (matches self.glp:94) |
| 2 | **Producer** — writes outbound, IGNORES inbound | `prod(ch(_, Out?), _) :- gen(Vals, Out).` — **bare `_`** for ignored inbound; `Out?` reader-hole; body writes `Out` | PASS |
| 3 | **Consumer** — reads inbound, CLOSES/abandons outbound | `cons(ch(In, []), _) :- rd(In?).` — `In` **writer** captures inbound (read via `In?` in body); **`[]` head-constructs the closed outbound** | PASS |
| 4 | **Bidirectional** — reads inbound AND writes outbound | `dev(ch(In, Out?), _) :- proc(In?, Out).` — consumed-channel head `ch(In, Out?)` | PASS |
| 5 | **Stream producer recursion** | `gen([V\|Vs], [V?\|Out?]) :- gen(Vs?, Out).` / `gen([], []).` | PASS |
| 6 | **Stream consumer recursion** | `rd([V\|In]) :- ground(V?) \| rd(In?).` / `rd([]).` | PASS |
| 7 | **Output produced by a body subgoal** | `f(X, Y?) :- ... \| g(X?, Y).` — output **reader-hole `Y?`** in head, writer `Y` in body (manual §19.4) | — |

## Verified ANTI-forms (these FAIL — do not use)

| Bad form | Why it fails (REPL verdict) |
|---|---|
| `prod(ch(_In, Out?))` (named `_In` at a channel slot) | `[codegen] Undefined variable: _In` — a named anon at an unused slot is rejected; use **bare `_`** |
| `prod(ch(_, Out?), _Faults)` (named `_Faults` unused top-level arg) | `[codegen] Undefined variable: _Faults` — ignored top-level arg must be **bare `_`** |
| `prod(ch(_In, Out))` (outbound bare writer in head + body) | type error: mode mismatch (writer ↑ vs ↓) — 2 writers / 0 readers |
| `cons(ch(In, _))` (bare `_` at outbound when `In` is a writer) | type error: mode mismatch on the outbound slot |
| `cons(ch(In, Out?)) :- rd(In?), Out = [].` (reviewer's H5/H6 "fix") | type error: `writer requires ↑, got ↓` — **the review's proposed consumer fix is WRONG** |
| `cons(ch(In, Out?)) :- rd(In?).` (unfilled outbound hole) | SRSW violation (reader hole never written) |

## The two rules that resolve almost every finding

1. **Consumed-channel head = `ch(In, Out?)`**: `In` writer captures inbound (body reads `In?`); `Out?` reader-hole is the outbound (body writes `Out`). A produced-channel output arg carries the reader-hole in the head + the writer in the body.
2. **Ignored positions use bare `_`** (channel slot or top-level arg) — never a named `_Foo` at an unused position. To CLOSE a one-way consumer's outbound, head-construct `[]` (`ch(In, [])`), which the REPL accepts.

## Correction to the adversarial review

`contracts/glp-correctness-review.md` H5/H6 claimed the consumer form `ch(In, [])` "suspends forever / is a read-match" and proposed a body `Out = []`. **The REPL refutes this:** `ch(In, [])` loads cleanly (form #3 above) and the proposed `Out = []` body-fix does NOT compile. Keep `ch(In, [])`. The review's other HIGH findings (H1 send-shape, H2/H3 output holes, H4/H7 producer double-writer, H8–H15 double-inverted channel heads, M1 output holes, M3/M11 `Fault` needs `closed/2`, M8 `Link` type) stand and are fixed against forms #1–#7 here.
