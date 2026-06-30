# W1 — WAM & Classical Logic Abstract Machines: Transfer to a GLP Committed-Choice Machine Language

## 1. The WAM as the standard parser→backend seam

Ait-Kaci's *Warren's Abstract Machine: A Tutorial Reconstruction* (MIT Press 1991; free PDF [cliplab.org/~logalg/slides/8_wam_AitKaci_book.pdf](https://cliplab.org/~logalg/slides/8_wam_AitKaci_book.pdf); LaTeX source [github.com/a-yiorgos/wambook](https://github.com/a-yiorgos/wambook)) reconstructs the WAM "through several intermediate abstract machine designs" — i.e. the machine itself is built up in layers, not fronted by a separate IL.

**Instruction categories** (Ait-Kaci's taxonomy; instruction names visible in the Wikipedia worked example [en.wikipedia.org/wiki/Warren_Abstract_Machine](https://en.wikipedia.org/wiki/Warren_Abstract_Machine)):
- **put** — build/load argument registers for a *call* (caller side): `put_structure`, `put_variable`, `put_value`.
- **get** — match/decompose a callee's head arguments: `get_atom`, `get_variable`, `get_structure`.
- **unify** (structure-arg, read/write mode): `unify_variable`, `unify_value`, `unify_local_value`.
- **control / procedural**: `call`, `execute`, `proceed`, `allocate`/`deallocate` (environment frames).
- **indexing & choice**: `switch_on_term`, `switch_on_constant`, `switch_on_structure`; `try_me_else`, `retry_me_else`, `trust_me`.

**Memory model** (Wikipedia, citing Warren): three regions — a **heap/global stack** ("to store compound terms"), a **local stack** ("for environment frames and choice-points"), and a **trail** ("to record which variable bindings ought to be undone on backtracking"), plus a PDL (push-down list) for unification and the registers (P, CP, E, B, H, TR, S). Dereferencing chases bound variable cells to a representative; binding writes a cell and **pushes the address on the trail** so it can be reset.

**Why it is the de-facto seam**: Wikipedia states "Prolog code is reasonably easy to translate to WAM instructions, which can be more efficiently interpreted," and "code improvements and compilations to native code are often easier to perform on the more low-level representation," making WAM "the de facto standard target for Prolog compilers." The machine language *is* the analyzable, retargetable interface between front-end and back-end.

## 2. Committed choice simplifies the machine

Concurrent logic languages are committed-choice: "an action taken by a process … cannot be undone and backtracking is not permitted. Once a process has reduced itself using some clause, it is committed to it" ([en.wikipedia.org/wiki/Concurrent_logic_programming](https://en.wikipedia.org/wiki/Concurrent_logic_programming)). Consequences for the abstract machine:

- **No choice-point stack, no deep trail / `try_me_else`/`retry`/`trust`** of the Prolog WAM — there is nothing to backtrack into.
- Execution is **reduction** (try head+guard → commit → spawn body), not search. Technion's CARMEL-4 is literally named "the **unify-spawn** machine for FCP" ([cris.technion.ac.il/…/carmel-4-the-unify-spawn-machine-for-fcp](https://cris.technion.ac.il/en/publications/carmel-4-the-unify-spawn-machine-for-fcp/)) — the whole instruction repertoire reduces to *unify the head* and *spawn the body*.
- The retained complexity is **suspension**: an unbound reader during the try suspends the goal until a writer binds it (data-driven scheduling replaces backtracking). Tentative head/guard bindings may be undone on *clause-try failure or suspension*, but there is no cross-goal undo.
- The net effect is a smaller ISA. CARMEL-2, a RISC VLSI tuned for FCP, has "only **29 carefully selected instructions**" with "10 special instructions" and "intelligent dereference" ([New Generation Computing, BF03037206](https://link.springer.com/article/10.1007/BF03037206)) — versus the full Prolog WAM. FCP's Logix compiles "FCP programs into an FCP abstract machine instruction set" emulated in C ([nongnu.org/efcp](https://www.nongnu.org/efcp/)); Houri & Shapiro's sequential abstract machine for FCP (Weizmann **CS86-20**, 1986) is the documented origin.

## 3. Direct AST→machine-language, or insert an IL?

Real logic systems compile **source → AST → abstract-machine language directly**; any further layering sits *below* the machine language, not between AST and it:
- **GNU Prolog**: "compiles a Prolog program to a WAM file which is then translated to a low-level machine-independent language called mini-assembly" → native ([progopedia.com/implementation/gnu-prolog](http://progopedia.com/implementation/gnu-prolog/); Diaz, *JFLP* 2001 [scholar.lib.vt.edu/…/JFLP-A01-06.pdf](https://scholar.lib.vt.edu/ejournals/JFLP/jflp-mirror/articles/2001/S01-02/JFLP-A01-06.pdf)). The IL (mini-assembly) is *under* WAM.
- **SWI-Prolog** compiles directly to a ZIP-based bytecode VM ([arxiv.org/pdf/1011.5332](https://arxiv.org/pdf/1011.5332)) — no intermediate above the VM.
- **KLIC** (committed-choice KL1) compiles **KL1 → C** directly, with guard-simplification passes, then the host C compiler ([en.wikipedia.org/wiki/KL1](https://en.wikipedia.org/wiki/KL1)).

So historically the **abstract-machine language is the seam**; a distinct IL is the exception, used only where retargeting to native/native-via-C demands a lower layer, or for analysis/optimization passes that are easier on a normalized form.

## 4. What transfers to a GLP committed-choice machine language

- The **WAM organizing principle** transfers wholesale: a heap for terms, registers for the calling convention, and put/get/unify-shaped instructions are exactly what glpnet's v2.16.3 ISA already embodies — confirming the machine language can serve as the front/back seam **with no separate IL** (Q2-supporting; the FCP/CARMEL/KL1 lineage all do this).
- **Drop the Prolog backtracking machinery** (choice-point stack, `try/retry/trust`, the *deep* trail). GLP's three-phase HEAD/GUARD/BODY with committed choice needs only the *tentative* binding/undo for a failed-or-suspended try, plus **suspension records + reactivation** — matching FCP's unify-spawn reduction and CARMEL's small ISA.
- **Intelligent dereference + the two-cell writer/reader model**: CARMEL's "intelligent dereference" is the hardware analogue of GLP's writer-MGU (bind writers only) and σ̂w discipline — dereference must respect reader/writer roles, not classic mutable variables.
- **A small, specialized ISA is the norm, not a compromise** (29 instructions for FCP) — so an IL is justified only if it buys *analyzability/multi-target reach above* the machine language, which is the open question for W2/MLIR agents, not a classical-baseline requirement.

Sources:
- [Ait-Kaci, WAM Tutorial Reconstruction (PDF)](https://cliplab.org/~logalg/slides/8_wam_AitKaci_book.pdf) · [wambook source](https://github.com/a-yiorgos/wambook)
- [Wikipedia: Warren Abstract Machine](https://en.wikipedia.org/wiki/Warren_Abstract_Machine)
- [Wikipedia: Concurrent logic programming](https://en.wikipedia.org/wiki/Concurrent_logic_programming)
- [Emulated FCP / Logix (nongnu.org/efcp)](https://www.nongnu.org/efcp/)
- [CARMEL-2 (New Generation Computing, BF03037206)](https://link.springer.com/article/10.1007/BF03037206) · [CARMEL-4 unify-spawn machine](https://cris.technion.ac.il/en/publications/carmel-4-the-unify-spawn-machine-for-fcp/)
- [Wikipedia: KL1](https://en.wikipedia.org/wiki/KL1)
- [GNU Prolog (Progopedia)](http://progopedia.com/implementation/gnu-prolog/) · [Diaz, GNU Prolog (JFLP 2001)](https://scholar.lib.vt.edu/ejournals/JFLP/jflp-mirror/articles/2001/S01-02/JFLP-A01-06.pdf) · [On the Implementation of GNU Prolog (arXiv)](https://arxiv.org/pdf/1012.2496)
- [SWI-Prolog / ZIP VM (arXiv 1011.5332)](https://arxiv.org/pdf/1011.5332)

*Note: CARMEL-2 internals (BF03037206) are paywalled; its 29-instruction/"intelligent dereference" figures are from the publisher abstract, not the full text. The committed-choice "no choice points/no backtracking" claim is grounded in the Wikipedia concurrent-logic-programming source and the verified on-disk GLP three-phase/writer-MGU facts.*