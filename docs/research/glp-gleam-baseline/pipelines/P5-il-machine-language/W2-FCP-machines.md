I have strong primary-source grounding. Writing the report.

---

# W2 — Concurrent-Logic Abstract Machines (GLP's actual lineage)

## The FCP Sequential Abstract Machine (Houri & Shapiro, 1986)

The canonical machine language for a *flat* committed-choice logic language already exists and is documented. Houri & Shapiro, *"A Sequential Abstract Machine for Flat Concurrent Prolog"* — Weizmann CS86-20 (1986), published in **J. Logic Programming 7(2):85–123 (1989)** — is the artifact glpnet's v2.16.3 ISA descends from; it "made commercial deployment of FCP feasible" ([efcp refs / search](http://www.nongnu.org/efcp/references), [Springer chapter "Design of an Abstract FCP Machine"](https://link.springer.com/chapter/10.1007/978-3-322-97611-6_3)).

The cleanest open description of its structure is the **Houri–Shapiro patent US 5,222,221A** ([Google Patents](https://patents.google.com/patent/US5222221A/en)). Its instruction set has exactly **four categories**:

- **clause (indexing) instructions** — clause selection;
- **argument instructions** — head unification + data allocation;
- **guard instructions** — build guard args + execute tests;
- **process instructions** — outcome handling / body spawn.

This maps almost 1:1 onto GLP's three-phase execution: indexing + argument = **HEAD**, guard = **GUARD**, process = **BODY**. The `commit` instruction is "planted at the end of the head-plus-guard instructions and commits the current process to the current clause" ([search](https://www.semanticscholar.org/paper/...)).

**Suspension/reactivation is represented directly as machine state + instructions** ([US 5,222,221A](https://patents.google.com/patent/US5222221A/en)):
- on clause failure due to an unbound variable, "the address of the variable is added to the **suspension table**";
- a **`suspend` instruction** is planted after the final clause try — if reached, no clause succeeded, so the process suspends and returns control to the scheduler;
- each suspended process links to its variables via **suspension records** ("a list-cell whose car is a reference to the process hanger");
- **wake-up:** "when a variable is instantiated during a clause try, its suspension list is appended to the **activation queue**."
- **read-only variables:** the machine has a "read-only reference" datatype — "a read-only occurrence of a variable cannot be used to instantiate that variable." This is FCP's single-cell ancestor of GLP's reader.

A breadth-first **process queue** of "unreduced processes" with time-slice bounding is the scheduler.

## CARMEL / CARMEL-2 / CARMEL-4 — how *small* it gets

The hardware lineage proves the ISA compresses to a RISC core. **CARMEL-2** (Harsat & Ginosar, FGCS 1988) is a VLSI uniprocessor for FCP with **only 29 instructions, 10 of them FCP-special**, hitting 2,400 KLIPS on `append` ([New Generation Computing BF03037206](https://link.springer.com/article/10.1007/BF03037206); [CARMEL-1](https://link.springer.com/chapter/10.1007/978-1-4613-1619-0_3)). **CARMEL-4** is described as "the **unify-spawn machine** for FCP" — a Unification Unit + Spawn Unit ([Technion CRIS](https://cris.technion.ac.il/en/publications/carmel-4-the-unify-spawn-machine-for-fcp/)). Compilation was made fast via **Kliger & Shapiro's decision-tree → decision-graph** clause-indexing compiler (ICLP/SLP 1988; NACLP 1990) ([dblp Shapiro](https://dblp.uni-trier.de/pers/hd/s/Shapiro:Ehud)).

## KL1 / KLIC, Strand, PCN — the seam choices of the cousins

- **KL1 = flat GHC** (Ueda) under ICOT/FGCS ([KL1 Wikipedia](https://en.wikipedia.org/wiki/KL1)). **Kimura & Chikayama, "An abstract KL1 machine and its Instruction Set"** (ICOT TR-246, 1987) defines **KL1-B**, whose instruction set is "**unification, goal-manipulation, and suspension instructions**" — same three pillars as FCP. **KLIC** then compiles KL1 → **C** (not native), for portability, with "generic objects" for extensibility ([ICOT abst 078](https://www.airc.aist.go.jp/aitec-icot/ICOT/Museum/IFS/abst/078.html), [Springer 3-540-58402-1_4](https://link.springer.com/chapter/10.1007/3-540-58402-1_4)).
- **Strand** (Foster & Taylor): Prolog-like committed-choice surface, compiled for parallel runtimes ([Wikipedia](https://en.wikipedia.org/wiki/Strand_(programming_language))).
- **PCN** (outgrowth of UNITY + Strand): "the core notation is compiled into a **simple concurrent abstract machine** implemented portably via a **run-time library**" ([Wikipedia](https://en.wikipedia.org/wiki/Program_Composition_Notation)).

Pattern across all of them: source → (decision-graph compile) → **a small committed-choice abstract-machine ISA** → portable runtime (emulator, or C). None insert a separate optimizer IL between front-end and machine ISA; the **machine ISA itself is the seam.**

## Logix — module(persistent) vs computation(ephemeral)

**Logix** (Silverman, Hirsch, Houri & Shapiro, *Logix User Manual v1.21*) is the FCP environment, organized around three object kinds ([efcp](https://www.nongnu.org/efcp/), search): **computation** = "the unit of execution, control and debugging" (ephemeral); **module** = "the unit of compilation" (persistent code); **service** = access to system capabilities. This is precisely the front/back split the owner wants: compile-time persistent unit (module) vs run-time ephemeral unit (computation).

## Conclusion — is the FCP machine directly targetable for GLP?

**Yes in shape, no as a drop-in.** The FCP abstract machine is a *real, small, committed-choice ISA* whose four-category instruction set, `suspend`/activation-queue suspension model, and commit semantics already match GLP's HEAD/GUARD/BODY + suspend/reactivate. It validates the owner's instinct that **a machine language can be the seam with no separate IL** — and glpnet's **v2.16.3 ISA is literally the GLP-faithful descendant of it** (it is *more* completely specified, on-disk, than any open FCP-machine artifact).

**What is missing / under-defined for our use:**
1. **Variable model gap.** FCP defines only a single-cell **read-only reference**; GLP's faithfulness (M1) rests on the explicit **two-cell writer/reader** model and **writer-MGU** (binds writers only, never readers, never writer↔writer) from `GLP_IMPLEMENTATION.pdf`. The FCP machine does *not* encode writer-MGU — adopting FCP semantics verbatim would break M1.
2. **No open, complete ISA spec.** The FCP-machine detail is scattered across a 1986 TR, the 1989 JLP paper, a patent, and a paywalled chapter — none give a reusable normative bytecode. glpnet's v2.16.3 doc is the better normative base.
3. **No multiagent/dGLP layer** — FCP has no equivalent of GLP's grassroots/multiagent semantics.
4. **CARMEL's 29 instructions are hardware-tuned**, not a portable software bytecode — useful as a *minimality target*, not a spec.

**Implication for the seam:** target the **existing v2.16.3 machine language** (the FCP-machine's GLP-correct heir) as the seam; the FCP/CARMEL literature is the proof-of-concept that this seam is small, suspension-complete, and directly targetable — and Logix's module/computation split is the documented precedent for the persistent/ephemeral front/back boundary.