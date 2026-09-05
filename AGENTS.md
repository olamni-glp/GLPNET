# Instructions for Codex — glpnet

## 1 · CLAUDE.md IS THE SINGLE SOURCE OF TRUTH. READ IT.

**Everything that governs work in this repo lives in [`CLAUDE.md`](CLAUDE.md).** Repo identity,
working modes, spec-first development, the bug protocol, the test protocol, git workflow, the
PGLite data-dir rule, directory structure and the GLP quick reference are all there and are
maintained there. **Read `CLAUDE.md` and follow it.**

This file used to be a full copy of those instructions. It is not one any more, and it must never
become one again — the copy drifted for 111 days and, by 2026-09-05, was actively dangerous:

| what the stale copy said | what was actually true |
|---|---|
| "`D:` is exFAT; always use `--data-dir C:/pglite/research/glpnet`" | **`C:\pglite\research\glpnet` is STRICTLY PROHIBITED.** `D:` is NTFS and the canonical cluster is the repo-local `D:/bstdev/research/glp/glpnet/.pgdb`. Acting on the stale line would have recreated a forbidden cluster. |
| CalVer `vYYYY.MM.DD[-N]` on `main`, feature branches only | buildkit GitFlow: feature → `develop` → `release/*` → `main`, tags `vYYYY.MM.DD.N` cut by `buildkit release` |

**The rule this repo already applies to specs applies to instructions too: one authority per
subject, and other documents reference it rather than duplicating it.** If you need a rule, read
`CLAUDE.md`. If a rule is wrong, fix it in `CLAUDE.md`.

---

## 2 · 🔴 THE MANDATORY-READING STOP DOES NOT APPLY TO A NON-INTERACTIVE INVOCATION

`CLAUDE.md` opens with a mandatory-reading sequence that ends **"STOP AND WAIT. Do not read other
files until Gabi gives direction."** That rule exists for an *interactive* session, where direction
arrives in the next turn.

> ### A non-interactive invocation — `codex exec`, a review, an audit, a scripted analysis — HAS
> ### ALREADY BEEN GIVEN ITS DIRECTION: the prompt it was started with. **Do not stop after the
> ### mandatory reading. Carry out the task in the prompt.**

**This is not a liberty; it is a correction of a measured defect.** On 2026-09-05 an adversarial
`codex exec` review of feature 105 dutifully read all four documents, announced *"Per AGENTS.md, I
am stopping before reading any review-scope files"*, and exited — **reporting zero findings having
analysed nothing.** A reader seeing "0 findings" would reasonably have called the branch clean.
It was not: re-run with this carve-out stated, the same review returned **eleven findings, five of
them HIGH**, including an election-integrity defect that silently counted a vote for the wrong
party. The stop rule had quietly disabled the repo's own second instrument, and the only reason
anyone noticed is that the empty result was recorded as **INCONCLUSIVE rather than clean**.

### 2.1 · Read only what the task needs

`docs/DISCIPLINE.md`, `docs/typed-glp-manual.md` and `docs/glp-cheat-sheet.md` are the **GLP
language** manuals. Read them when your task touches `.glp` source, the parser, the type checker,
the compiler or the runtime — and then read them properly, because
**§1.14 of `CLAUDE.md` is absolute: never program on ignorance of GLP and its type system.**

When the task touches none of that — reviewing C#, Python or shell; auditing the fleet tooling;
reading the COOP mailbox — **skip them and say that you skipped them.** Reading four language
manuals to review a C# file spends the context window that the review needed.

---

## 3 · Tooling facts specific to running Codex on this host

- **PowerShell is blocked by policy here.** `pwsh.exe` / `powershell.exe` invocations are rejected,
  including when a generic shell wrapper reaches for one to run a `cmd` payload. Use `rg`, `git`,
  `sed`, `cat`, `python`, or a non-shell file-reading capability.
- **A rejected tool call is not a result.** Switch tools and continue. If you truly cannot read the
  code, **say so explicitly as your answer** — never report "no findings" when the honest answer is
  "I could not look". An empty review must be recorded as INCONCLUSIVE, not clean.
- `codex exec` takes its prompt on **stdin**, not as an argument (as an argument it hangs on stdin).
- `dotnet build` can fail on a stale `VBCSCompiler` lock. **Do not kill it** — it may belong to
  another lane's session. Add `-p:EnableSourceLink=false -m:1`.
- Set `PYTHONUTF8=1`; the cp1252 console chokes on rich's `→`.

---

## 4 · What this file is allowed to contain

Only the two things above: the pointer to `CLAUDE.md`, and guidance that is **specific to
non-interactive/Codex execution** and therefore has no home in `CLAUDE.md`.

**Do not copy repo rules into this file.** If you find yourself about to, the rule belongs in
`CLAUDE.md` and this file should reference it. That discipline is the whole reason the table in §1
can never happen again.
