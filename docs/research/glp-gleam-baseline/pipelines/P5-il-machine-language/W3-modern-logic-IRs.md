# W3 — Modern Logic IRs & Multi-Target Backends

Every claim below is grounded in a primary source (URL inline). I report findings; the IL-vs-direct fork is presented as options-with-consequences, not self-decided.

## Mercury: a logic language that deliberately stacks IRs for multi-target reach

Mercury's compiler passes through three named IRs in sequence: **HLDS** ("the compiler's main internal representation," kept close to source semantics), then either **MLDS** ("imperative code at a level that corresponds to handwritten C or Java code... assignment, if-then-else, switch and while") or **LLDS** ("only slightly above assembly... virtual machine registers... labels and jumps"), then target code ([compiler_design.html](https://www.mercurylang.org/development/developers/compiler_design.html)). What the layering buys is explicit and decisive for this question:

1. **Analysis happens high.** Mode analysis, determinism checking and type-specific transforms run on HLDS *before* any backend is chosen; the team states "we prefer to do things as HLDS to HLDS transformations where possible, since this is much easier to debug initially and to modify later" ([compiler_design.html](https://www.mercurylang.org/development/developers/compiler_design.html)).
2. **One mid-IR fans out to many targets.** MLDS abstracts imperative structure once and emits **C, Java, and C#**; LLDS is the low-level-C/register path ([compiler_design.html](https://www.mercurylang.org/development/developers/compiler_design.html)).
3. **The BEAM cautionary note.** Mercury *had* an Erlang/BEAM backend and **deleted it**: "We have removed the Erlang backend as it was unmaintained" ([release-notes-22.01](https://dl.mercurylang.org/release/release-notes-22.01.html)). The lesson for a Gleam/AtomVM target is that a BEAM backend is feasible but is a *maintained surface*, not a free lunch.

## BinProlog / BinWAM: the machine language *is* the seam (no IL above it)

The opposite philosophy. BinProlog compiles full Prolog to **binary clauses** via a continuation-passing-style **binarization** transformation, then runs them on the **BinWAM**, "a specialization of the WAM for the efficient execution of binary logic programs" that **drops WAM environments / the AND-stack** for a "minimalistic... RISC"-style instruction set (Tarau's "WAM-RISC") ([arxiv 1102.1178, §2–3.5](https://arxiv.org/pdf/1102.1178); [Neumerkel, *The binary WAM*](https://www.complang.tuwien.ac.at/ulrich/papers/PDF/binwam-nov93.pdf)). Payoff is *simplicity and size*: "a very small emulator (about 60K...) that often fits completely in the cache" ([emse BinProlog node3](https://www.emse.fr/~beaune/BinProlog5.75/node3.html)). Here there is **no IL above the abstract machine** — a source→machine-language transform plus a tiny ISA is the entire pipeline. Crucially the *value* was extracted by a **source-to-source transform (binarization) that still produces the machine language** — directly analogous to the owner's Q3 hypothesis (an IL that *helps generate* the machine code, even if only the machine code crosses).

**Portable bytecode as a wire format** confirms the machine language can itself be the seam: SWI-Prolog's `.qlf` files and saved states are "dumps of VM instructions" of the ZIP VM, "machine-independent intermediate code in a format dedicated for fast loading" — but the SWI roadmap warns they are "Sensitive to VM instruction numbering" and "break... if the VM is being extended" ([SWI qlf manual](https://www.swi-prolog.org/pldoc/man?section=qlf); [SWI roadmap: machine-independent QLF](https://github.com/SWI-Prolog/roadmap/wiki/Machine-independent-QLF-files-and-states)). That is the exact in-process-AND-over-the-wire seam the owner's Q4 wants — with the caveat that an unstabilised ISA is a fragile contract.

## MLIR as a logic/relational IL that lowers to LLVM

LingoDB makes the strongest modern case for an IL *above* native code in a declarative setting. It introduces stacked MLIR dialects — **relalg** (relational algebra), **db**, **dsa**, **util** — and does **progressive lowering** from relational algebra → sub-operators → imperative ops → **LLVM IR → machine code** ([VLDB p2389-jungmair](https://www.vldb.org/pvldb/vol15/p2389-jungmair.pdf); [MLIR users](https://mlir.llvm.org/users/)). The stated wins are precisely the owner's analyzability/optimization axes: "moving query optimization into the query compiler to benefit from the existing optimization infrastructure and make cross-domain optimization viable," via "open intermediate representations that can be combined at each layer" ([VLDB p2389-jungmair](https://www.vldb.org/pvldb/vol15/p2389-jungmair.pdf)). This is the external validation for the repo's own prototyped GLP/FCP MLIR dialect.

## Conclusion — when an IL above the machine language pays off (options for the owner)

The literature splits cleanly along *how many targets and how much analysis* you need:

- **Option A — compile straight to the machine language (BinWAM / `.qlf` model).** Best when there is **one backend** and the abstract machine is already the contract. Buys minimal surface, smallest emulator, machine-language-as-wire-format. **Consequence:** no shared optimization/analysis layer; the ISA must be *frozen and versioned* or the seam is fragile ([SWI roadmap](https://github.com/SWI-Prolog/roadmap/wiki/Machine-independent-QLF-files-and-states)). Maps to GLP **Q2**: glpnet's v2.16.3 ISA can *be* the seam.

- **Option B — an IL *above* the machine language (Mercury MLDS / LingoDB MLIR model).** Pays off when you have **multiple backends** (Gleam/BEAM **and** C++/LLVM, the very pair Mercury's MLDS→{C,Java,C#} and LingoDB→LLVM demonstrate), want **optimization/analysis reuse** across targets, and want **verification/analyzability** (MLIR's combinable open IRs). **Consequence:** a second artifact to build and maintain — and Mercury's deleted Erlang backend shows even a working BEAM path rots if unmaintained ([release-notes-22.01](https://dl.mercurylang.org/release/release-notes-22.01.html)).

- **Option C (the owner's Q3 hypothesis, and what BinProlog actually did).** Use the logic-centric IL **inside the front-end as a generation/optimization aid**, but still emit the existing machine language as the *only* thing that crosses the seam. BinProlog's binarization is exactly this — a source-level transform that *produces* the ISA — and it is a documented, shipped design, not speculation ([arxiv 1102.1178](https://arxiv.org/pdf/1102.1178)).

**Decision pivot for the owner:** the choice is governed almost entirely by *backend count and analysis ambition* — single BEAM target with a stable ISA favors A/C; a maintained BEAM-*and*-LLVM/C++ future with cross-target optimization favors B.

Sources:
- [Mercury compiler design](https://www.mercurylang.org/development/developers/compiler_design.html)
- [Mercury 22.01 release notes (Erlang backend removed)](https://dl.mercurylang.org/release/release-notes-22.01.html)
- [BinProlog Experience (arXiv 1102.1178)](https://arxiv.org/pdf/1102.1178)
- [Neumerkel, The binary WAM](https://www.complang.tuwien.ac.at/ulrich/papers/PDF/binwam-nov93.pdf)
- [BinProlog node3 (size/cache)](https://www.emse.fr/~beaune/BinProlog5.75/node3.html)
- [SWI-Prolog QLF manual](https://www.swi-prolog.org/pldoc/man?section=qlf)
- [SWI roadmap: machine-independent QLF/states](https://github.com/SWI-Prolog/roadmap/wiki/Machine-independent-QLF-files-and-states)
- [LingoDB VLDB paper (p2389-jungmair)](https://www.vldb.org/pvldb/vol15/p2389-jungmair.pdf)
- [MLIR users list](https://mlir.llvm.org/users/)