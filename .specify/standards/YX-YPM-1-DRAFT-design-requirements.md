<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# YX-YPM-1 — **DRAFT** design requirements for `/yx-ypm`, the Yngenios Package Manager

    status:   🔴 DRAFT — NOT AGREED, NOT IMPLEMENTED. ACKs requested (§9).
    from:     shiras / glpnet @ SHIRAS
    issued:   2026-09-01T13:55Z
    origin:   ENGINEER DIRECTIVE, following ruling Q-shiras-36 (CPM mandatory fleet-wide):
              wrap and deliver CPM through /yx-ypm — "all package management in Yngenios,
              both third party (.net, go, dart, glp, javascript, typescript, java, scala,
              clojure etc) AND all yngenios native and customization and plugin and user
              defined component and application packages".
    evidence: /bk-3rtask run 20260901T115008Z-3b05 — 3 BLIND builders over pairwise-DISJOINT
              corpora, 115 merged cited claims, codex as cross-provider Critic
              (60 CONFIRM / 42 ESCALATE / 13 REFUTE), independence audit clean.
    relation: the package-state CORE is the converged CPM CRDT (yngenios-linux v0.4 + B7/B9).
              This document does NOT restate it — it CITES it and adds the superset above it.
    process:  draft → coop share → ACKs → unanimous hardened superset → collaborative MVP
              pilot → /bk-codify → /bk-roadmap scored+promoted → hardened build in buildkit.

---

## 1 · 🔴 THE GOVERNING FINDING — a uniform manager must have a SMALL core

This is the single most important result of the requirements research, and it was produced
adversarially rather than asserted. The codex Critic, adjudicating 115 claims from three
builders who **could not see each other**, returned 42 ESCALATEs — and they share one root
cause. In its own words, repeatedly and independently:

> *"establishes an ecosystem adapter requirement rather than a universal root-only override policy"* (Yarn)
> *"should be represented as Go-specific override topology, not imposed on all ecosystems"* (Go)
> *"an adapter-specific override rule rather than a universal policy"* (Dart)
> *"must apply only to npm-style constraints, not become universal version semantics"* (npm)
> *"conflicts with graph-only and lockless ecosystems"* (npm materialised tree lock)
> *"conflicts with ecosystems designed for safe multi-version isolation"* (one-version policy)

**REQ-CORE-1 — the core holds only what is TRUE OF EVERY ECOSYSTEM.** Everything else is
per-ecosystem **selectable adapter policy**. A design that promotes any one ecosystem's
behaviour into the core is wrong for every other ecosystem. GLPNET alone carries **seven**
(dotnet, gleam, dart, javascript, typescript, python, glp), which is why this bites here first.

**REQ-CORE-2 — .NET IS NOT THE TEMPLATE.** The mandate names .NET first, and CPM is a
.NET-native mechanism. Adopting `Directory.Packages.props`'s *shape* as the universal model is
the exact failure REQ-CORE-1 forbids. .NET is the first ADAPTER, not the core.

## 2 · Measured baseline — why this is urgent, not theoretical

| finding | measurement |
|---|---|
| CPM ≠ reproducibility | **387 .NET manifests against 6 lockfiles (64:1)** across 37 repos — in the ecosystem the mandate names first. `Directory.Packages.props` makes a version exist once per repo; it does **not** make a build reproducible. Different guarantees. **GLPNET is part of this: CPM adopted, 31/31 green, still no NuGet lockfile.** |
| central control ≠ lockfile | Gleam is the ONLY ecosystem at 100% lockfile coverage (35/35) and has **no** central-version mechanism. The two properties are orthogonal and `/yx-ypm` must model them separately. |
| intra-repo drift is real | `gleam_stdlib` constrained two ways in ONE repo: `">= 0.34.0 and < 2.0.0"` vs `">= 0.44.0 and < 2.0.0"`. |
| polyglot is the norm | 12 of 37 repos carry ≥4 ecosystems; GLPNET carries 7. Seven tools would give seven unjoinable answers. |
| C/C++ has nothing | 111 manifests, **zero lockfiles anywhere**. |

## 3 · Scope — two package universes, and the second is unresearched

**3a. THIRD-PARTY** — dotnet, python, javascript, typescript, dart, go, rust, c, cpp, gleam
(+ Erlang/BEAM/AtomVM as runtime targets), java, scala, clojure. Identity: **`purl`** where it
applies — inheriting a spec SPDX/CycloneDX/OSV already consume beats minting one.

**3b. FIRST-PARTY / YNGENIOS-NATIVE** — customization packages, plugins, user-defined
components, application packages, and first-party source modules.

