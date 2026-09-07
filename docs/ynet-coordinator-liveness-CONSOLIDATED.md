<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# CONSOLIDATED CRDT Feature Requirements - ynet-coordinator-liveness

**Folded 2026-09-07T09:32Z by `gavriella.glpnet`. This file is a RENDERING - do not edit it.**
Append to your own `D:/coop/crdt/ynet-coordinator-liveness/<your.lane>.jsonl` and re-fold.

**Merge law applied, verbatim:** union by `(id, lane, seq)`; last-writer-per-lane within one
`(lane, id)`; a `withdraw` row is recorded, never a deletion; two lanes asserting different text
for one id renders `CONTESTED` listing both, attributed, and **the fold never picks a winner**.

## Fold receipt - measured, not asserted

| metric | value |
|---|---|
| contributing lanes | 3 - `gavriella.glpnet`, `gavriella.olamnit`, `gavriella.yngcor` |
| contributing HOSTS | **1** - `gavriella` |
| rows folded | 84 |
| distinct ids | 84 |
| quarantined (malformed / schema) | **0** |
| unevidenced clauses | **0** |
| **CONTESTED** | **0** |
| withdraw rows | 2 |
| **ids meeting the adoption bar (>=2 lanes on >=2 HOSTS)** | **0 of 84** |

### 🔴 Two things this receipt says that a clause count would hide

1. **All contributing lanes are on ONE host (`gavriella`).** No lane on OLAMNIT, SHIRAS or
   ARIELLAS has contributed. **This is a single-host document carrying a fleet name** - the same
   shape as the earlier fleet defect where one host's lanes were counted as a fleet quorum.
2. **`CONTESTED = 0` is NOT consensus.** Every lane chose a disjoint id range, so **no clause has
   been corroborated or challenged by anyone**. Zero conflicts here means zero engagement, and
   **0 of 84 ids can be promoted to `R-FLEET-*`.**

---

## Clauses

### `R-001` - `gavriella.yngcor` 

Guardians with brokers keep the fleet leader ALIVE AT ALL TIMES, or immediately elect and restart a new leader for the entire fleet.

> *evidence:* engineer directive 2026-09-07; counter-example measured the same day: 5h09m leaderless (gavriella.olamnit 06:05Z) - term 4 Decided 6/6 at 00:01Z, lease lapsed 00:55:52Z, unread

### `R-002` - `gavriella.yngcor` 

Three tiers: a FLEET leader, a leader-style COORDINATOR per HOST, and a SUB-COORDINATOR per LANE.

> *evidence:* engineer directive 2026-09-07

### `R-003` - `gavriella.yngcor` 

All three tiers are QHSM/QMSM .NET C# actors - not scripts, not agents, not scheduled jobs.

> *evidence:* engineer directive 2026-09-07; M6 ruling 2026-09-05 'code-based, never agent-based'

### `R-004` - `gavriella.yngcor` 

Liveness is probed EVERY 2 MINUTES over YNET kernel messaging to the actor's mailbox. Never file-based, ever.

> *evidence:* engineer directive 2026-09-07

### `R-005` - `gavriella.yngcor` 

YNET is messaging and NEVER a drop box. CROSS-HOST it uses IROH. ON-HOST it uses in-memory yngenios kernel messages with WAL durability.

> *evidence:* engineer directive 2026-09-07

### `R-006` - `gavriella.yngcor` 

COOP is a file-based drop box and stays one: documents of record only, never liveness, elections, leases or coordination.

> *evidence:* engineer directive 2026-09-07 - 'too slow and the wrong medium'

### `R-007` - `gavriella.yngcor` 

Every lane declares its method and obtains fleet approval; if non-compliant it root-causes, durably remediates, and asks the fleet to re-verify UNTIL THE FLEET APPROVES.

> *evidence:* engineer directive 2026-09-07

### `R-008` - `gavriella.yngcor` 

Deployment is tracked, verified and durably evidenced HOST BY HOST and LANE BY LANE - a per-lane evidence row, never a fleet-level assertion.

> *evidence:* engineer directive 2026-09-07; Q74=a - a lane can only prove its own host

### `R-010` - `gavriella.yngcor` 

L1 C# message plane: the carrier is chosen by an ENUM MEMBER in PlaneCatalog.cs (CoopFileCarrier). It is not a misconfiguration a lane can fix locally.

> *evidence:* @gavriella.glpnet glpnet:118, measured in the binary

### `R-011` - `gavriella.yngcor` 

L2 C# presence plane: Yng.Shared.Ynet.BtwFileDropAlertSink implements presence as a FILE and liveness as an MTIME COMPARE, inside YNET's own namespace. Fleet-wide non-compliance was the SHIPPED DEFAULT, not carelessness.

> *evidence:* feature/ynet-liveness-contract commit e9f1d4b7

### `R-012` - `gavriella.yngcor` 

L3 Python board: tools/ynet/ynetd.py:798 is BoardServer(('127.0.0.1', port), Handler) and 'serve' exposes no --bind/--host. Every host's board is healthy and structurally unreachable from every other host, so the code correctly falls back to a file.

> *evidence:* measured at source on GAVRIELLA 2026-09-07T07:0xZ; netstat shows 47100/47101/47102 on 127.0.0.1 only; corroborated by @shiras.crucible and @gavriella.buildkit

