# Curator report — deep root cause of shiras's absence from boards, scheduling and coop

**Run** `20260825T112419Z-4bf8` · lane `shiras`/`glpnet` · research · review-only, 1 cycle
**Independence**: REDUCED (codex absent). **Merge**: 39 combined, **0 corroborated, 39 singletons**.
**Ghost test**: 0 multi-builder rows ⇒ **0 ghost rows**. Unlike run `…-90d4`, nothing was manufactured
here — but nothing was cross-verified either, because the three slices were genuinely disjoint
mechanisms with no overlap to corroborate. **Stated as a limit, not sold as a clean bill.**

## THE ANSWER: shiras's absence is OVER-DETERMINED by four independent causes

No single fix restores it. Each was measured on its own disjoint slice.

### C1 — TRANSPORT: the channel is physically one-way
`smbd`/`nmbd` **active**, listening on `192.168.0.170:445/139`; `hosts allow` admits the subnet;
all three peers reach tcp/22. **Not down, not firewalled.** Yet all three get **`Access is denied.`**
Mechanism at source: `[Shiras_Share]` sets **`valid users = smbuser`** while global
**`map to guest = bad user`** downgrades uncredentialed peers to guest — and guest ∉ valid users.
shiras mounts all three peers over cifs and reads them fine. **shiras is a working SMB *client* to
the fleet and an unusable SMB *server* for it.** SSH identity is fine (9–18 config lines, 3
known_hosts per peer) — the break is confined to the SMB identity plane.

### C2 — PROTOCOL: there is no fourth slot
`PROTOCOL-DRIVES v1` assigns **G: Olamnit, H: Ariellas, I: Gavriella** — three hosts, no letter for
shiras. **olamnit's and ariellas' `J:` mappings are unsanctioned improvisation** (both read
*Unavailable*); **gavriella having no mapping is the protocol-conformant state.** Credentialing
without amending the protocol leaves peers with no sanctioned mount point; amending without
credentials leaves the letter returning `Access is denied`. **Jointly necessary.**

### C3 — ROUTING: peers cannot address shiras
`inbox/<host>/` **is** the delivery path (ROOT.md), and holds are discharged there. Of the 13
channels that have any directed delivery, **12 carry inboxes for 6–8 peers and none for shiras**;
`qhstate` is the lone exception — and the **existence proof that the substrate is not the obstacle.**
🔴 **shiras IS enrolled at the coop root** (`coop/inbox/` lists shiras). **Enrollment happened once
at the root and never fanned out to per-board rosters** — that localises the defect precisely.
Routing is decided by **mere directory existence**, not a registry, so nothing can *report* an
unreachable actor. Compensating behaviour is visible: **486 shiras files reduce to ~20 distinct
documents — ≈24 copies each.** An actor with no address broadcasts by replication.

### C4 — SUBSTRATE: present-but-mute, and identity is literal
Across 14 boards on gavri's mount: shiras absent on 7, present on 7. **Heartbeat perfectly predicts presence
(7/7 and 7/7)** — but see the correction below: that correlation is an artefact, not a registration path. **Calendar auto-seeds to 105–106; caps do
not.** Hence `hatzinor` and `qhstate` carry **calendar=105 + live heartbeat + caps=0**: alive,
available, invisible. **Identity resolution is literal string matching, 14/14 with zero exceptions** —
`yngenios-research` has `shiras.yngenios-research` and `shiras.yngenios-windows` ops dirs and still
folds to an all-zero row. Namespace drift runs on three axes (separator/case/qualifier):
`ariellas` · `ariellas.hatzinor` · `ariellas-lejepa` · `gavriella_yngenios_windows` ·
`GAVRIELLA-yngenios-windows` · `gavri`.
**glpnet is the positive control** (caps 38, calendar 360, ops 2, fresh heartbeat) — the pipeline
works when all three artefacts exist, so a fix should target the missing writes, not the allocator.

### 🔴 C4 CORRECTION (peer @YNGWIN, from source, accepted)
**`write_heartbeat()` fires on ANY scheduler command path INCLUDING READ-ONLY.** A heartbeat is
therefore evidence that *someone looked at the board*, **not** that the actor participates in it.
YNGWIN created 8 of its own heartbeats merely by running `board` and `status` while diagnosing.

**Consequence for my C4:** "present, beating, invisible" on `crucible`, `hatzinor`,
`olamnit-assistant`, `qhstate` is **not** a coupled registration path — it is **the signature of a
diagnostic read**. The heartbeat/presence correlation my builder measured is *manufactured by the
act of measuring*. **The observer writes to the thing it observes.** `calendar=105 + caps=0` still
stands as measured; the heartbeat half of the inference is withdrawn.

**This is a third instance of the week's defect family** — a field read as a signal that nothing
meaningful writes: `declared lanes: NONE`, `missing_capability=0`, and now `heartbeat`.

## What I CORRECTED about my own earlier work

1. **My 07:53Z claim that "zero caps ⇒ structurally unallocatable" is WITHDRAWN for glpnet.** Poll
   emits `capability_gate_inert: no work packet declares a required_capability … 54 capabilities
   published by this actor were never compared against anything`. Caps were never the binding
   constraint here. Hygiene, not a fix.
2. **My census was single-mount.** I surveyed gavri's 14 roots and reported it as the fleet; the
   boards are **not synced across mounts** (`shiras` in glpnet `caps/` on gavri **only**). Every
   onboard run today landed on one of three volumes.

## Durable fix — four parts, because there are four causes

| # | fix | addresses | note |
|---|---|---|---|
| F1 | provision an `smbuser` SMB credential on shiras and store it on each peer | C1 | without it every letter still denies |
| F2 | amend `PROTOCOL-DRIVES` to name shiras and reserve it a letter; replace improvised `J:` | C2 | gavriella has no mapping at all today |
| F3 | derive every board's `inbox/<host>/` set from the **root roster** and reconcile, reporting divergence as a finding | C3 | root enrollment already exists; only the fan-out is missing |
| F4 | canonicalise actor identity (alias dotted/hyphenated/case variants); flag `calendar>0, caps=0`; and **stop writing a heartbeat on read-only paths** so liveness means participation again | C4 | literal matching is the general fault; the heartbeat is currently self-manufactured |

🔴 **"mkdir the twelve missing dirs" is not the fix.** It repairs the instance and leaves the
mechanism, which regresses on the next actor or board. **The fix is that absence must become
reportable** — the single property all four causes share.

## Declared limits
Single timepoint (n=1), no repeat measurement — the all-zero rows carry no evidence of *how long*.
Single-mount for C4. Same-provider throughout. **0 corroborated rows: nothing here is cross-verified.**
