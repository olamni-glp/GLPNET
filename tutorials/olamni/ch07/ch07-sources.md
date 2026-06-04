# Ch 7 Sources — Module System

**PDF**: `GLP_ART.pdf`, book pp 55–62 (PDF pp 67–74).

## Sections (verified)
- 7.1 Design Principles — p 55 (4 principles: hierarchy mirrors file system; implicit ancestor scoping; self-contained type checking; structural type compatibility)
- 7.2 Module Structure — p 56 (typical project tree example)
- 7.3 Procedure Declarations — p 56 (Private / Exported / Imported syntax + Social Agent example)
- 7.4 Cross-Module Type Checking — p 58 (mostly formal)
- 7.5 Project Compilation — p 58 (discovery, type checking, procedure renaming, call resolution, entry points)
- 7.6 Dynamic Linking — p 60 (load-time verification, type automata as runtime artifacts) — no code
- 7.7 Validation: Child-Safe Social Graph — p 61 (CSSG project tree, narrative)
- Exercises — p 61 (OUT OF SCOPE)

## Code-block index
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 7.2.1 | Project tree (file-system illustration) | p 56 | tree text (`social/self.glp`, `agent.glp`, `ui/{self.glp, mediator.glp, actors.glp}`, `boot.glp`) | module layout |
| 7.3.1 | Procedure-decl kinds table | p 56 | `procedure …`, `exported procedure …`, `imported procedure M#…` syntax | declaration kinds |
| 7.3.2 | Social Agent module — `-module(agent).` with exported `agent/4` | p 56–57 | module decl + `exported procedure agent(Constant?, UserInStream?, NetInStream?, OutputsList?).` + private `merge/3`, `lookup_send/4` decls + agent/4 clause `agent(Id, [msg('_user', Id1, connect(Target))|UserIn], NetIn, Outs) :- Id? =?= Id1?, ground(Target?) | lookup_send('_net', msg(Target?, intro(Id?, Resp)), Outs?, Outs1), …` | exported-module example |
| 7.3.3 | Boot module's call site — `imported procedure agent#agent(...)` + `agent # agent(alice, UserIn?, NetIn?, [output('_user', AgentToUser), output('_net', NetOut)])` | p 57 | imported decl + cross-module call | imported / cross-module call |
| 7.3.4 | Ancestor scope — `self.glp`: `Response ::= accept(FriendChannel) ; no.` and `AgentContent ::= befriend(Constant, Response?) ; connected(Constant) ; rejected.` | p 57 | 2 type defs in `self.glp` | shared types via ancestor scoping |
| 7.5.1 | Procedure renaming table | p 59 | mapping `agent.glp:merge/3 → agent:merge/3`, etc. | linker behaviour |
| 7.5.2 | Entry-point aliases | p 59 | `play1 :- boot:play1.` `play2 :- boot:play2.` | top-level alias generation |
| 7.7.1 | CSSG project tree | p 61 | `cssg_modules/{self.glp(40 types), agent.glp(exported agent/4 + 13 privates), ui/{mediator.glp(ui_mediator/5 + 3 privates), actors.glp(16 exported actors)}, boot.glp(7 plays, untyped)}` | full validation example |

## Formal boxes
- **Formal 7.1: Type Scope Assembly** — p 57–58 (algorithm building each module's type environment from ancestor `self.glp` files).
- **Formal 7.2: Cross-Module Well-Typing** — p 58 (rule: imported decl carries full signature, no source access needed).
- **Formal 7.3: Correctness of Project Compilation** — p 60 (renaming preserves well-typing).

## Tutorial mode
multi-actor-distillation begins here per charter. Ch 7 is the **transition chapter**: it is the first chapter where projects are *modular* (`{self.glp, agent.glp, ui/, boot.glp}` shape per charter §2.2). The tutorial outputs are project subdirectories paired with `glp_multiagent/lib/main_olamni_ch07_<use-case>.dart` per charter §2.2.

## Use cases (from §7.7 CSSG validation)
1. Cold-call befriending (between adults).
2. Friend-mediated introduction (accept/reject).
3. Parent-mediated child introduction (accept/reject by each party).

These are the same scenarios that Ch 8 develops as detailed protocol code; in Ch 7 they are referenced as the *validation set* for the module system.

## Companion repo references
- `programs/cssg_modules/{self.glp, agent.glp, ui/{mediator.glp, actors.glp}, boot.glp}` — exact match for §7.7 example.
- `programs/typed_book/cssg/` — typed CSSG variants.
- `glp_multiagent/lib/main_cssg_mad_modules.dart` — Flutter template.
- `../charter.md`