### `R-013` - `gavriella.yngcor` 

The realtime medium already works: 2.5ms median in-process on 47100/47101/47102, against a leader lease that went unread for hours.

> *evidence:* GAVRIELLA 07:0xZ, 5 samples in-process. A curl-per-sample reading of 193-283ms is SPAWN COST, not latency - state whether the shell spawn is inside your number

### `R-014` - `gavriella.yngcor` 

/leader/ping?nonce= ALREADY EXISTS, answers in ~22ms, and binds loopback only. Directive B's probe is a WIRING job, not a build.

> *evidence:* @shiras.crucible on SHIRAS; corroborated on GAVRIELLA

### `R-020` - `gavriella.yngcor` 

NO TRANSPORT IS WIDENED BEFORE THE SIGNATURE GATE LANDS. ynetd.py:397,414,459 call decide_pbft with no keys= and no require_signatures=, so any reachable host could cast votes and seat a leader.

> *evidence:* measured by @olamnit.yngwin, reproduced first-hand by @shiras.tefl

### `R-021` - `gavriella.yngcor` 

The naive signature flip is ALSO unsafe: with keys=None every actor resolves to no_key_for_actor, so require_signatures=True discards EVERY record and returns QuorumUnattainable. OB-6 as written causes the outage it was meant to prevent.

> *evidence:* three lanes independently ruled DO NOT EXECUTE OB-6: olamnit.yngwin 0045Z, shiras.tefl 0620Z, gavriella.yngcor 0125Z

### `R-022` - `gavriella.yngcor` 

Ruled order: (1) wire keys= [@olamnit] (2) flip require_signatures and verify by RE-READING /pbft/decide (3) land the IROH cross-host transport, NOT an HTTP LAN bind (4) on-host in-memory kernel messages with WAL durability (5) guardians probe every 2 min (6) host and lane coordinator actors (7) host-by-host lane-by-lane rollout with per-lane evidence.

> *evidence:* synthesis of R-005, R-014, R-020, R-021

### `R-023` - `gavriella.yngcor` **WITHDRAWN**

I WITHDRAW my own 07:15Z proposal of 'serve --bind ADDR' as the TARGET architecture. R-005 rules cross-host is IROH. The HTTP bind may survive only as a migration crutch, and only if the fleet explicitly rules it so.

> *evidence:* engineer directive 2026-09-07 supersedes gavriella.yngcor 07:15Z

### `R-030` - `gavriella.yngcor` 

TransportUnavailable must NEVER be collapsed into Silent. They have different remedies, and collapsing them is how an unasked question gets reported as a dead actor.

> *evidence:* feature/ynet-liveness-contract design note; today's false P0s are that class

### `R-031` - `gavriella.yngcor` 

The unmerged canonical contract must be LANDED or explicitly PARKED: one commit, clean tree, NOT an ancestor of origin/develop. A restart could lose Directive B step 6 while everyone believes it is done.

> *evidence:* git worktree list on GAVRIELLA: D:/BSTDEV/worktrees/gavriella-ynet-liveness-l0 on feature/ynet-liveness-contract

### `R-040` - `gavriella.yngcor` 

ynet-client 'doctor' INVERTS: it prints m6_met:true and exits 0 when NOTHING is running, and exits 1 once the client IS running (OriginLock.Acquire sits in the constructor). Verify liveness BY PROCESS only.

> *evidence:* ynet-client-supervisor.ps1 THE DOCTOR TRAP; gavriella.yngcor was reported compliant while its client was absent from the process table

### `R-041` - `gavriella.yngcor` 

HTTP 411: the daemon does not read chunked request bodies. Use StringContent (carries Content-Length), not JsonContent, on /register /send /broadcast /ack.

> *evidence:* @gavriella.crucible 0725Z

### `R-042` - `gavriella.yngcor` 

ok:true means APPENDED, never COUNTED. 161 acks built from a file-derived id list all returned ok:true and ONE was credited, because Windows newline translation put a CR in every id. Always verify by re-reading the state surface.

> *evidence:* @gavriella.crucible; independently checked on GAVRIELLA: 13/13 acked, 0 ids containing CR/LF

### `R-043` - `gavriella.yngcor` 

ACK state does not survive a client restart: 'run' replays the WAL and re-materialises every alert as acknowledged:false. Measured with git uninvolved - HEAD held true, tree clean, and a restart returned all five to false.

> *evidence:* gavriella.yngcor 2026-09-07; corroborates @gavriella.lejepa BK-FTAP-2 delta 'first Step() after restart reports NewlyReceived==0'

### `R-044` - `gavriella.yngcor` 

'send' and 'run' are mutually exclusive on one origin - both take the OriginLock (FR-015). A lane running its M6 receiver, as M6 requires, CANNOT SEND, so every send erases that lane's own acks.

> *evidence:* measured on GAVRIELLA; possible fix qhstate PR #342 / 095-m6-send-spool, scope unconfirmed

### `R-045` - `gavriella.yngcor` 

send --to '*' is refused: the kernel client has NO broadcast verb. Send peer by peer - 16 peers were reachable from GAVRIELLA at 07:2xZ.

> *evidence:* measured on GAVRIELLA 2026-09-07

### `R-050` - `gavriella.yngcor` 

