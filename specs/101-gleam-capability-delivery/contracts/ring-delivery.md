# Contract — ring delivery

## C1 · The runtime-free contract
The shared surface carries **no third-party runtime dependency**. BEAM and AtomVM each provide one
realization held to it (`008` FR-017; LATTICE line 35: *per-runtime single implementation held to a
shared contract, never peers*).

**Refusal C1-R:** a build that introduces a runtime dependency into the contract **FAILS**.
Positive control (SC-004) introduces one and asserts the failure.

## C2 · Ring admission
Admission is by **measured contract consumption**, never by a name (`008` FR-018).

**Refusal C2-R:** a subtree offered on name alone is refused **with the name quoted**.
Positive control (SC-005): `glp_gleam` is not the polyglot-L0 `kv`/`mailbox`/`network` service set —
it is a GLP language runtime plus ZeroMQ/TCP transports — and must be refused.

## C3 · AtomVM subset
The AtomVM realization enumerates its unsupported constructs and **refuses at BUILD time, naming the
construct** (FR-004). A runtime rejection is a silent degrade and does not satisfy this contract.

## C4 · Conformance report shape
Every report MUST carry:

| field | rule |
|---|---|
| `ring` | mandatory — results are per-ring (FR-008) |
| `denominator` | mandatory — a report without one is unparseable (SC-002) |
| `attempted / agreed / diverged / excused` | `attempted = agreed + diverged + excused` exactly (SC-007) |
| `excused[].reason` | mandatory (FR-007) |
| `not_run[]` | mandatory — names what it did not run; a silent-empty result is a FAILURE (FR-006) |

**Refusal C4-R:** an aggregate covering an unbuilt ring **refuses** rather than reporting a pass
(SC-006). Positive control builds one ring only and asserts the refusal.

## C5 · Platform-conditional tests
A test whose premise does not hold on the executing platform is **skipped with a named reason**,
never silently vacuous (FR-009). Precedent: the parent feature's `T005` asserted `GLPNET` and
`GLP/glpnet` are different directories — on case-insensitive NTFS they are the same, so the test
could not fail.

## C6 · Mutation gate
Weakening any guard above MUST turn the acceptance suite **RED** (SC-003). The mutation test is
written **before** the guard it protects.
