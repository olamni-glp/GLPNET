# Olamni Tutorial — Plan

Tight implementation plan for the per-chapter companion tutorial to *The Art of Grassroots Logic Programming* (Shapiro, 2025). Per-chapter sub-plans are at `ch01-04_plan.md` (combined) and `chNN/chNN_plan.md` (chs 5–13). Per-chapter source references live in `ch01-04-sources.md` and `chNN/chNN-sources.md`. Each sub-plan is intended to be opened and executed in its own Claude Code session.



## Output target
- Output dir: `olamni/tutorial/chNN/`.
- Filenames: `ch-NN-ex-MM-shortname.glp` (chs 1–6, single files); `chNN/<use-case>/{self,agent,network,actors,boot}.glp` (chs 7–13, project subdirs).

## Scope
- In-chapter Program listings only — NOT end-of-chapter exercises, NOT appendix Selected Exercise Solutions.
- Appendix in scope: Social Networks Code drives ch 9 use cases; Library Utilities feed per-chapter `useful-techniques.glp` files.

## Design principles
1. **Unit of grouping**: section-driven for chs 1–6 (one file per substantial Program); use-case-driven for chs 7–13 (one project per use case). Don't fuse unrelated material; don't split a single use case.
2. **Step-by-step alignment**: every code-bearing section gets a tutorial entry. Reader on §X.Y loads the matching file/project.
3. **Per-chapter useful-techniques.glp**: collects unconnected helper snippets when a chapter has them.
4. **Cumulative agent code** (chs 7–13): later use cases' `agent.glp` includes whatever earlier-section clauses they need to run standalone.
5. **Comments paraphrase book prose**: every clause block carries a `%%` comment derived from the matching paragraph of the book.

## Implementation principles
1. **Stay close to book first**; refactor toward existing typed_book/ patterns only on very close match. Ask Udi when unclear.
2. **Multi-agent template**: project subdirectory shape `{self.glp, agent.glp, network.glp, actors.glp, boot.glp}`. Pair each project with `glp_multiagent/lib/main_olamni_chNN_<use-case>.dart` copied from the canonical Flutter template and retargeted via `_projectDir` and `_SpawnConfig`.
3. **Bonus ch 13**: Python actors instead of Dart/Flutter; bridge over JSON-line stdin/stdout subprocess. Scenario TBD with Udi.
4. **REPL conventions**: `cd glp_runtime/bin; dart run glp_repl.dart`; load file or project; run goal. `→ succeeds` or `→ suspended` (suspended is OK for plays whose channels remain open at end).
5. **Commit-and-push per chapter**: after a chapter lands, run baseline tests to verify no regressions, commit only the new tutorial files (no `git add -A`), push to `claude/<branch>`, ask Udi to merge to main.


## Build order
chs 1–4 (REPL only) → ch 5 → ch 6 → ch 7 (first multi-agent + Flutter) → ch 8 → ch 9 → ch 10 → ch 11 → ch 12 → ch 13 (bonus).


## Testing
Each  for  each Tutorialririal  a  test  plan  with at 3  different  scenarios
of  test inputs  for the  tutorail  code must  be prepred  as  prt of the speclit-specofui  tool  chain  and  this must 
clearly  adress  the  planned  use  cases  from spoecit  for the Tutorial
one the glp  code has been  coded it  must  be  tested using the glp.repl and  supoprtin g tools if  neded  and each  scanrio  must  be turned into  a tst script  tat  drives the siccesfull testing  olf the  glp  code.  traces  of eCH REPL  TEST FOR  EACH TURIOAL  GLP  FILE  MUST  BE SAFED. ON DISK

 

## Verification
The semantics  and authoritative  grammmar  and authoritative  exemplar  code for 
glp  all ,glp  programms  and  inclusin   all  tuturial  coe  is defined
in GLP_IMPLEMENTATION.pdf and GLP_art  and in the examples  in the prpgrammes  and test  fiolder .glp files wrt  to  exemplar  code beyon  the two .pdfs
Were there is  doupt  ciode  most  be  verfied  againt these reference

## Notes carried forward
- §4.3 (chs 1–4 plan) and §12.7/§12.8 (ch 12 plan) inventories captured directly from the PDF; sub-plans now list specific Programs.
- Book draft contains only a TOC page for ch 6 (§6.1–§6.5 listed, no body). Per Udi: build one tutorial file per TOC heading, sourcing material from where the topics actually appear in the book — quicksort from §5.6; buffered communication from §4.2; equator mechanism from `docs/naming-conventions.md` + `programs/typed_book/meta/enhanced/abortable_meta.glp`; difference lists and bidirectional comms from `typed_book/`.
- Ch 13 is the parent-children CSSN protocol applied to 3 AI engineers (parents) × 3 AI agents (children) collaborating on shared work, with one Python process per engineer over line-delimited JSON. Project at `ch13/ai-engineers-collab/`.

## How a sub-project plan is run
Open `chNN_plan.md` in a new Claude Code session, then read `chNN-sources.md` once to load the numbered source refs. Execute the plan's **Shared** block once, then each per-file/per-use-case action block. Test in REPL (and Flutter for chs 7–13). Commit per chapter. Plans are deliberately ≤100 words: action lines only, no narrative; numbered source refs `[sN]` quoted only when vital.