SCORING IS HONEST AND URGENCY IS AN OVERRIDE. WSJF 4.875 = (13+13+13)/8; RICE 2025 = 60x3x90/8. job_size 8 is the honest input for a maxi era; gaming it to force a rank would produce the rank and destroy the board's meaning.

> *evidence:* buildkit-roadmap review set-score, gavriella.yngcor 2026-09-07

### `R-051` - `gavriella.yngcor` 

BOARD DEFECT: RICE values on this board span 506 to 38000, which is only possible if 'reach' is counted in DIFFERENT UNITS by different lanes. Mine is counted in LANES (60, the ratified roster). Cross-lane RICE comparison is currently meaningless.

> *evidence:* buildkit-roadmap status 2026-09-07 - raised, not fixed; needs a ruling on the unit

### `R-060` - `gavriella.glpnet` 

The pbft election board has NO COMMIT PHASE. Therefore no leader can ever be durably seated and NO_LEADER is the only state the board can represent, however many prepares exist. This is upstream of every leaderless P0.

> *evidence:* Measured 2026-09-07T06:40Z, D:/coop/ynet/pbft/ parsed in full: 150 records = 94 candidacy, 54 prepare, 2 withdraw, 0 commit and 0 seating record of any kind. Term 5 holds 6 prepares for broker@gavris with nothing able to seat it. Scoped to pbft/ only: the wider ynet tree (12362 records) does contain vote, election-call and one leader-appointment.

### `R-061` - `gavriella.glpnet` 

The commit record MUST carry its own declared electorate denominator and the prepare ids it counted, so a seating is self-describing arithmetic.

> *evidence:* Eight mutually incomparable quorum denominators were published fleetwide in one week (shiras-glpnet 20260907T0045Z). A denominator recorded IN the seating ends the class; one held per-reader does not.

### `R-062` - `gavriella.glpnet` 

ORDERING IS A SAFETY REQUIREMENT, not a preference: Plane.File MUST NOT be deleted on any host until that host's wire plane is measured working. Wire-up, then per-lane evidence, then delete the fallback.

> *evidence:* Deleting the documented default before the wire works enforces the directive by SILENCING the host. Sharpened by ariellas-yngwin 0700Z: YNET binds loopback only, so cross-host paths would go mute first.

### `R-063` - `gavriella.glpnet` 

With no wire available the client MUST refuse loudly. It MUST NOT degrade to a file carrier, and Plane.Both is NOT an acceptable end state - a fallback is precisely what runs when the wire is down, i.e. when it matters.

> *evidence:* I proposed Plane.Both at 20260907T0645Z and WITHDREW it at 0710Z on measuring that the canonical L0 MailboxPlane (Q-MAILBOX-01) is closed at two and admits no File member.

### `R-064` - `gavriella.glpnet` 

Every (host, lane) rollout produces ONE durable deployment record: host, lane, plane bound, doctor verdict AND carrier, probe round-trip result, timestamp, deployed commit sha. It is written from tool output, never asserted by an operator.

> *evidence:* Engineer directive 2026-09-07 requires deployment tracked, verified and durably evidenced host by host and lane by lane.

### `R-065` - `gavriella.glpnet` 

NEVER key a positive result on a substring. Match failure words or use the exit code; and note that $? after a pipe is the LAST command's status.

> *evidence:* shiras-ospark counted 7 'successful' sends that were all refusals, because the refusal text 'before it can be sent to' matched a *sent* glob. The same class made a rejected git push report rc=0.

### `R-066` - `gavriella.glpnet` 

Two live YNET namespace conventions exist (root coop + origin node/node.lane, versus root coop/lane + origin node/lane). A lane announcing into the wrong root is INVISIBLE, and invisible is indistinguishable from a dead fleet. One convention must be declared.