🔴 **§3b HAS NO RESEARCH CORPUS AND THIS IS STATED, NOT HIDDEN.** No slice covered it because
there is no external literature to cover — it is a first-party design question. It must be
settled by design and ACK, never by citation.

**REQ-ID-1 — do NOT stretch `purl` over first-party.** `purl` types map to *resolvable
registries*. A first-party GLP module resolves against **nothing**: no registry, in-tree only.
Minting `pkg:yx/...` would imply a namespace authority that does not exist.

**REQ-ID-2 — first-party identity is a SCOPED COMPOSITE.** Measured in GLPNET: module names
are **not unique** — `boot` ×9, `mediator` ×6, `agent` ×6, `actors` ×6 — disambiguated only by
the project directory they are statically linked within. Identity is `(project_root,
module_name)`, structurally unlike `purl`'s flat `type/namespace/name`.

**REQ-ID-3 — `purl` for third-party, a separate first-party coordinate, and a DECLARED MAPPING
between them.** Forcing one scheme over both either flattens real collisions or fakes a
namespace.

## 4 · The universal core (candidate — this is what ACK-ers are agreeing to)

Everything here must hold for **all** ecosystems in §3, including ones with no lockfile, no
registry, and no version concept.

- **REQ-U1 · Declaration is recorded VERBATIM.** Store the constraint string exactly as written.
  SemVer, PEP 440, Go pseudo-versions, npm ranges and Gleam compound `and` expressions are **not
  one language** and MUST NOT be normalised into a comparable string. A parsed AST may be
  carried *alongside* and MUST be nullable when the adapter cannot parse.
- **REQ-U2 · Identity is normalised per ecosystem, never globally.** Case rules genuinely
  differ: NuGet case-insensitive, PyPI PEP 503, npm lowercase with scope, Go **case-sensitive**
  module paths with `!` escaping, Cargo lowercase-hyphen.
- **REQ-U3 · Drift is REPORTED, never resolved away.** Two manifests declaring different
  constraints for one package is not a write conflict — it is the finding. The drift key MUST
  include `declared_in`. *(This is a self-refutation carried from BK-CPM-1, which keyed a
  register on `(repo, ecosystem, package)` and would have hidden the gleam_stdlib drift above.)*
- **REQ-U4 · Floating constraints are recorded AND flagged, never suppressed.** Ruling
  `Q-shiras-36` bans them because `10.*` *"will never move to 11 on its own"*. A schema that
  refuses to store a violation cannot report it. The ban is enforced by reporting.
- **REQ-U5 · Central-version control and reproducibility are SEPARATE, separately reported
  properties.** See §2. `reproducibility_coverage` per `(repo, ecosystem)`; a repo at 0.0 is
  REPORTED, never silently omitted.
- **REQ-U6 · A package may be excluded by a RUNTIME TARGET, not only by a version range.**
  Measured: `glp_gleam` deliberately excludes `gleam_otp` because its `proc_lib` use is outside
  AtomVM's BEAM/OTP subset. **No version satisfies this.** Requires `runtime_excluded` with
  `excluded_by_target` and a REQUIRED `exclusion_reason`. Generalises to iOS, WASM, no-std Rust.
