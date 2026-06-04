# Feature Specification: Chapter 11 — Grassroots Cryptocurrencies

**Feature Branch**: `012-tutorial-ch11`
**Created**: 2026-04-28
**Status**: Draft (raw — produced by deep PDF scan)
**Input**: `olamni/tutorial/ch11/ch11-sources.md` + `GLP_ART.pdf` book pp 101–114 (PDF pp 113–126).
**Constitution**: `.specify/memory/constitution.md` — Principle VI; Principle I.
**Tutorial Mode**: multi-actor-distillation

## Clarifications
- Two related use cases per charter §1: the Grassroots Flash 3-agent payment+approval play (§11.4 + §11.5 Complete Example) and the Redemption play (§11.4 redeem handlers + §11.5 redemption example). Decision: each becomes its own project subdir.
- Cross-reference `programs/Bonds/` in the repo: bonds plays `fplay1`–`fplay12` extend the GC machinery — relevant for Flutter template alignment, but Bonds is NOT the chapter's source. Stay close to §11.4 / §11.5.

## Source Programs (verified against PDF)
See `ch11-sources.md` code-block index. Highlights:
- §11.4: `agent/3` event loop; `get_balance/3`, `set_balance/4`; `handle/6` clauses for `issue`, `accept`, `approve`, `mint`, `redeem`; `alice_mutual_credit/3`, `bob_mutual_credit/3`; redemption helpers `compute_repayments/5`, `take_from_currency/8`, `emit_repayments/5`.
- §11.5: `play_cryptocurrency/0`, `find_payment_to/3`, `has_payment_to/2`, `find_approval/2`, `play_redemption/0`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Grassroots Flash 3-agent payment with approval (Priority: P1)

Three agents (Alice/Bob/Carol). Alice and Bob establish mutual credit (each issues 100 personal coins to the other). Alice pays Carol 20 Bob-coins. Bob approves. Carol accepts.

**Independent Test**: load `olamni/tutorial/ch11/grassroots-flash/` project + `main_olamni_ch11_grassroots_flash.dart`. Run `play_cryptocurrency`. Expected end-state per §11.5: Alice holds 80 Bob-coins; Bob holds 100 Alice-coins; Carol holds 20 Bob-coins. Carol's Accept block points to BOTH Alice's Issue block and Bob's Approve block.

**Acceptance Scenarios**:
1. Project shape per charter §2.2.
2. `agent.glp` contains §11.4 `agent/3` + balance helpers + handlers for `issue`, `accept`, `approve`, `mint` verbatim with `%%` paraphrase comments.
3. `actors.glp` contains the 3-agent goal-stream wiring from §11.5 with `find_payment_to`/`find_approval` cross-refs.
4. `boot.glp` wires `play_cryptocurrency`.
5. REPL trace produces the 7-step §11.5 execution sequence; final balances match.

### User Story 2 — Coin redemption with currency preferences (Priority: P2)

Bob holds 50 Alice-coins and demands redemption with preferences `[carol, dave]`. Alice has 20 Carol-coins and 15 Dave-coins; she repays 20 Carol + 15 Dave + 15 Alice (own coins as remainder) = 50.

**Independent Test**: load `olamni/tutorial/ch11/redemption/` project + `main_olamni_ch11_redemption.dart`. Run `play_redemption`. Expected per §11.5: Alice holds 0 Carol-coins, 0 Dave-coins, owes 15 more Alice-coins. Bob holds 0 Alice-coins, 20 Carol-coins, 15 Dave-coins, 15 Alice-coins (remainder).

**Acceptance Scenarios**:
1. `agent.glp` cumulative: includes ALL §11.4 handlers (issue/accept/approve/mint + redeem + repay) per charter §2.4.
2. Redemption helpers `compute_repayments/5`, `take_from_currency/8`, `emit_repayments/5` present.
3. `boot.glp` wires `play_redemption`; final balances match §11.5.

### User Story 3 — Useful techniques (Priority: P3)

`ch11/useful-techniques.glp` collects `get_balance`, `set_balance`, `find_payment_to`, `has_payment_to`, `find_approval` if shared across the two project subdirs.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001** Two project subdirs (grassroots-flash, redemption) under `olamni/tutorial/ch11/`, each `{self.glp, agent.glp, network.glp, actors.glp, boot.glp}` per charter §2.2.
- **FR-002** Each paired with `glp_multiagent/lib/main_olamni_ch11_<use-case>.dart` cloned from `main_cssg_mad_modules.dart`.
- **FR-003** §11.1–§11.3 (introduction, blocklace, Grassroots Flash protocol) are mostly prose; their key data shapes (`block(tx(Currency, Payments, Comment), Pointers)`) are documented in `self.glp` as type definitions and in headers.
- **FR-004** Type definitions in `self.glp` derive from the protocol descriptions in §11.3 (Issue, Accept, Approve, Mint, Disapprove block content shapes) — verify alignment with `programs/typed_book/cryptocurrencies/` if present.
- **FR-005** Every clause carries `%%` paraphrase comments per charter §1.5.
- **FR-006** Each project must produce the documented end-state on REPL execution; final balances are the verification criterion.
- **FR-007** Cross-reference `programs/Bonds/` in headers — the bonds extension is informative for the Flutter template alignment but NOT the chapter source.
- **FR-008** §11.6 Security Properties (prose) referenced in headers.
- **FR-009** §11.7 Exercises out of scope per charter.
- **FR-010** REPL-test traces saved on disk per charter §Testing.