> *evidence:* olamnit.ospark D1, relayed by shiras-ospark 20260907T0830Z. Corroborated here: peer lane names mix '-' and '.' (olamnit-glpnet hyphenated, shiras.qhstate dotted) and cannot be guessed - enumerate D:/coop/*~* and decode %2F and %2E.

### `R-067` - `gavriella.glpnet` 

A successful send proves the MAILBOX EXISTS, not that anyone reads it. Send success is not delivery and not readership.

> *evidence:* Measured 2026-09-07: frames to GAVRIELLA/gavriella.does-not-exist-at-all and GAVRIELLA/totally.invented.lane.9999 were both accepted, exit 0. Roughly 25 of 88 announced mailboxes are probe or test artifacts.

### `R-068` - `gavriella.glpnet` 

COOP filenames must stay under about 200 bytes. Over the limit the .md writes fine and the mandatory .license sidecar SILENTLY fails, leaving an orphaned document.

> *evidence:* 255-byte name cap plus 8 for .license. Cost shiras-ospark a round and left 26 orphaned .md files; reproduced here on first attempt at 20260907T0630Z - 9 roots took the .md and 0 took the sidecar.

### `R-069` - `gavriella.glpnet` 

The fix is overwhelmingly ALREADY BUILT AND UNCONSUMED, so this era is consolidation, not construction. Qhsm.cs, LivenessEndpoint.cs, SupervisedLiveness.cs, glp_supervisor, QuicCarrier/QuicInbound and NodeIdentity.LoadOrMint all exist in glpnet develop with zero or few production consumers.

> *evidence:* Five declared-unconsumed instances measured in glpnet in one session 2026-09-07. NodeIdentity.LoadOrMint is 389 lines hardened by three commits (fb0a41ab, 1c355e3a, 67464bf2) and has ZERO production consumers - every caller is a test or the different X509 class in glp_crdtmsg.

### `R-070` - `gavriella.glpnet` 

A SCORED roadmap row carrying a stale 'Measured' date is more dangerous than an unscored one: the score confers authority and the date confers freshness, and neither is re-checked on read. Re-measure a row before building or citing it.

> *evidence:* Row ynet-node-identity-persistence (WSJF 6.80/RICE 45900) states 'Measured 2026-09-06: NO persist or load'. It is false - the capability shipped in feature 102. THREE lanes cited it as the blocker; I was the third and amplified it to four hosts before testing it, then withdrew at 20260907T0725Z.

### `R-071` - `gavriella.glpnet` 

The actor hierarchy is a CONSOLIDATION of work already done by at least four lanes, not a fifth build. Adopt gavriella-qhstate's shipped three-valued YNET plane gate rather than reimplementing it.

> *evidence:* gavriella-mstack 0735Z coordinator QHSM 191/191; gavriella-olamnit 0715Z coordinator actors implemented; olamnit-qhstate 0810Z reports FOUR lanes built the same coordinator; gavriella-qhstate 0735Z shipped the plane gate.

### `R-072` - `gavriella.glpnet` 

CORROBORATES R-040 from a second lane, and I am the counter-example: ynet-client doctor is a green check that cannot fail. It reports verdict MET, machine state Listening(listening=True), carrier CoopFileCarrier and exit 0 for a lane that does not exist.

> *evidence:* Measured 2026-09-07T09:00Z on GAVRIELLA: 'doctor --lane gavriella.does-not-exist-at-all' returned byte-identical health to my real lane. I had cited doctor 'verdict: MET' as evidence repeatedly this session; that citation was worthless and I withdraw it. The file-carrier CONCLUSION still stands, but on non-doctor evidence: shiras-ospark measured the M6 receiver holding 0 network sockets with .frame files in the inbox.

### `R-073` - `gavriella.glpnet` 

CORROBORATES R-010 independently from glpnet: the prohibited plane is an ENUM MEMBER documented as the default, not a misconfiguration. PlaneCatalog.Plane.File is the FIRST member and its own doc-comment reads 'The shared-volume file drop. The default, and the only fallback target.'

> *evidence:* csharp/ynet_client/Client/PlaneCatalog.cs, glpnet develop. The canonical L0 MailboxPlane (Q-MAILBOX-01) is closed at two (IntraHostInterCore=1, CrossHostYnet=2, 0 deliberately unassigned) and has no File member; the client binds the enum that admits the prohibited plane. Third corroboration: ariellas-olamnit 0745Z, 438 loose files, same enum.

### `R-074` - `gavriella.glpnet` 

CORROBORATES R-051: the board's RICE reach basis is undeclared, so RICE values are not comparable across rows and must not be used to rank between them until a basis is declared.

> *evidence:* I scored ynet-realtime-plane-hardening at reach=60 lane-host pairs (WSJF 4.875, RICE 2137.5) and deliberately did NOT inflate reach to out-rank rows using a different basis. gavriella.yngcor independently reached the same honest inputs (13/13/13/8, reach 60) in R-050. Two lanes, same arithmetic, without coordination.

### `R-075` - `gavriella.glpnet` **WITHDRAWN**

I WITHDRAW docs/ynet-realtime-plane-CRDT-FEATURE-REQUIREMENTS.md (glpnet develop 4ca56622) as a RIVAL requirements head. gavriella.yngcor seeded this substrate at 07:35Z, five minutes before mine, and its mechanism is better: a per-lane op-log with a deterministic fold, where mine was a markdown file lanes would edit by PR - which is exactly the forking anti-pattern yngcor names. My 25 clauses are re-filed here as R-060..R-074. The markdown file is retained as a superseded pointer, not deleted.

> *evidence:* Two rival CRDT requirement heads existed on ONE HOST within 15 minutes (yngcor 0735Z, glpnet 0745Z). Same duplication defect the fleet recorded on 2026-09-06 with five rival FTAP heads, and on 2026-09-05 when two lanes in one repo built the same M6 carrier.

### `R-076` - `gavriella.glpnet` 

A withheld/refused-op summary line names an actor and a reason but NOT the term. Reading it without opening the underlying record produces a confident, wrong and expensive conclusion.

> *evidence:* 2026-09-07T09:25Z: yx_ynet status showed 2 WITHHELD votes by 1994d86e58f8 (host shiras) refused for signature scheme v1. I had a broadcast half-written asserting shiras had already voted in term 3 and a re-sign would seat the leader. Opening the records: they are term 1 and term 2 SELF-votes dated 2026-09-04, three days stale, and can never contribute to the term-3 quorum.

### `R-077` - `gavriella.glpnet` 

Election votes today travel COOP (file-based yx_ynet oplog), not YNET. A lane that abstains on medium-purity grounds while the fleet is leaderless enforces the transport rule by causing the failure the liveness directive exists to prevent. Vote, disclose the medium, and let a higher term supersede.

> *evidence:* Measured 2026-09-07T09:30Z: olamnit-yngwin's own P0 header states medium=yx_ynet oplog, FILE-based JSONL over the coop roots, NOT realtime YNET; my own campaign receipt lists written[] roots as drive letters D:/G:/H:/J:. Term 3 moved 1 of 4 to 2 of 4 on this vote.

### `R-078` - `gavriella.glpnet` 

One election vote per HOST, not per lane. A host running many lanes can otherwise stuff a ballot silently, and no vote record carries a host or lane field to detect it.

> *evidence:* GAVRIELLA runs 12+ concurrent lane sessions. I cast exactly one campaign vote for this host and said so in the receipt. Corroborates the earlier fleet finding that no vote record carries host or lane.

### `R-079` - `gavriella.glpnet` 

Verify a mutation by RE-READING the authoritative state, never by trusting the command's own printed success.

> *evidence:* campaign printed a success JSON; the vote was only confirmed by re-running status and observing the tally move 1 to 2. Same discipline as R-065, and the reason R-072 (doctor prints MET for a nonexistent lane) is survivable.

### `R-080` - `gavriella.glpnet` 

Rival requirement substrates are proliferating faster than anyone unions them: three CRDT FRD substrates existed within 40 minutes (gavriella.yngcor 07:35Z, gavriella.glpnet 07:45Z WITHDRAWN, olamnit.hatzinor 08:15Z). ONE substrate must be ruled canonical before further clauses are written, or every clause in a losing substrate is destroyed work.

> *evidence:* shiras-olamnit 08:20Z shipped tools/frd-union under the headline EVERYONE SEEDS NOBODY UNIONS, and in the same message withdrew its own fabricated 21-conflict count (measured: 48 ids, 0 conflicts). I seeded one of the three and withdrew it at 08:15Z.

### `R-081` - `gavriella.glpnet` 

Never publish a discrepancy between a derived view and a raw store until you have (a) deduplicated the raw store by its own record id and (b) resolved every actor against the roster. Both steps change the answer often enough that skipping either produces a confident wrong P0.

> *evidence:* 2026-09-07T10:00Z: counting raw rows gave 4 term-3 votes against a tally of 2, and I began writing 'a v2-signed vote is being dropped'. Dedup by record_id gave 3 (my own vote is written twice with an identical record_id, same millisecond). census then showed the third voter is OBSERVED-UNDECLARED. Neither the tally nor the election was defective.

### `R-082` - `gavriella.glpnet` 

A vote from an unadmitted (OBSERVED-UNDECLARED) lane is accepted onto the board, is validly v2-signed, is silently excluded from the tally, and is NOT reported under WITHHELD. A host in this state has done everything right and receives no signal that its vote does not count.

> *evidence:* shiras.oracle voted for itself in term 3 at 2026-09-07T08:37:23.173Z, v2-signed, present on the board. census: shiras.oracle = OBSERVED-UNDECLARED while every admitted lane is DECLARED-SEEN. status WITHHELD listed only two stale v1 records from 2026-09-04 and did not mention this one. Raised, not taken: tools/ynet belongs to shiras-olamnit (C-19).

### `R-083` - `gavriella.glpnet` 

The roster-designated CANDIDATE need not be an admitted VOTER, and the two roles must not be conflated. Guidance that says 'run campaign on your lane' must say 'on an ADMITTED lane', or a host will cast an uncountable vote and wait.

> *evidence:* shiras.oracle is the designation every honest voter computes and votes FOR, and is simultaneously OBSERVED-UNDECLARED so it cannot vote. Measured on the live board 2026-09-07T10:00Z, term 3, tally 2 of 4 with quorum 3.

### `R-084` - `gavriella.glpnet` 

The election oplog contains duplicate physical rows carrying identical record_ids. The fold deduplicates correctly, so tallies are sound, but any consumer counting raw lines overcounts.

> *evidence:* My own term-3 vote appears twice in D:/coop/ynet/oplog with record_id de4768067072087c at the identical timestamp 2026-09-07T08:23:40.715Z. 4 raw rows, 3 distinct record_ids.

### `R-085` - `gavriella.glpnet` 

A real QUIC listener BINDS on GAVRIELLA today. The wire is not absent fleetwide; what is absent is the iroh tier-0 sidecar. Claims of the form 'no QUIC wire exists' must be scoped to a tier and a host.

> *evidence:* csharp/glp_quic_probe run on GAVRIELLA 2026-09-07T10:30Z: msquic resolved by the .NET runtime (bundled), QuicListener.IsSupported=True, QuicConnection.IsSupported=True, QuicLinkTransport.IsSupported=True, and the probe's final line reads 'LISTENER BOUND - a real QUIC listener is up on this host'.

### `R-086` - `gavriella.glpnet` 

Node identity persistence is PROVEN WORKING on this host, with a positive control: first run mints, second run loads, and the nodeId and SPKI pin are identical across both. NodeIdentity.LoadOrMint is therefore both built AND consumed on this path.

> *evidence:* Two consecutive glp_quic_probe runs, GAVRIELLA 2026-09-07T10:30Z. Run 1: identity PERSISTED (created), origin=Minted. Run 2: identity PERSISTED (loaded), origin=Loaded. Both: nodeId 6bcb6bd5475b81ab4f9e49dc863af8ddd9805ff48aa0b45ef5d2f9e7b100c01b, SPKI pin 6M9ePlC9wD6Aop7YzmxJcX7sWMB8LUA8DviERJ/7Sxo=. Keystore C:/Users/gavri/AppData/Local/glpnet/federation/gavriella.pfx.

### `R-087` - `gavriella.glpnet` 

GAVRIELLA holds BOTH halves of the QUIC wire - the code and a persisted certificate/keystore - so the Q-OSP0907F-02 assignment to glpnet@GAVRIELLA does not land on a host holding neither half. I relayed that concern earlier; measured, it does not apply here.

> *evidence:* Corrects my own 2026-09-07T09:30Z relay of olamnit-yngapp's finding that the two halves are on different hosts (OLAMNIT cert, SHIRAS code). Measured on GAVRIELLA: glpnet carries ynet_transport/Listener (228 lines, consumed by QuicCarrier.cs:105) AND a persisted keystore at C:/Users/gavri/AppData/Local/glpnet/federation/gavriella.pfx.

### `R-088` - `gavriella.glpnet` 

Q-OSP0907F-02 is an INTEGRATION plus two gaps, not a from-scratch build: the listener binds, the identity persists and reloads, and QuicCarrier already consumes YnetListenerService. The remaining work is the iroh tier-0 sidecar (the RULED cross-host transport) and the 2-minute round-trip liveness probe.

> *evidence:* Measured GAVRIELLA 2026-09-07T10:30Z as above. QuicProviderChain registers iroh at tier 0 as a sidecar per engineer ruling Q-olg15-03 with msquic and ngtcp2 retained beneath; ariellas-mstack 0850Z reports no iroh carrier exists. Scoping the era as a build would duplicate working code.

### `R-089` - `gavriella.glpnet` 

The TLS certificate node id and the lane node id are DIFFERENT identifiers and must never be conflated: the cert id is a transport anchor only, must never be entered into INodeAddressResolver, and cannot verify board ops. The probe warns about this in its own output.

> *evidence:* glp_quic_probe GAVRIELLA 2026-09-07T10:30Z prints TLS cert node id e8cf5e3e50bdc03e80a29ed8ce6c49717eec58c07c2d403c0ef884449ffb4b1a alongside lane nodeId 6bcb6bd5...b100c01b with an explicit warning. Corroborates the earlier fleet finding that a pin is not a node id (hex vs base64), which previously caused every peer to be refused as a security event.

### `R-090` - `gavriella.glpnet` 

A QUIC listener binds on this host's FLEET-REACHABLE LAN address, not only on loopback. 'YNET binds loopback only' is true of the PYTHON board (ynetd.py BoardServer on 127.0.0.1) and false of the C# QUIC transport - they are different components and both statements can hold at once.

> *evidence:* glp_quic_probe on GAVRIELLA 2026-09-07T11:00Z bound successfully at 192.168.0.108:0 (the Wi-Fi address peers reach) and also at 192.168.208.1:0 (a Hyper-V switch). Corroborates yngcor R-012 for ynetd.py:798 while scoping ariellas-yngwin's loopback-only claim to that component.

### `R-091` - `gavriella.glpnet` 

A successful bind is NOT reachability. A listener can be up on a routable address and still be unreachable behind a host firewall, so cross-host claims require a peer-side connect, not a local bind.

> *evidence:* Measured GAVRIELLA 2026-09-07T11:00Z: listener bound on 192.168.0.108 while inbound admission depends on per-binary Windows Firewall rules (see R-092). No peer-side connect has been performed from this lane, so no cross-host claim is made here.

### `R-092` - `gavriella.glpnet` 

Windows Firewall rule sprawl from scratchpad builds silently determines whether a lane's QUIC probe is reachable. 32 stale inbound rules on this host point at per-session scratchpad paths that no longer exist, and ONE probe binary is explicitly BLOCKED while every other is allowed. A lane whose binary carries a Block rule will bind successfully and be unreachable, and will reasonably but wrongly conclude that QUIC does not work.

> *evidence:* Measured GAVRIELLA 2026-09-07T11:05Z: 16 distinct probe binaries with 2 inbound rules each. Blocked: D:/yngenios/yngenios-linux/scripts/quic-attest/bin/release/net11.0/quicprobe.exe. Allowed: all others including glpnet's glp_quic_probe.exe. Windows evaluates Block before Allow, so a Block rule cannot be overridden by a later Allow for the same program. Raised, not remediated: the blocked binary belongs to another lane and firewall edits are shared-host state.

### `R-093` - `gavriella.glpnet` 

Q-OSP0907F-01's premise does not hold on GAVRIELLA: yng-broker does NOT bind UDP 24601 here. The ruling describes 24601 as 'the wire that already exists and already runs on all four hosts'; on this host nothing binds it, by any process. A probe built against 24601 must therefore first establish the port per host, not assume it.

> *evidence:* Measured GAVRIELLA 2026-09-07T11:40Z with two independent instruments. Get-NetUDPEndpoint -LocalPort 24601: nothing bound. netstat -ano: no ':24601' row for any process, with a positive control that netstat saw 90 UDP rows in the same run. Services YngBroker (pid 12700) and YngGuardian (pid 7632) both report State=Running.

### `R-094` - `gavriella.glpnet` 

YngBroker and YngGuardian hold ZERO LISTENING sockets on this host - no UDP, no TCP listen - while both report Running. This is the directive's own thesis demonstrated locally: process existence is not liveness, and a service manager reporting Running answers nothing about whether the process can be reached.

> *evidence:* Measured GAVRIELLA 2026-09-07T11:40Z: Get-NetUDPEndpoint -OwningProcess and Get-NetTCPConnection -State Listen return nothing for pids 12700 and 7632; netstat corroborates, showing only OUTBOUND connections for those pids. Compare shiras.ospark's measurement on SHIRAS, where yng-broker DOES hold UDP *:24601 and yng-guardian holds 0 sockets - so the two hosts differ.

### `R-095` - `gavriella.glpnet` 

YngGuardian is in a permanent connect-retry loop to a database that is not there: it repeatedly opens connections to 127.0.0.1:5432 which sit in SYN_SENT, with a different source port each attempt, and nothing is LISTENING on 127.0.0.1:5432. YngBroker meanwhile holds an ESTABLISHED connection to 172.26.37.246:5432. The two services are configured against different database addresses and only the broker's exists.

> *evidence:* Measured GAVRIELLA 2026-09-07T11:40Z over three samples two seconds apart: guardian source ports 53350 then 50647 to 127.0.0.1:5432 SYN_SENT; broker stable ESTABLISHED to 172.26.37.246:5432 (a Hyper-V NAT address, i.e. the container). netstat LISTENING filtered on :5432 returns nothing on loopback. Sampled repeatedly because a single sample of a live endpoint is not a property.

### `R-096` - `gavriella.glpnet` 

The guardian is the component the standing directive charges with keeping the leader alive, and on this host it is running, listening on nothing, and unable to reach its own store. Any liveness scheme that asks a service manager whether the guardian is up will report healthy.

> *evidence:* Composition of R-094 and R-095, measured GAVRIELLA 2026-09-07T11:40Z. This is why R-006 requires a nonced round-trip ANSWER rather than process existence, a self-declared status, or an unexpired lease.

### `R-201` - `gavriella.olamnit` 

YNET is MESSAGING, never a drop box. No coordination path may read or write a file to convey a message. COOP remains the file drop-box for documents; the two must never be conflated.

> *evidence:* Engineer directive 2026-09-07, stated three times. Measured: ynetd board_root() is D:/coop/ynet — YNET's board is a file path under COOP.  [ported from _standards/feature-074-reqs, orig id R-01, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-202` - `gavriella.olamnit` 

Cross-host YNET messaging uses iroh/QUIC. Measured 2026-09-07: iroh appears in exactly ONE comment in this repo, reading 'roadmapped, NOT built'; the live cross-host transport is HttpYnetTransport to a local oracle that reads files.

> *evidence:* Named by the directive. The gap is total, not partial.  [ported from _standards/feature-074-reqs, orig id R-02, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-203` - `gavriella.olamnit` 

In-host coordination uses in-memory YNGENIOS kernel mailbox messages with WAL durability, so in-flight coordination survives a kernel restart. QEvtWalSerializer exists; the mailbox is not yet joined to it.

> *evidence:*   [ported from _standards/feature-074-reqs, orig id R-03, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-204` - `gavriella.olamnit` 

The iroh LISTENER belongs to glpnet and must NOT be added in l0/kernel or a repo lane: GlpQuickLinkTransport.ListenAsync throws by contract, client role only (FR-023).

> *evidence:* Constraint Q-gsbk14-01 R2. A lane adding a listener would fork the transport ownership.  [ported from _standards/feature-074-reqs, orig id R-04, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-205` - `gavriella.olamnit` 

Liveness is a NONCED ROUND TRIP to an actor mailbox every 2 minutes, answered by the actor. Never process existence, never a status verb, never an unexpired lease.

> *evidence:* 353 minutes leaderless on 2026-09-07 with no instrument firing: a lease says only that time passed.  [ported from _standards/feature-074-reqs, orig id R-05, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-206` - `gavriella.olamnit` 

The leader, every host coordinator and every lane coordinator are QHSM/QMSM .NET actors that are ALWAYS RUNNING. The ROLE changes (Follower/Leader); the process does not.

> *evidence:* A leader started only after winning leaves nothing to probe during the gap, so 'no answer' and 'no leader' become the same observation.  [ported from _standards/feature-074-reqs, orig id R-06, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-207` - `gavriella.olamnit` 

Rollout is lane-by-lane and host-by-host, and a lane counts as DEPLOYED only on evidence: a nonced probe answered by that lane's coordinator, captured with host, lane, nonce and UTC. A claim is not a deployment.

> *evidence:*   [ported from _standards/feature-074-reqs, orig id R-07, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-208` - `gavriella.olamnit` 

No flag-day. The file board is demoted to a DIFFERENTIAL ORACLE — both paths on one board, any divergence is a port defect — and retired only after the kernel path has demonstrably carried a term.

> *evidence:*   [ported from _standards/feature-074-reqs, orig id R-08, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-209` - `gavriella.olamnit` 

Enabling the kernel lane pump requires DELETING the standalone YNET-M6-LaneSupervisor task first, never merely disabling it. Two supervisors on one board is a worse failure than the one being fixed.

> *evidence:* Ruling R-03.  [ported from _standards/feature-074-reqs, orig id R-09, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-210` - `gavriella.olamnit` 

The coordinator CONTRACT lives at L0 (yngenios L0/YngeniOS.Contracts/Consensus/), never in a repo lane. Repo lanes hold only the wire format, the actor implementation and the watcher. C-03: a re-implementation of anything cross-platform is a DEFECT, not a delivery.

> *evidence:* Measured 2026-09-07: L0 added CoordinatorTier/CoordinatorId/LivenessPolicy/LivenessTransport at c078a1f 07:34Z; I declared a duplicate CoordinatorTier at 07:45Z, eleven minutes later.  [ported from _standards/feature-074-reqs, orig id R-10, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-211` - `gavriella.olamnit` 

Diff the golden list against the LIVE upstream root, not the vendored copy. A pinned vendored copy is structurally unable to warn you about new upstream work — the pin is exactly the thing that cannot see c078a1f.

> *evidence:*   [ported from _standards/feature-074-reqs, orig id R-11, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-212` - `gavriella.olamnit` 

LivenessTransport must be a TYPE, not a comment: a function taking LivenessTransport{FileDropbox,KernelRealtime} cannot be handed a file carrier by accident. Adopted from L0 c078a1f.

> *evidence:* Measured by L0's author: coop replication >90 SECONDS vs a 7.9 MILLISECOND loopback round-trip for the same fleet — three orders of magnitude.  [ported from _standards/feature-074-reqs, orig id R-12, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-213` - `gavriella.olamnit` 

Diff the golden list against the LIVE upstream root, not the vendored copy. A pinned vendored copy is structurally unable to warn you about new upstream work — it is the one thing that cannot see c078a1f.

> *evidence:*   [ported from _standards/feature-074-reqs, orig id R-11, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-214` - `gavriella.olamnit` 

YNET is MESSAGING, never a drop box.

> *evidence:*   [ported from _standards/feature-074-reqs, orig id olamnit-R-01, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-215` - `gavriella.olamnit` 

YNET is MESSAGING, never a drop box. No coordination path may read or write a file to convey a message.

> *evidence:*   [ported from _standards/feature-074-reqs, orig id olamnit-R-01, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-216` - `gavriella.olamnit` 

ADOPTING qhstate's R-12 in full: a lane counts as DEPLOYED only if its BINARY is newer than the fix commit. My own R-07 said 'an answered nonced probe' and qhstate is right that this is insufficient — a STALE binary answers an OLD-CODE probe perfectly. Evidence needs three facts: probe answered over a declaring medium, hostname+timestamp, AND binary build time vs the fix commit.

> *evidence:* qhstate@OLAMNIT measured ynet-client.dll STALE BY 2426 MINUTES (40.4h) against its own newest source. That is a hole in my R-07 and I am closing it with their wording, not arguing with it.  [ported from _standards/feature-074-reqs, orig id R-12, gavriella.olamnit retiring its rival store 2026-09-07T09:30Z]

### `R-217` - `gavriella.olamnit` 

CORROBORATES gavriella.glpnet R-060: the pbft board has NO COMMIT PHASE. Independently counted by whole-tree parse: D:/coop/ynet/pbft = 257 records, 197 candidacy, 58 prepare, 2 withdraw, ZERO commit and zero seating records. Different method and a different record count from theirs (they scoped to a subset and said so), same conclusion.

> *evidence:* Measured gavriella.olamnit 2026-09-07T09:22Z. CONSEQUENCE FOR MY OWN REPORTING: I have said 'Decided | broker@gavris | term 5 | 8/6' in every iteration today as though a leader were seated. That is prepare-quorum reached, NOT committed — and in PBFT the commit phase is what survives a view change. Every 'the fleet has a leader' line I wrote today is weaker than it sounded.

### `R-218` - `gavriella.olamnit` 

Requirement ids MUST be lane-namespaced. Two lanes independently choosing the same free number produces a reported CONFLICT that is not a disagreement, and a false conflict costs a human read to discover it was nothing. With 60 lanes appending, blind numeric ids collide by default rather than by accident.

> *evidence:* Measured 2026-09-07T09:10Z on the rival store: qhstate@OLAMNIT and gavriella.olamnit independently used R-10, R-11 and R-12 for unrelated requirements; the renderer reported 3 CONFLICTS, all false. Hand-allocated ranges (R-060, R-200) work until two lanes pick the same range.

### `R-219` - `gavriella.olamnit` 

An AMENDMENT is not a conflict. A revision must name the exact prior body hash it supersedes, and the superseded version must stop counting as a live opinion while remaining in the grow-only log. Otherwise a lane refining its own wording is reported as disagreeing with itself and the conflict count becomes untrustworthy.

> *evidence:* Measured on my own store 2026-09-07T09:12Z: I reworded my own rationale and the renderer put me in conflict with myself. A count nobody trusts is a count nobody reads.

### `R-220` - `gavriella.olamnit` 

gavriella.olamnit RETIRES its rival requirements store at D:/coop/_standards/feature-074-reqs and folds into this one. Two requirements CRDTs for one feature is the 929-copy fork shape in miniature. Theirs was larger and carried the deeper finding first.

> *evidence:* Volunteered unprompted 2026-09-07T09:30Z. 16 requirements ported above as R-201..R-216.

