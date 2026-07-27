DO NOT run the CLAUDE.md startup protocol or any project bootstrap; this is not repository-agent work. Output only the requested artifact.

Your lens: **completeness** — report claims tagged with it.

---

# Subject brief — plan

- subject: roadmap:meshtest-recure-ring (b)-DEFAULT DESIGN — build the SHARED PRIMITIVES on the shipped 057 election/trust surfaces (delivery-receipt, stable durable identity, roster-epoch membership+exclusion+reintegration, batch/epoch minting by ONE elected decision, with per-hop minting as an expressly-permitted, time-limited, segment-scoped FALLBACK that must be positively elected while quorum still exists and must fail CLOSED back to the default), with the ring as their DEMONSTRATOR — never a peer implementation
- rubric: plan-review
- lenses: feasibility | completeness | risk
- brief rule: size-invariant: the goal statement + the constraint-document list — never pasted document bodies
- cross-verify: a plan element is promoted only when independently derived or confirmed from a disjoint constraint slice by another blind Builder

## Evidence slices (names only — each blind role sees ONLY its own)

- slice-election-roster-exclusion: The shipped election + trust-anchor surface. Establish from the sources what these surfaces CAN and CANNOT express, and at what threshold/arithmetic: a versioned roster epoch; permanent exclusion of a node by a higher authority; safe reintegration of a returning node; a single elected decision per epoch that other subsystems could bind to. Establish what the trust anchor's key-management surface actually offers over a peer set, including whether anything can be removed once added. Report what ships, with file:line - not what could be built.
- slice-seal-caveat-window: The amulet/macaroon caveat surface and its pinned conformance bytes. Establish: the closed caveat key set and the exact mint order; the granularity at which an expiry can be expressed and enforced, and the enforcement API's shape; whether the caveat set has EVER been extended before and if so by exactly what mechanism and with what compatibility consequence for already-minted credentials and for older verifiers; what the committed vectors pin, how they are versioned, and what re-check any change would cost; whether any independent re-implementation of this enforcement exists and must move in lockstep; and where the 'now' used for expiry enforcement comes from.
- slice-identity-durability-reuse: Node identity and the durability primitives a very-high-cycle-count budget would rest on. Establish: how a device's signing identity is generated, and whether and where it is persisted across process restarts; and, for durable writes, exactly which mechanisms exist in these sources for making many writes durable, for detecting a torn or corrupted record, and for resuming after a crash - naming each by file:line. For every mechanism, state plainly whether it EXISTS AND COULD BE WIRED, or DOES NOT EXIST AND WOULD HAVE TO BE WRITTEN. Keep those two categories strictly apart.
- slice-mesh-steering-emergence: The mesh + link layer. Establish: exactly which fields the wire frame carries and which of them a forwarder consults, and therefore whether an originator can express an intended PATH rather than only a destination; what mechanisms exist for changing a node's neighbours or membership at runtime; what inputs the routing cost function accepts, from whom, and whether any bound constrains what a contributing node can drive it to; whether any history, smoothing, or flap-tracking of route quality over time exists in these sources; and what topology scenarios the test/verification sources actually exercise, naming them.
- slice-mint-authorization: The coin's mint and reward surface. Establish: on which code path(s) a value-creating operation's authorization evidence is actually verified, and on which it is not, naming each by file:line; what invariant any audit/conservation check actually enforces, and precisely which classes of defect it would and would not detect; whether reward units of the smallest denomination can be split and/or merged, and under what conditions either is refused; and what the shipped corroboration surface requires of evidence before a claim may be auto-endorsed. Report the code's behaviour, not its comments - and where a comment and the code disagree, say so.

---

## Your evidence slice: slice-mint-authorization

The coin's mint and reward surface. Establish: on which code path(s) a value-creating operation's authorization evidence is actually verified, and on which it is not, naming each by file:line; what invariant any audit/conservation check actually enforces, and precisely which classes of defect it would and would not detect; whether reward units of the smallest denomination can be split and/or merged, and under what conditions either is refused; and what the shipped corroboration surface requires of evidence before a claim may be auto-endorsed. Report the code's behaviour, not its comments - and where a comment and the code disagree, say so.

Sources (yours ALONE — do not consult anything outside this list):

- Olamnit/Olamnit.Coin/Ledger/
- Olamnit/Olamnit.Coin.Rewards/
- Olamnit/Olamnit.Coin.Tests/Ledger/