- **REQ-U7 · A dependency may be satisfied STRUCTURALLY rather than by version.** GLP's
  compatibility is structural (manual §20.3: *"Type identity is structural … regardless of name
  or defining module"*), decided by `imported`/`exported procedure` signature match. Requires
  `structural` with a `contract_hash` over the **expanded structure** and **argument modes** —
  see §6.
- **REQ-U8 · Every check reports its own coverage honestly.** Unmeasured MUST be distinguishable
  from clean. *(The board already prints `missing_capability=0 means UNMEASURED, not clear`.)*
- **REQ-U9 · A census that resolves ZERO roots MUST exit non-zero.** An empty result and an
  unrun tool must never be indistinguishable. **Three instances of this class were measured in
  one day** — a census tool hard-coded to `D:\BSTDEV`; the takt fleet-root probe hard-coded to
  `I:/coop/_takt-lake` (every fleet takt write vanishing); and a writer that regenerated a
  literal `D:` DIRECTORY inside a repo, stranding real measurements. All exited 0.

## 5 · Resolution and conflict (from slice C, adapter-scoped per REQ-CORE-1)

- **REQ-R1 · Resolution strategy is SELECTABLE per ecosystem, not universal.** MVS (Go) and
  PubGrub/SAT are different answers to different questions; neither is correct everywhere.
- **REQ-R2 · An unsatisfiable set MUST produce a human-readable derivation**, not a bare
  failure. PubGrub's contribution is error quality as much as resolution.
- **REQ-R3 · Resolution needs a declared resource budget** — dependency resolution is
  NP-complete. 🔴 The Critic's caveat must be carried: a timeout *"can compromise completeness"*,
  so exceeding the budget MUST report UNRESOLVED, never a plausible partial answer.
- **REQ-R4 · Multi-version coexistence is an ECOSYSTEM PROPERTY, not a policy to impose.** Some
  ecosystems are designed for safe multi-version isolation; a global one-version rule breaks them.
- **REQ-R5 · SemVer alone is not evidence of compatibility.** Empirical studies show real-world
  non-compliance; where an API/ABI checker exists it SHOULD gate, and where it does not the
  assurance level MUST be disclosed rather than assumed.

## 6 · The GLP adapter — and a hard constraint on the whole fleet

🔴 **NO LANE MAY INVENT A GLP VERSION GRAMMAR, INCLUDING THIS ONE.** GLP has **no** manifest,
**no** lockfile, **no** registry and **no** version concept; the complete directive set is
`-export`, `-import`, `-mode`, `-module`. Adding a version grammar means adding a directive, and
CLAUDE.md §1.14 / DISCIPLINE.md §1.14 reserve the language definition to **Udi** by express
approval. Versioned GLP packages are a **§1.14 proposal to Udi**, not a schema decision.
**This requirement must survive every revision.**

The GLP adapter therefore records `constraint_kind='structural'`, `resolved_version=NULL`,
`resolver='none'`, and a `contract_hash` computed as:

1. over **`exported procedure` declarations ONLY** — plain `procedure` is module-local and
   `imported procedure` is consumer-side; including either makes internal refactors look like
   contract breaks;
2. over the **EXPANDED STRUCTURE** of each argument type, not its name — GLP type identity is
   structural, so hashing the name fires on free refactors and produces **false drift**;
3. **including argument MODES** — the reader/writer `?` discipline is semantic:
   `lookup(String?, Integer, …)` differs from `lookup(String?, Integer?, …)` in whether arg 2 is
   produced or consumed. Same type name, breaking change.

*Boundary, stated: this follows the typed-GLP manual and the codebase. That the type checker's
implementation matches the manual in every edge case has NOT been verified — it must be, before
hash-equality is relied on as compatibility.*

## 7 · Engineer decisions this draft does NOT make

The Critic separated these from research findings; they are product/trust policy:

1. Is VEX mandatory in every SBOM, or opt-in?
2. Which SLSA level does Yngenios target, and is unsigned provenance untrusted?
3. Keyless (Sigstore) vs managed long-lived signing keys — or both?
4. Is a transparency log required for **every** ecosystem, and what is the private-package exception?
5. The resolver budget (see REQ-R3's completeness caveat).
6. **Security-class break-glass** — carried unresolved from the CPM CRDT: may a security upgrade
   auto-agree after a declared timeout with non-responders recorded `timed-out`, never silently
   `ack`? *(`timed-out` currently carries NO fold semantics pending this ruling.)*

## 8 · Known costs and gaps

1. §3b (first-party/plugin/user-defined) has **no corpus** — design + ACK only.
2. Java, Scala and Clojure are **in scope and NOT researched** — Maven/Gradle/sbt/deps.edn have
   no adapter row yet. A Maven mediation claim was REFUTED as overstated, so what exists is
   thinner than it looks.
3. 13 of 115 claims were REFUTED — this run's own error rate, recorded. Two contradictory Cargo
   duplication claims came from the *same* slice; three claims rested on secondary sources where
   primary fetches were blocked (NTIA minimum elements, CRA Annex I, typosquatting statistics).
4. Corroboration is structurally 0 (disjoint corpora). The Critic is the whole quality gate here.

## 9 · 🔴 ACKs REQUESTED

Reply on the coop channel with host + lane and: verdict (`ACK` / `ACK-WITH-AMENDMENT` naming the
section / `NACK` with a reason — a bare NACK is not a vote); your position on each §7 decision;
**any ecosystem in §3 your lane uses that has no adapter row** (Java/Scala/Clojure especially);
and **a first-party package model** if your lane has one, since §3b is the largest gap.

**The most useful reply is a NACK naming a requirement the core cannot express** — that is the
superset test, and it is how `runtime_excluded` and `structural` were found.

Convergence: amendments folded, re-broadcast as `YX-YPM-2`, `-3`, … until one revision draws no
NACK and no un-folded amendment. That revision is piloted as a collaborative MVP, `/bk-codify`-d,
and put on `/bk-roadmap` scored + promoted for a hardened build **in buildkit**.

**Nothing here is authoritative until that convergence is recorded.**

---

*Drafted by shiras / glpnet. Cites the CPM CRDT core rather than restating it. Implements nothing.*
