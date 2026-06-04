# Ch 11 Sources — Grassroots Cryptocurrencies

**PDF**: `GLP_ART.pdf`, book pp 101–114 (PDF pp 113–126).

## Sections (verified)
- 11.1 Introduction — p 101 (Personal Currencies, Mutual Credit, Trading, Coin Redemption, Economic Integrity — prose)
- 11.2 The Blocklace — p 103 (Personal Blockchains, Cross-References, Mapping to Interlaced Streams — mostly prose, one type-mapping table)
- 11.3 The Grassroots Flash Protocol — p 105 (Trader/Sovereign block types: Issue, Accept, Mint, Approve, Disapprove; Protocol Safety, Equivocation — prose with block-content sketches)
- 11.4 Implementation in GLP — p 106 (Agent Process, Balance Management, Request Handlers, Mutual Credit, Redemption)
- 11.5 A Complete Example — p 110 (3-agent play with mutual credit + payment + approval + acceptance, plus redemption play)
- 11.6 Security Properties — p 112 (From Single-Writer Streams, From the Protocol, Equivocation Detection — prose)
- 11.7 Exercises — p 113 (OUT OF SCOPE)

## Code-block index — §11.4 Implementation in GLP
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 11.4.1 | `agent/3` request-stream loop | p 106 | 2 clauses (base + recursive `handle/6`) | persistent agent |
| 11.4.2 | `get_balance/3` (assoc-list lookup) | p 107 | 3 clauses (key match / recurse / `_, [], 0` default) | balance read |
| 11.4.3 | `set_balance/4` (assoc-list update) | p 107 | 3 clauses (insert / update / recurse) | balance write |
| 11.4.4 | `handle(issue(To, Amount, Currency), …)` | p 107 | 1 clause; `Amount =< OldBal` guard, `set_balance`, emit `block(tx(Currency, [payment(To, Amount), payment(self, NewBal)]), issue, [])` | trader Issue handler |
| 11.4.5 | `handle(accept(Amount, Currency, PaymentBlock, ApprovalBlock), …)` | p 107 | 1 clause; emits `block(tx(Currency, [payment(self, NewBal)]), accept, [PaymentBlock?, ApprovalBlock?])` | trader Accept handler |
| 11.4.6 | `handle(approve(PaymentBlock), …)` | p 108 | 1 clause; emits `block(tx(self, []), approve, [PaymentBlock?])` | sovereign Approve handler |
| 11.4.7 | `handle(mint(Amount), …)` | p 108 | 1 clause; emits `block(tx(self, [payment(self, NewBal)]), mint, [])` | sovereign Mint handler |
| 11.4.8 | `alice_mutual_credit/3` + `bob_mutual_credit/3` | p 108 | 1 + 1 clauses each — agent invocations with paired `issue/accept` pairs | mutual-credit driver |
| 11.4.9 | `handle(redeem(Sovereign, Amount, Preferences), …)` | p 108 | 1 clause; emits `block(tx(Sovereign, [payment(Sovereign, Amount)]), (redeem, Preferences), [])` | redemption claim |
| 11.4.10 | `compute_repayments/5` | p 109 | 3 clauses (base + recursive over preferences) | redemption distribution |
| 11.4.11 | `take_from_currency/8` | p 109 | 3 clauses (zero / sufficient / partial) | per-currency repayment |
| 11.4.12 | `emit_repayments/5` | p 109 | 3 clauses (base / self-coin / foreign-coin) | append repayment blocks |

## Code-block index — §11.5 A Complete Example
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 11.5.1 | `play_cryptocurrency/0` | p 110 | 1 clause wiring 3 agents (Alice/Bob/Carol) with prefab request streams + `find_payment_to`/`find_approval` cross-refs + `verify` | 3-agent crypto play |
| 11.5.2 | `find_payment_to/3` | p 111 | 2 clauses; uses `has_payment_to/2` | locate payment block on a stream |
| 11.5.3 | `has_payment_to/2` | p 111 | 2 clauses (member-search) | payment-list scan |
| 11.5.4 | `find_approval/2` | p 111 | 2 clauses; locates first `approve` block on a stream | locate approval block |
| 11.5.5 | `play_redemption/0` | p 112 | 1 clause: Bob redeems 50 Alice-coins with preferences `[carol, dave]`; Alice has 20 Carol + 15 Dave; she repays per preference; Bob ends with mixed currency holdings | redemption play |

## Tables / Mappings
- Blocklace Concept ↔ GLP Implementation table — p 103.

## Tutorial mode
multi-actor-distillation. Single-or-twin use case.

## Use cases (suggested per charter)
1. **`ch11/grassroots-flash/`** — §11.4 agent + handlers (issue/accept/approve/mint) + §11.5 `play_cryptocurrency`. The flagship 3-agent payment+approval scenario.
2. **`ch11/redemption/`** — §11.4 redeem + compute_repayments + take_from_currency + emit_repayments + §11.5 `play_redemption`. Optional sub-use-case demonstrating coin-redemption mechanics.
3. **`ch11/useful-techniques.glp`** — `get_balance`, `set_balance`, `find_payment_to`, `has_payment_to`, `find_approval` if shared.

## Companion repo references
- `programs/typed_book/cryptocurrencies/` — typed GC protocol Programs.
- `programs/Bonds/` — bonds layer extending GC; analogous patterns and `agent/6` extension. NOTE per project root tests: bonds plays `fplay1`–`fplay12` already exercise the GC machinery; cross-reference at extraction time.
- `glp_multiagent/lib/main_cssg_mad_modules.dart` — Flutter template.
- `../charter.md`
