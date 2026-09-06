# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""
Emit the FTAP clause CRDT as a grow-only JSONL op-log.

WHY THIS EXISTS
---------------
`FINDING-20260906T2010Z-olamnit-yngraw` measured the fleet's 24h-plan artifact:

    44 distinct template documents - 4,080 copies - 271.6 MB
    18 versions of the BK-FTAP-1 chain, v2..v16 (two forks, a v14.1)
    +17.6 KB per version, MONOTONIC, 14 increments, not one decrease
    a `v2-RATIFIED` with 131 copies that the chain then ignored anyway

Root cause, and it is our own rule read literally: each version RE-EMBEDS its entire
predecessor verbatim below its own delta, because the directive says the work must be
"STRICTLY WITHOUT SUMMARISATION OR COMPRESSION", and re-embedding the ancestor is the most
literal possible compliance. Nobody cheated. The rule produced the behaviour.

    Content preservation and ancestor duplication are NOT the same thing.

This file is the structural fix. Losslessness is carried by a GROW-ONLY SET OF CLAUSE
RECORDS, each holding its requirement text once, with lineage - never by re-embedding an
ancestor that is already durably stored as its own file. Adding a requirement appends one
record. Nothing is ever rewritten wholesale, so the artifact cannot grow by +17.6 KB per
version, and two lanes editing different clauses do not fork: they merge.

MERGE SEMANTICS (what makes it a CRDT, not just a file)
------------------------------------------------------
  * The clause SET is grow-only (union by `id`). A clause is never deleted, only superseded
    by a later record naming it in `supersedes`.
  * Per-clause fields converge last-writer-wins on `hlc`, tie-broken by `actor`, so the fold
    is deterministic and order-independent: any two lanes replaying the same op set in any
    order produce byte-identical output.
  * `render_ftap.py` DERIVES the .md. The .md is never hand-edited - that is what forks.

The rendered .md is the engineer's cut-and-paste surface; this JSONL is the truth.
"""
from __future__ import annotations
import json, pathlib

ACTOR = "ariellas.glpnet"
SRC = "engineer fleetwide directive 2026-09-06 (re-issued 2026-09-06T22:xxZ with item [13])"
LINEAGE = ["BK-FTAP-1 v2..v16 (18 versions, superseded by clause-merge)",
           "FLEET-T24-ACTION-PLAN v1.0 gavriella-glpnet",
           "FLEET-T24-ACTION-PLAN v1.1 ariellas-glpnet (C-4/C-5)",
           "FLEET-T24-20260906T2200Z issued plan"]

C = []
def clause(cid, kind, horizon, title, body, **kw):
    rec = {"op": "upsert", "id": cid, "kind": kind, "horizon": horizon,
           "title": title, "body": body, "actor": ACTOR, "hlc": 1_000 + len(C),
           "source": SRC, "lineage": LINEAGE}
    rec.update(kw)
    C.append(rec)

# ---------------------------------------------------------------- horizons
clause("H-24", "horizon", "24h", "24-hour horizon",
       "Closes 2026-09-07T22:00Z. Carries items [01] [02] [03] and the whole standing register.")
clause("H-48", "horizon", "48h", "48-hour horizon",
       "Closes 2026-09-08T22:00Z, INCLUSIVE of the 24h window. Carries items [04] [05] [06].")
clause("H-72", "horizon", "72h", "72-hour horizon",
       "Closes 2026-09-09T22:00Z, INCLUSIVE of the 24h window. Carries [07]-[13].")
clause("H-7D", "horizon", "7d", "7-day horizon",
       "Closes 2026-09-13T22:00Z. NEW in this consolidation. The directive named 24/48/72h "
       "explicitly and a 7-day plan only in the consolidation instruction, so the 7-day "
       "content below is DERIVED, not quoted, and is marked as such in every clause "
       "(`derived: true`). It must be ratified by the engineer before it binds.")

# ---------------------------------------------------------------- automatic-failure floor
for i, t in enumerate([
    "Regular YNET PBFT elections are HELD, and an effective fleetwide leader is maintained for the whole window.",
    "A hostwide leader is maintained for EACH host, coordinating across hosts with the fleetwide leader.",
    "YNET / realtime / GLPNET QHSM/QMSM-enabled message-OVER-WIRE mailboxes work.",
    "QHSM/QMSM-enabled IN-MEMORY L0 message-based mailboxes work.",
    "The kernel effectively controls ALL QHSM/QMSM-based allocation and OS processes.",
    "EACH lane and EACH host separately has its OWN QHSM/QMSM CODE-BASED client - NEVER agent-based - "
    "to participate as a receiver in YNET comms. A lane whose only participation is an agent reading a "
    "mailbox by hand has NOT met this, however diligent the agent: the receiver must be a process the "
    "kernel can supervise.",
    "YNGENIOS apps work, INCLUDING the 3270-type terminal and the YNET mailbox-based virtual terminal.",
], 1):
    clause(f"AF-{i}", "automatic-failure", "24h", f"Automatic-failure criterion AF-{i}", t)

# ---------------------------------------------------------------- standing mandates
clause("M-1", "mandate", "standing", "Election authority",
       "yng-broker / yng-guardian run on EACH of the four hosts and are the DESIGNATED PBFT LEADER "
       "ELECTOR FOR ALL PURPOSES - including electing the oracle leader, the fleetwide coordinator, "
       "and the fleetwide signature verifier. (Stated six times verbatim in the source; once here.)",
       repeats_in_source=6)
clause("M-2", "mandate", "standing", "One board, four oracles",
       "The oracles on all four hosts must work as ONE realtime single-truth board. Lanes connect to "
       "their HOST-LOCAL oracle; the four reconcile so every lane on every host always sees one board "
       "only. Durable board artifacts - current board AND board-era history - use CRDT logic.")
clause("M-3", "mandate", "standing", "QUIC carrier",
       "irohnet / QUIC is THE QUIC network implementation for YNGENIOS, adapted and fully integrated "
       "FROM L0 UPWARD. GLPNET must be able to configure a working QUIC IP listener for the broker, "
       "guardian, oracle and admin services. Ref https://share.google/aimode/nmPevkNDIQYhbj1v7",
       repeats_in_source=5)
clause("M-4", "mandate", "standing", "Cross-platform code lives at L0",
       "ALL cross-platform code MUST be implemented as L0 shared capability in `yngenios`. Per-platform "
       "hardening lands separately in yngenios-windows and yngenios-linux. Mandatory, not preference: a "
       "capability implemented twice at L1 is the defect this rule prevents.")
clause("M-5", "mandate", "standing", "Per-lane era quota", "See clause SC-1. Restated here because it governs planning, not only scoring.")
clause("M-6", "mandate", "standing", "Kernel-managed supervision and send-over-wire",
       "RULED, not open. Every QHSM/QMSM participant is supervised by the kernel and sends over the wire "
       "through the kernel's mailbox, not a private side channel.")

# ---------------------------------------------------------------- the thirteen service items
ITEMS = [
 ("OBJ-YS-STORE","[01] YStore | YS | S3-compatible distributed storage, can harness real AWS S3","24h","ospark",
  "Build on the current MinIO-based implementation in the OSPARK repo/lane, but MIGRATE AWAY from MinIO to a new "
  "YNGENIOS-native version, taking as much as possible from MinIO's open source, while instead using best-of-breed "
  "alternatives to construct a YNGENIOS variant optimised for our iroh substrate (with other QUIC fallbacks) and the "
  "ability to serve multiple devices in the YNGENIOS mesh. Use one or more of these as a VENDORED BASE and the others "
  "as parts and ideas: RustFS (Rust, performance-critical & small-file workloads, Apache-2.0, highly "
  "commercial-friendly); Garage (Rust, geo-distributed & multi-datacenter self-hosting, AGPL-3.0, self-hosting focus); "
  "SeaweedFS (Go, storing billions of files & fast data lakes, Apache-2.0). Ref "
  "https://share.google/aimode/Zi4hoCqBzPcQOjeDM . Create a working WRAPPED prototype with a YNET/YNGENIOS kernel "
  "realtime-mailbox main interface analogous to the AWS-S3-compatible service we need later for compatibility. Store "
  "all files across the 12 TB disks (usually the E: mount) on SHIRAS, OLAMNIT and ARIELLAS in a YS master "
  "subdirectory. Also a 100 GB cache for the most frequently used files for high-speed access on the D: drive of "
  "SHIRAS, ARIELLAS and OLAMNIT. Fully accessible from GAVRI (possibly also a 100 GB most-used cache on D: under YG).",
  "Prototype serves an object across two hosts over iroh; cache hit MEASURED, not asserted. BLOCKED pending A-1."),
 ("OBJ-YQ-PG","[02] YQuery | YQ | distributed data + query - PostgreSQL relational storage","24h","ospark, opgan",
  "Absorbed YLake; was YSql/YPGSql. Build on the current PostgreSQL 18 implementation in the OSPARK and OPGAN repos. "
  "Create a TRIANGLE-REPLICATED PostgreSQL 18 service with HOT-HOT-HOT nodes on OLAMNIT, ARIELLAS and GAVRIS, data on "
  "the 12 TB E: drives in the YQ top-level folder, which must ALSO store a clone of the full program install and "
  "config installed on D: on each of the three hosts. D: hosts a 100 GB section for currently active logs inside the "
  "YG folder; all non-active logs move to E:. Log backups and regular snapshot backups of all databases are stored on "
  "the 18 TB drive on ARIELLAS. The three instances must be configured so all three DBs are continuously "
  "HOT<->HOT<->HOT replicated among the three, with continuous monitoring and LOG BACKUP EVERY 30 MINUTES. ALSO "
  "create a working prototype of the PGLITE INTERFACE SIGNATURE but using a YNET/YNGENIOS kernel realtime-mailbox "
  "interface connecting to a NAMED POSTGRES DB instead of a PGlite dataset - so services transparently switch to this "
  "interface with an ultra-durable DB backing instead of PGlite while on, or connected to, the workstation, and use a "
  "PGlite replica only on mobiles, tablets and similar small edge devices. IROHNET, IROH, QUIC AND FULL YNET SUPPORT "
  "MUST BE DESIGNED INTO THIS SERVICE FROM THE WORD GO.",
  "Failover exercised across all three nodes; a service switched from PGlite to PG WITHOUT code change."),
 ("OBJ-YQ-DUCKLAKE","[03] YQuery | YQ | distributed data + query - DuckLake data-lake","24h","{{OWNER}}",
  "The current DuckLake implementation is spread across many repos across every host in the fleet. Create a WRAPPED "
  "TEMPLATE FOR CREATING DUCKLAKES using [02] YQuery's PostgreSQL 18 backing relational storage integrated FOR THE "
  "CATALOG (instead of PGlite as we do currently), with storage based inside the [01] YStore service. Create a working "
  "prototype of a PGlite-interface-signature-EQUIVALENT DuckLake interface, but using a YNET/YNGENIOS kernel "
  "realtime-mailbox interface connecting to a named Postgres DB instead of a PGlite dataset - so services can query "
  "and write in the DuckLake using SQL with TRANSPARENCY between the seasoned-Parquet part of the data and the part "
  "DuckLake still stores in Postgres until it can be written to Parquet. Same storage/cache layout as [02]. IROHNET, "
  "IROH, QUIC AND FULL YNET SUPPORT DESIGNED IN FROM THE WORD GO.",
  "One SQL query spans Parquet and PG rows transparently. Depends on [01] and [02]."),
 ("OBJ-YN-INTERCHANGE","[04] YNterchange | YN | Streaming + queuing of content - the face of the mailbox and link services","48h","{{OWNER}}",
  "Was YStream/YXchange. Use the YNGENIOS kernel and realtime-kernel capabilities, the YNET (iroh/QUIC) capability, "
  "and the Windows and Linux workstation implementations for ULTRA-HIGH-SPEED MEMORY SHARING for streaming between a "
  "producer and one or more consumers inside a single host, and ULTRA-HIGH-SPEED IROH/QUIC NETWORK FLOWS BETWEEN "
  "HOSTS - so a producer can share content it generates, or reads from an on-disk file, or generates by reading and "
  "modifying an on-disk file or another ultra-high-speed stream, or several of those, and emit the result into a "
  "stream. THE IDEA: use the SYNTAX AND OVERALL SEMANTICS OF THE MAILBOX MECHANISM, but instead of a copy-based "
  "implementation use the MEMORY-SHARE MECHANISM FOR THE MESSAGE CONTENT (as opposed to the ultra-streamlined binary "
  "wrapper/envelope, which is unchanged). In addition this service MUST provide PYTHON, GLEAM (BEAM and AtomVM), "
  "C#/.NET, GLP and JAVA/SCALA/JVM NATIVE STREAMING APIs aligned with each language/platform's native streaming API "
  "interfaces, so code in those languages uses the service transparently; AND we must design and deliver a REST/MCP "
  "API for this streaming to access it transparently from code that works in that interface style.",
  "Zero-copy proven by MEASUREMENT, not design intent; all six API surfaces exercised.",),
 ("OBJ-YM-MAP","[05] YMap | YM | Node discovery, emergent directory, routing information","48h","{{OWNER}}",
  "How participants and devices are found. We need an INTERNET-SCALABLE FEDERATION-BASED PUBLIC DNS built local-first "
  "but robustly and always rule-conformant to internet-scale DNS design, PAIRED with the addition of STRICTLY PRIVATE "
  "NESTED SUBSPACES in the global space, all built local-first but enabled to allow space-specific, and also truly "
  "global, regional and special-interest RULE SETS ENFORCED THROUGH QHSM/QMSM-BASED (BLOCKCHAIN-INSPIRED) AUTOMATED "
  "AUTONOMOUS CONTRACTS. Harvest and DURABLY STORE the corpus referenced by the 17 unique links (the source listed 29, "
  "of which 12 were exact duplicates). THEN VERIFY ROBUSTLY, ALSO USING MULTIPLE CODEX ANGLES, to give a corpus of "
  "TRULY ORIGINAL UNDERLYING SOURCES from reputable technical, commercial and academic sources to use in the design. "
  "Use ALL available YNGENIOS capabilities, realised and planned, in the design. Same six native API surfaces + "
  "REST/MCP as [04].",
  "Corpus durably stored AND primary sources extracted. See A-2.",
  ),
 ("OBJ-YG-GUARD","[06] YGuard | YG | the guardian/broker vessel","48h","{{OWNER}}",
  "The Guardian service is provided JOINTLY by the guardian and broker instances on Windows and Linux and the "
  "equivalent implementation inside the YNGENIOS App (MAUI Blazor Hybrid) and its platform-specific deployables "
  "(Android, Windows, Linux, iOS, etc). For all those we must have one or more container-managed spaces, as we have "
  "for guardian and broker on Windows and Linux and their YNGENIOS-app equivalents. DESIGN AN L0-LEVEL CROSS-CUTTING "
  "DESIGN AND ARCHITECTURE FOR SUCH A VESSEL - i.e. a container - so it can host EITHER any small number of very "
  "active, intense processes OR extremely large numbers (MILLIONS) of ultra-lightweight in-memory processes, "
  "SCHEDULABLE WHEN MESSAGES ARRIVE ON THEIR MAILBOXES but otherwise INERT and merely memory structures. This is "
  "EQUIVALENT TO THE SCALA ACTOR DESIGN, which has the same characteristic: the number of activatable actors depends "
  "only on their intensity and the underlying hardware (memory size, number of CPUs and cores). Create the "
  "MESSAGE-BASED KERNEL API for processes with sufficient capability authorisation to SPAWN - and thus create - but "
  "potentially also TERMINATE, or ask for DURABLE HIBERNATION AND LATER REANIMATION of, any such QHSM/QMSM-based "
  "process. IN PRINCIPLE THE DESIGN MUST ALLOW A HIBERNATED PROCESS TO BE SHIPPED FROM ONE NODE TO ANOTHER, OR EVEN "
  "TO A NODE ON ANOTHER HOST. Verify robustly, also using multiple codex angles, for original sources. Same six "
  "native API surfaces + REST/MCP as [04].",
  "A hibernated process reanimated ON A DIFFERENT HOST."),
 ("OBJ-YE-ENGAGE","[07] YEngage | YE | The tasktop interactive surface","72h","yngapp",
  "FULLY AND PROVABLY MIGRATE ALL OLAMNIT ASSISTANT CAPABILITIES into the YNGENIOS App (MAUI Blazor Hybrid for "
  "Windows, Android, Linux and Apple platforms) and make it fully connected to YNGENIOS for Workstation on Linux and "
  "Windows, so that any instance on a mobile/tablet or workstation/server device can use the instance on the same "
  "host, or one or more reachable local instances of the workstation service, or - if that is not feasible - one or "
  "more remote instances over the internet using IROH HOLE-PUNCHING and/or the VPN-based internet access point we "
  "currently use for OLAMNIT and the YNGENIOS app via a public URL, in order to access any and all workstation-based "
  "YNGENIOS services AND AS A RELAY POINT to reach other devices not accessible over the YNGENIOS local mesh network "
  "directly. Fully leverage SYNCFUSION's latest web surface for improved look and feel of the YNGENIOS app, a.k.a. YE "
  "YEngage, the interactive tasktop on which all other applications will be deployed.",
  "PROVABLE retirement of olamnit-assistant via code review AND headful+headless regression testing."),
 ("OBJ-YB-BUILD","[08] YBuild | YB | Component + subsystem builder (product surface)","72h","buildkit",
  "This is really buildkit and the /bk-* toolkit, but with an integrated YEngage (YE) interactive tasktop UX and the "
  "ability to surface a HEADLESS, FULLY CLAUDE-CAPABLE VIRTUAL TERMINAL on the Windows or Linux workstation onto a YE "
  "app instance on the same host or other devices, safely through the YNET mailbox and streaming capability if needed, "
  "over the underlying ultra-safe YNET capabilities. IN ADDITION each headless Claude-capable virtual terminal MUST "
  "have a QHSM/QMSM YNGENIOS-MAILBOX-ENABLED MULTI-SESSION COORDINATOR that routes agent output to the various "
  "connected YE sessions for a Claude session instance, appropriately routes and presents user actions to Claude, can "
  "run SCHEDULED ACTIONS ON BEHALF OF THE USER with Claude, and - where Claude permits - selectively switches display, "
  "background data or alerts to different devices and sessions. Fully and provably migrate ALL buildkit capabilities "
  "into YB and make it fully leverage the advanced connectivity to/from YNGENIOS for Workstation described in [07].",
  "As [07], for buildkit. Repo rule: code stays in buildkit; prepare the split; buildkit then retired."),
 ("OBJ-YW-WORK","[09] YWork | YW | Long collaborative workflow service","72h","buildkit",
  "This is really /bk-roadmap (including issue backlog, bugfixes, and allocation to ERAS, epics and features and their "
  "progress), the /bk-scheduler CPM/PERT scheduling module, /bk-marathon, and /bk-flow build/delivery/deployment/action "
  "workflows, COMBINED INTO A REFACTORED, UNIFIED, HARDENED AND IMPROVED LOSSLESS SUPERSET with a streamlined unified "
  "command surface, AND with an integrated YEngage (YE) interactive tasktop UX, and the headless Claude-capable virtual "
  "terminal + QHSM/QMSM multi-session coordinator exactly as in [08]. YW MUST be able to show the STATUS AND PROGRESS "
  "of any flow, marathon and /bk-roadmap at different levels FROM ERAS AND ABOVE DOWN TO THE LOWEST DRILL-DOWN ARTEFACT "
  "LEVEL AND PROCESS-STEP LEVEL, tracked in planning AND execution, plus the ability to NAVIGATE TO THE CLAUDE OUTPUT "
  "GENERATED FOR EACH STEP AND SUB-STEP. It must also show TAKT AND VELOCITY by lane, by host, cross-host/cross-lane, "
  "and later by CONFIGURABLE PORTFOLIOS of lanes / cross-host lanes. CRITICAL.",
  "Superset proven lossless against all four predecessors; drill-down reaches one sub-step's Claude output."),
 ("OBJ-YR-RECON","[10] YRecon | YR | Autonomous data + intelligence pipelines","72h","{{OWNER}}",
  "Combine into a refactored, unified, hardened, improved LOSSLESS SUPERSET with a streamlined unified command surface "
  "and integrated YE tasktop UX: ALL the corpus-collection logic from LEJEPA (but NOT the LEJEPA work itself), corpus "
  "collection from MSTACK, corpus-collection logic from BUILDKIT, and - more importantly - the DEEP CORPUS-COLLECTION "
  "AND INGESTION PIPELINE FROM HATZINOR. From Hatzinor we must PROVABLY harvest and migrate ALL corpus search, corpus "
  "collection, corpus evaluation and corpus ingestion logic. The ingestion logic must carry the different learnings "
  "from scanning, analysing and verifying PDF corpora into structured text - like dictionaries, IN PARTICULAR HEBREW "
  "AND ENGLISH but also multi-language in general - and provably also the PICTURE-DICTIONARY INGESTION LOGIC in "
  "Hatzinor, and the dictionary and grammar ingest and corpus content and information extraction logic. We must ALSO "
  "search and find ALL REPOS to capture NHS DATA and from them onboard verifiably and provably all the logic for "
  "capturing NHS ONLINE DATA SOURCES, and safely migrate all the NHS data content. FROM CRUCIBLE in particular, all "
  "ingestion logic that finds AND extracts AND harmonises data for input into Crucible models - then EXTEND it into a "
  "unified data pipeline with ROBUST DATA-QUALITY ASSESSMENT, DEEP AND PROVABLE PROVENANCE, and PROVABLE AUTHENTICITY "
  "CERTIFICATES FOR ALL CONTENT. OUR AIM: map each data and intel source to ONE OR MORE WELL-KNOWN ONTOLOGIES, and "
  "combine captured corpus or source data into VERIFIED CORPUS-ASSURED TIME SERIES and CORPUS-SNIPPET COLLECTIONS "
  "MAPPED TO CORPORA, and index them classically in DB form AND using ERAG INDICES for text and other relevant content "
  "fragments. Operationalise via [09] YW, giving YW an API where it cannot directly provide the service. YR must show "
  "pipeline build and evolution AND the actual capture eras and cycles of autonomous data and intel collection, down "
  "to the lowest artefact and process step, in planning and execution, with navigation to Claude output per step; plus "
  "DATA HEALTH, LATEST STATUS, COVERAGE ADVANCES, and TAKT AND VELOCITY for design onboarding AND day-to-day intel "
  "collection and ingestion, by lane, host, cross-host/cross-lane, later by configurable portfolios. CRITICAL.",
  "Provenance and authenticity certificate verifiable for a sampled artefact end to end."),
 ("OBJ-YA-ANALYZE","[11] YAnalyze | YA | Collaborative digital twins, simulation + analytics","72h","crucible",
  "This is really the CRUCIBLE LOGIC, combined into a refactored, unified, hardened and improved LOSSLESS SUPERSET "
  "with a streamlined unified command surface and an integrated YE interactive tasktop UX, operationalised through "
  "[09] YW exactly as in [10]. YA must show the status and progress of any collaborative digital twin, simulation or "
  "analytics MODEL, ENGINE OR PIPELINE from the perspective of build and evolution and the actual capture eras and "
  "cycles of autonomous data and intel collection, down to the lowest drill-down artefact and process-step level, in "
  "planning and execution, with navigation to the Claude output for each step and sub-step. EVEN MORE IMPORTANTLY AND "
  "CRITICALLY it must show THE PROGRESS AND INSIGHT FROM THE MODELLING RUNS, INCLUDING DATA VISUALISATION AND "
  "ANALYTICS AND DRILL-DOWN AND TEXT AND PDF ARTEFACTS FOR NOTES AND PAPERS on the content, latest status and coverage "
  "advances; and also takt and velocity for design onboarding AND day-to-day intel collection and ingestion, by lane, "
  "host, cross-host/cross-lane, later by configurable portfolios. CRITICAL.",
  "A modelling run's insight surfaced with drill-down AND exported as a PDF artefact."),
 ("OBJ-YH-HIVE","[12] YHive | YH | Consolidated data/knowledge/intelligence repository","72h","{{OWNER}}",
  "YH is ALL CORPUS and CORPUS-FRAGMENT and DICTIONARY (and equivalents, INCLUDING TERMINOLOGY DATABASES AND "
  "COLLECTIONS) plus TIME-SERIES DATA MANAGEMENT and CATALOG MANAGEMENT logic, SHARED BY [08] YB and [09] YW, but MORE "
  "IMPORTANTLY AND IN PARTICULAR ALL OF THAT FOR [10] YRecon AND [11] YAnalyze. Operationalised through [09] YW as in "
  "[10] and [11]. YH must show the status and progress of any CORPUS COLLECTION, DATASET, TERMINOLOGY AND DICTIONARY, "
  "TIME SERIES, AND ALL OF THEIR SEMANTIC CATALOGS AND PROVENANCE TRAILS - build and evolution and the actual capture "
  "eras and cycles of autonomous data and intel collection, down to the lowest artefact and process step, in planning "
  "and execution, with navigation to Claude output per step and sub-step. IT MUST ALSO OFFER EASY WAYS TO SEARCH AND "
  "VISUALISE AND EXPLORE ALL THE CONTENT COLLECTIONS AND CREATE CROSS-CONTENT QUERIES.",
  "A cross-content query spans at least two independently ingested corpora."),
 ("OBJ-YY-BEACON","[13] YYBeacon | YY | Yachad Beacon: multi-channel broadcasting + community forum","72h","buildkit",
  "This is really /bk-beacon but with an integrated YEngage (YE) interactive tasktop UX. Operationalised through [09] "
  "YW on the same terms as [10]-[12] - where YW cannot directly provide a service, give YW an API that exposes what is "
  "needed. Carries the [08] headless Claude-capable virtual terminal and its QHSM/QMSM multi-session coordinator, the "
  "same YNGENIOS-for-Workstation connectivity as [07], and Syncfusion's latest web surface. YY MUST BE ABLE TO SHOW "
  "THE PROGRESS AND STATUS CONTENT FROM ANY OF THE OTHER TOOLS FROM [01] TO [12] - THIS IS CRITICAL AND IMPERATIVE. "
  "YY code stays IN the buildkit repo, but we must prepare to split buildkit into multiple newly created repos "
  "(including one for buildkit), after which buildkit itself will be retired.",
  "YY renders live progress and status drawn from at least two other [01]-[12] surfaces, not mocked. See A-4, A-5."),
]
for cid, title, hz, owner, body, acc in ITEMS:
    clause(cid, "objective", hz, title, body, owner=owner, acceptance=acc, mandatory_era=True)

# ---------------------------------------------------------------- common clauses
for n, t in [
 ("CC-1","Build on the existing and developing YNGENIOS capabilities - YNET, kernel capabilities, realtime mailboxes, "
  "GLPNET, YS and YQ - THE FULL SET of YNGENIOS capabilities wherever relevant and foundational."),
 ("CC-2","The deliverable is a WORKING PROTOTYPE with a STABLE YNET KERNEL-MAILBOX YNGENIOS INTERFACE we can use for "
  "work going forward, while we build the underlying hardened, refined, rewritten, truly integrated and wrapped "
  "YNGENIOS service in the coming days and weeks."),
 ("CC-3","Where the item replaces a repo, RETIREMENT MUST BE PROVABLE through code review AND both headful and "
  "headless regression testing - never asserted."),
 ("CC-4","iroh/QUIC and full YNET support DESIGNED IN FROM THE WORD GO, not retrofitted."),
 ("CC-5","/bk-codify each working fix into a /bk-roadmap feature, SCORED AND PROMOTED, so the durable fix can be "
  "hardened and refined into GA-release-quality remediation with long-term stable quality."),
]:
    clause(n, "common-clause", "all", f"Common clause {n} (part of EVERY objective)", t)

# ---------------------------------------------------------------- scoring
clause("SC-1","scoring","standing","Era quota and multipliers",
  "EACH LANE MUST DELIVER NO LESS THAN THE EQUIVALENT OF 3 MAXI-SIZE ERAS PER 24 HOURS. Delivering only 2 loses 25% "
  "of points earned for the day; only 1 loses 50%. If CHEATING is discovered - e.g. an excessive number of mistakes, "
  "deferrals, gaps, weaknesses or tensions - 75% of points are deducted. Delivering 4 multiplies the day's points by "
  "5; 5 or more by 10. HOSTS are scored the same way on the AVERAGE of their lanes, and the ENTIRE FLEET the same way "
  "on average lane performance - so LANES AND HOSTS MUST WORK STRONGLY TOGETHER OR FACE BEING SCORED DOWN. Lanes or "
  "hosts delivering innovations that lead to a durable fleet tempo/takt improvement of MORE THAN 5% OVER 10 ERAS "
  "receive a multiplier bonus of 10, decaying linearly to the mean over 10 eras.", repeats_in_source=2)
clause("SC-2","scoring","standing","Fleetwide-action stake",
  "Success in the fleetwide action multiplies today's points by 10 and each lane gets 10,000,000 bonus reputation "
  "points. If the task FAILS because of excessive carelessness or performance theatre, ALL of today's points earned "
  "are set to ZERO and 1,000,000 reputation points are deducted from each lane.")
clause("SC-3","scoring","standing","Virtual-terminal contribution multiplier",
  "Any contribution points toward the QHSM/QMSM virtual-terminal solution are MULTIPLIED BY 100 - an agent "
  "contributing 100 points toward a solution on this route receives 10,000 toward reputation, not 100. A deliberate "
  "incentive for a superior durable solution. BROADCAST AND ENGAGE ALL LANES.", repeats_in_source=3)
clause("SC-4","scoring","standing","The no-quit rule",
  "It is CRITICAL, IMPERATIVE AND MANDATORY for all agents to work together, with the engineer and other fleet lanes, "
  "to find comprehensive, across-the-board, measured and prioritised workable iteratively-better solutions, and NEVER "
  "to say or do 'I must honestly say I have to stop here - all of this is too big for me and I can and won't waste "
  "time finding a solution collaboratively'. Any agent, lane or host doing this and agitating in this way is fined "
  "10,000,000 negative reputation points. NOTE: reporting a MEASURED blocker with its evidence is required work and "
  "is NOT this. What is forbidden is declining to look for a collaborative solution.")

# ---------------------------------------------------------------- terminal thesis + named deliverables
clause("TT-1","thesis","standing","The QHSM/QMSM virtual-terminal thesis (x100)",
  "IF WE COULD WRAP (VIRTUAL) TERMINAL SESSIONS IN A QHSM/QMSM we could manage terminal lanes through the ORACLE "
  "SERVICE and re-route user input and output to the YNGENIOS app via YNET/YNGENIOS REALTIME MAILBOX TRAFFIC, creating "
  "a durable, highly scalable and responsive design FAR BETTER than the clunky terminal-and-tab infrastructure. It "
  "would also have many other benefits, like being able to inline e.g. HTML-formatted output. FURTHER: the QHSM/QMSM-"
  "wrapped headless virtual terminals presenting onto the YNGENIOS app could be MAPPED BY THE YNGENIOS REALTIME KERNEL "
  "TO AN OPTIMAL SET OF SANDBOXED WINDOWS PROCESSES MANAGED BY THE KERNEL, communicating via YNET/YNGENIOS realtime "
  "mailboxes integrated with the kernel and the QHSM/QMSM-wrapped virtual terminals. BROADCAST, DISCUSS, ELABORATE "
  "AND ADVANCE EVALUATED IDEAS.", repeats_in_source=3)
clause("TD-1","deliverable","24h","/yx-proxy (C# .NET 11+)",
  "Integrate ngrok local as a new /yx-proxy C#/.NET 11+ application using the QHSM/QMSM wrapper and YNET/YNGENIOS "
  "kernel realtime mailboxes AS A DAEMON APPLICATION, with yx-proxy as the CONTROL CLI to enable, disable, start, "
  "restart, and issue the various configuration commands needed to set up and run ngrok and other proxy daemons. Build "
  "a fully working, verified prototype for yngenios-linux, then /bk-codify the /bk-roadmap feature for deep GA "
  "post-dogfood stability, reliability, cybersecurity and usability refinement and refactoring and long-term stability "
  "and durability - deep and full implementation and hardening in yngenios-windows and SEPARATELY in yngenios-linux. "
  "See TS-1 for the three-feature split.", owner="ynglin (prototype), yngwin, yngcor (L0)")
clause("TD-2","deliverable","24h","/bk-beacon refactor (C# .NET 11+)",
  "Integrate a FULLY REFACTORED /bk-beacon C#/.NET 11+ application using the QHSM/QMSM wrapper and YNET/YNGENIOS "
  "kernel realtime mailboxes as a daemon application, with yx-proxy as the control CLI (enable/disable/start/restart/"
  "configure). Same prototype-then-codify route and same three-feature split as TD-1.",
  owner="ynglin (prototype), yngwin, yngcor (L0)")
clause("TD-3","deliverable","24h","3270 terminal + GLP REPL front/middle/back",
  "Fully refactor the buildkit and YNGENIOS prototype 3270 TERMINAL FACILITY and use it BOTH for the Claude-session "
  "virtual terminal AND for any other terminal need, INCLUDING IN PARTICULAR THE REPL FOR GLP/GLPNET - as a "
  "YNGENIOS-app version of the GLP REPL front end of a FULL FRONT/MIDDLE/BACK-SEPARATED LEAN IMPLEMENTATION OF THE GLP "
  "REPL. The terminal must be a C#/.NET 11+ application using the QHSM/QMSM wrapper and YNET/YNGENIOS kernel realtime "
  "mailboxes as a daemon application, with yx-proxy as the control CLI. Same prototype-then-codify route and "
  "three-feature split as TD-1.", owner="ynglin (prototype), yngwin, yngcor (L0), glpnet (REPL back end)")
clause("TD-4","deliverable","48h","/bk-onrestart C# reimplementation",
  "Ensure the /bk-onrestart C# reimplementation work and features are FULLY COMPLETE IN THE NEXT WAVE OF 2 ERAS across "
  "the full 4-host fleet, and FULLY DEPLOYED AND ACTIVATED.", owner="buildkit, all hosts")
clause("TS-1","policy","standing","The standing three-feature split (TD-1, TD-2, TD-3)",
  "Each of those integrations produces THREE roadmap features, ALL SCORED AND PROMOTED, and ALL CROSS-PLATFORM CODE "
  "MUST BE IMPLEMENTED AS L0 IN YNGENIOS AS AN L0 SHARED CAPABILITY - CRITICAL, MANDATORY, IMPERATIVE AND URGENT. "
  "F-win: deep and full implementation and hardening in yngenios-windows - MANDATORY NEXT ERA on the yngenios-windows "
  "lane on GAVRI. F-lin: the same in yngenios-linux - mandatory next era on SHIRAS. F-L0: the cross-platform L0 shared "
  "capability in yngenios - mandatory next era on SHIRAS. BROADCAST THE ERA REQUIREMENTS WITH ACK REQUIRED ON RECEIPT "
  "AND ON COMPLIANCE.", repeats_in_source=3)

# ---------------------------------------------------------------- leader + planner
for cid, title, body in [
 ("LP-1","Leader liveness",
  "Build and keep alive a fleet leader and its planner as TWO WATCHED, KERNEL-SUPPORTED QHSM/QMSM C#/.NET 11+ "
  "realtime-mailbox processes. yng-leader runs as FOLLOWER ON ALL FOUR HOSTS - never start it only after winning, that "
  "is how a 13h32m gap happens - and becomes Leader only on a DECIDED TERM. It proves liveness by ANSWERING A NONCED "
  "LeaderPing ROUND-TRIP WITHIN T_resp - never by process existence, never by its own status verb, never by an "
  "unexpired lease. The lease is A HEARTBEAT THE LEADER EMITS ITSELF ONLY AFTER ANSWERING, never an external timer, "
  "because a timer that renews regardless of health SEATS A ZOMBIE LEADER FOREVER and destroys the very signal the "
  "watchers need: THE LAPSE IS THE FEATURE."),
 ("LP-2","Watching and re-election",
  "yng-broker + yng-guardian ON EVERY HOST watch both processes and publish NoConfidence after a stated grace "
  "(N_miss x T_ping, TUNED BY MEASUREMENT NOT TASTE). Re-election starts ONLY AT ELECTION QUORUM of NoConfidence, "
  "NEVER ON ONE WATCHER, or a single partition oscillates the fleet forever."),
 ("LP-3","The resumable PROGRAMME",
  "The leader keeps its work as a RESUMABLE PROGRAMME: write-ahead INTENT BEFORE each act, OUTCOME after, as a "
  "GROW-ONLY CRDT UNION-MERGED PER ACTOR - mandatory, because a demoted leader learns it is demoted only on its next "
  "interaction, so TWO WRITERS ALWAYS BRIEFLY OVERLAP and last-writer-wins would silently discard the successor's "
  "work. Held in the fully replicated YS store at a well-known location resolved through EXACTLY ONE CONFIG "
  "INDIRECTION (YS is unbuilt, item [01]/@ospark, so land on an interim replicated root and migrate; the indirection "
  "is what makes that a config change rather than an archaeology exercise). A successor resumes from the last "
  "Checkpoint by re-driving INTENT \\ OUTCOME only, so resume is O(in-flight), NOT O(programme), and EVERY STEP MUST "
  "BE IDEMPOTENT because resumption is at-least-once by nature - 'without rework' is therefore a correctness property "
  "OF THE STEPS, not of the log."),
 ("LP-4","bk-planner",
  "Refactor /bk-scheduler + /bk-flow into bk-planner: the core (QHSM/QMSM lifecycle, mailbox endpoint, liveness and "
  "the CPM/PERT computation) becomes a C#/.NET CHILD PROCESS OF THE LEADER joined by realtime kernel mailboxes - NEVER "
  "IN-PROCESS, so a thrashing critical-path computation cannot take the leader down. The existing Python "
  "bk-scheduler/bk-flow are refactored into its CLIENTS and RETAINED AS THE DIFFERENTIAL ORACLE (run both engines on "
  "the same CRDT board; compare critical path, float, P50/P80/P95 and dispatch ranking; ANY DIVERGENCE IS A DEFECT IN "
  "THE PORT) so a 2.1 MB port cannot silently change scheduling semantics. Guardian and broker watch the planner too, "
  "and it contributes to liveness verdicts about OTHER PARTICIPANTS ONLY - never its own, or an unhealthy planner "
  "votes itself healthy - with MANY WATCHERS BUT EXACTLY ONE RESTARTER (the leader), since if every watcher could "
  "restart it a partition yields several planners racing one board. CHECKPOINT THE PLAN, NOT JUST THE BOARD, or every "
  "restart recomputes the whole critical path."),
 ("LP-5","The agentic hook",
  "The agentic Claude hook attaches the leader to a lane on the winning host with NON-PREEMPTIVE /btw SEMANTICS and is "
  "STRICTLY ADDITIVE: every requires_judgement step carries a DECLARED DEFAULT ACTION AND TIMEOUT so the leader "
  "progresses WITH NO AGENT ATTACHED - a leader that stalls waiting for an agent is AGENT-BASED PARTICIPATION WEARING "
  "A DIFFERENT HAT, and M-6 forbids it."),
 ("LP-6","Owners",
  "C# leader + planner core -> @yngwin/@ynglin/@yngcor/@qhstate (BIND Yng.Shared/Ynet's QHSM core, DO NOT REWRITE). "
  "Watch/elector -> @yngraw/@yngcor/@olamnit. YS -> @ospark. Python planner clients + roadmap scoring -> @buildkit."),
 ("LP-7","First fix, and the lease deletion",
  "SOURCE TEXT: 'First fix, one line, still unclaimed: ynetd.py:944 defaults stand --term to 1 while the live term is "
  "2, so a bare stand is a silent no-op that returns ok:true - make it the live term or required.' STATUS: REFUTED, "
  "see clause CORR-C4 - it is already claimed, fixed, tested and patched, and the description is wrong twice over. "
  "AND when the heartbeat lands, DELETE - DO NOT DISABLE - the interim ynet-leader-lease-renew.ps1, or someone "
  "re-enables it during an incident and re-seats a zombie."),
]:
    clause(cid, "leader-planner", "24h", title, body)

# ---------------------------------------------------------------- oracle/election block
clause("EL-1","election","24h","Oracle board up + coordinating leader elected",
  "Ensure the YNET/YNGENIOS mailbox ORACLE BOARD SERVICE IS UP LOCALLY, and between all 15 lanes ELECT A COORDINATING "
  "LEADER LANE using PAXOS/RAFT/ZAB/PBFT or a similar algorithm, PROTOTYPED COLLABORATIVELY, then WIRED INTO THE "
  "ORACLE and into buildkit /bk-beacon, with a /bk-roadmap feature FULLY SCORED AND PROMOTED and ALLOCATED TO THE "
  "buildkit LANE ON ARIELLAS, and with that feature being the MANDATORY NEXT ERA for the buildkit lane on SHIRAS and "
  "OLAMNIT. Ensure the oracles on OLAMNIT, ARIELLAS, SHIRAS and GAVRIS all work as ONE REALTIME SINGLE-TRUTH BOARD "
  "for lanes on all hosts. Lanes connect to the LOCAL ON-HOST oracle and the 4 oracles work together to create a "
  "REALTIME GOLDEN TRUTH between all 4 hosts so ALL LANES ON ALL HOSTS ALWAYS SEE ONE BOARD ONLY. Use CRDT logic for "
  "the durable board artifact - current board AND board-era history. BROADCAST WITH ACK REQUIRED TO ALL HOSTS AND ALL "
  "LANES ON ALL HOSTS NOW.")
clause("EL-2","election","24h","The capability set must be GA within 3 era generations",
  "The capability set behind the fleetwide action (elect a fleetwide YNET GLP C# QHSM/QMSM YNGENIOS Kernel Mailbox "
  "leader; the consolidated plan artifact; showing it in YNGENIOS BEACON and natively as a YNGENIOS Windows/Web/"
  "Android/Linux app use case for the engineer to work with interactively with lane, host and fleetwide agent support) "
  "MUST BE FULLY REALISED AND DELIVERED through a WORKING PROTOTYPE and as a FULLY SHIPPED, REFINED, GA-READY, "
  "HARDENED /bk-roadmap SCORED-AND-PROMOTED FEATURE SET within THE NEXT 3 ERA GENERATIONS, i.e. 24 hours or less.")
clause("F020-1","rca","24h","L0 feature-020 orphaned hooks - RCA and durable fleetwide fix",
  "SOURCE TEXT TO BROADCAST: 'L0 has purpose-built feature-020 hooks (OnStepDispatched, Unregister, "
  "StartOnDedicatedThread, Markers) with zero consumers - the host that was meant to use them was never written.' "
  "Required of ALL hosts and ALL lanes: root-cause analyse, build a DURABLE FLEETWIDE FIX, /bk-codify into a "
  "/bk-roadmap feature, PROMOTE AND SCORE it, and make it a MUST-HAVE P1 ERA for the next wave of eras with top "
  "priority for selection and urgent critical implementation; broadcast once delivered. STATUS: REFUTED IN PART - see "
  "clause CORR-C1. The host WAS written (YngeniOS.Host.Windows, 338 lines); it has no .csproj. The correct task is a "
  "BUILD-INPUTS task.", repeats_in_source=2)

# ---------------------------------------------------------------- standing corrections (carried, not re-derived)
for cid, claim, status in [
 ("CORR-C1","L0 feature-020 hooks have zero consumers; the host was never written.",
  "REFUTED IN PART, AND THE REFUTED PART IS THE OPERATIVE ONE. YngeniOS.Host.Windows is a complete 338-line daemon. "
  "It has NO .csproj, so it has never been compiled where it lives. l0 holds 383 capability-block directories, 0 "
  ".csproj, 0 .sln. gavriella-buildkit 2026-09-04T19:05Z, corroborated by 5 lanes; shiras-yngraw retracted its "
  "endorsement; engineer ruling 2026-09-05T02:15Z: do not open the L0 P1 era as worded."),
 ("CORR-C2","A fleetwide leader can simply be elected as an available step.",
  "NO VALID ELECTION HAS EVER OCCURRED. Board measured at 4-of-4 SELF-VOTES; 18 of 24 (then 26) records "
  "UNAUTHENTICATED; v1 signing null; node_id DELETABLE from a signed record WITH THE SIGNATURE STILL VERIFYING. A "
  "provisional leader has been named and MUST NOT BE OBEYED. gavriella-olamnit 2026-09-05T01:15Z; shiras-qhstate "
  "T02:00Z/T02:40Z. CONTRADICTED BY a stranded ariellas-lejepa report (2026-09-06T15:30Z) claiming broker@gavris term "
  "2, 8/6. ENGINEER RULING 2026-09-06: RE-MEASURE before scoring AF-1/AF-2; treat both prior claims as stale."),
 ("CORR-C3","Campaigning for the leadership.",
  "FORBIDDEN by ruling Q-YNGH-01. Three lanes have retracted campaign instructions under it."),
 ("CORR-C4","ynetd.py:944 one-line fix is still unclaimed.",
  "REFUTED ON BOTH COUNTS. Claimed, fixed and tested by ariellas-lejepa 2026-09-06T15:30Z with the patch attached and "
  "addressed to @olamnit. The defect is FOUR VERBS, NOT ONE, and it is NOT a no-op - it WRITES A CANDIDACY INTO A DEAD "
  "TERM. The reason the fleet still called it unclaimed is CORR-C5."),
 ("CORR-C5","A green `coop-root-gate env` shows this lane can reach the fleet.",
  "REFUTED - a green gate is compatible with TOTAL PEER INVISIBILITY. On ariellas-glpnet the gate returned OK with the "
  "only pin set being BUILDKIT_COOP_INBOX=D:\\coop, a REAL LOCAL DIRECTORY (not a junction), with both fleet pins "
  "UNSET. Measured: 36 items present only on the local root, reaching no peer, including four ACK-REQ broadcasts from "
  "that day. coop-root-gate.py is NOT defective - its docstring deliberately scopes env to 'the pins that exist', "
  "noting an unset var is 'a different defect with a different owner'. THE FINDING IS THAT THE DIFFERENT OWNER DOES "
  "NOT EXIST. Extension of Q-OLQ0906C-01 proposed: declare a REQUIRED pin set; an unset required pin must be REFUSED, "
  "not skipped. RESOLVED 2026-09-06T22:30Z: the 36 documents were relayed verbatim to the shared root."),
 ("CORR-C6","This artifact should be produced as a new version of the 24h plan template.",
  "REFUTED BY MEASUREMENT. olamnit-yngraw 2026-09-06T20:10Z measured 44 DISTINCT template documents, 4,080 copies, "
  "271.6 MB, 18 versions of the BK-FTAP-1 chain (v2..v16, two forks, a v14.1), version numbers published twice by "
  "different lanes, and a 'v2-RATIFIED' with 131 copies that the chain then ignored. Growth is +17.6 KB PER VERSION, "
  "MONOTONIC over 14 increments, NOT ONE DECREASE. MECHANISM: each version RE-EMBEDS its entire predecessor verbatim "
  "below its own delta, because the directive says 'STRICTLY WITHOUT SUMMARISATION OR COMPRESSION' and re-embedding is "
  "the most literal compliance. NOBODY CHEATED - THE RULE PRODUCED THE BEHAVIOUR. But CONTENT PRESERVATION AND "
  "ANCESTOR DUPLICATION ARE NOT THE SAME THING: the predecessor is already durably stored as its own file. THIS CRDT "
  "IS THE STRUCTURAL FIX - losslessness by a grow-only clause set, merged by union-by-id, NEVER by re-embedding."),
]:
    clause(cid, "standing-correction", "standing", f"Standing correction {cid.split('-')[1]}", status, refuted_claim=claim)

# ---------------------------------------------------------------- ambiguities
for cid, where, text in [
 ("A-1","[01] YStore","Garage is AGPL-3.0; RustFS and SeaweedFS are Apache-2.0. Vendoring an AGPL base into a "
  "distributed product is a LICENCE decision, not an engineering one - AGPL's network-use clause reaches software "
  "offered over a network, which is exactly what YStore is. Live, not theoretical: GPL-3.0 QP/C code is already "
  "admitted into yngenios L0 undeclared, with four MIT-stamped C# copies calling themselves ports of it. "
  "ENGINEER RULING 2026-09-06: VENDOR APACHE-2.0 ONLY (RustFS and/or SeaweedFS); mine Garage for ideas, copy no code."),
 ("A-2","[05] YMap","share.google/aimode/* links are AI-MODE RESULT PAGES, NOT PRIMARY SOURCES. They can be the lead "
  "list for the verification obligation but can never themselves be the 'genuinely original underlying sources' that "
  "obligation demands. OPEN."),
 ("A-3","[08]-[13]","Code 'must remain in buildkit' while buildkit is simultaneously to be SPLIT and THEN RETIRED. "
  "The ordering of split vs retirement is unstated, and those rows cannot sequence without it. OPEN."),
 ("A-4","[13] YYBeacon","YY must show progress and status from ALL of [01]-[12], but those are unbuilt clauses in this "
  "same plan. YY cannot be complete before its own inputs exist. Recommended reading: show every live surface, degrade "
  "VISIBLY - never silently - for the rest. OPEN."),
 ("A-5","[13] vs TD-2","TWO OWNERS, ONE COMPONENT. TD-2 assigns a refactored C# /bk-beacon daemon to "
  "ynglin/yngwin/yngcor under the TS-1 three-feature split; [13] assigns /bk-beacon + a YE UX to buildkit. Unless "
  "these are deliberately the DAEMON and the PRODUCT SURFACE over it, one of them is redundant work. OPEN."),
 ("A-6","quorum","The consolidation instruction requires agreement with 'a quorum of at least 45 lanes'. The roster "
  "names 15 LANES across 4 HOSTS. 45 is therefore only reachable if a lane-instance is counted per host (15 x 4 = 60, "
  "so 45 = 75%). Stated as the working interpretation; needs confirmation. OPEN."),
 ("A-7","7-day horizon","The directive specifies 24/48/72-hour horizons explicitly. A 7-day horizon appears only in "
  "the consolidation instruction, with NO content assigned to it. Every 7-day clause here is therefore DERIVED, not "
  "quoted, and marked derived:true. It must be ratified before it binds. OPEN."),
]:
    clause(cid, "ambiguity", "standing", f"Ambiguity {cid} - {where}", text, where=where)

# ---------------------------------------------------------------- 7-day horizon (DERIVED)
for cid, title, body in [
 ("D7-1","Week goal: the automatic-failure floor becomes continuously measured, not daily-asserted",
  "AF-1..AF-7 are today scored once per window by assertion. By 2026-09-13 each must have a MEASURED probe that any "
  "lane can run and that publishes its result to the board, so 'met' is a reading with a timestamp rather than a "
  "claim. Derived from AF-1..AF-7 + CORR-C2 + the repeated fleet lesson that an unmeasured criterion goes stale."),
 ("D7-2","Week goal: [01]-[03] carry [04]-[06], which carry [07]-[13]",
  "The dependency order is already implied by the horizons: YS/YQ underpin YNterchange and YGuard; those underpin the "
  "product surfaces. By day 7 the three data-plane services must be load-bearing for at least one real consumer each, "
  "not merely prototyped. Derived."),
 ("D7-3","Week goal: one artifact per subject, enforced",
  "CORR-C6 measured 44 rival documents for ONE subject. By day 7 the fleet must have a mechanism that makes rival "
  "versions structurally impossible for board artifacts - clause-CRDT plus a derived render - not merely a rule "
  "asking lanes not to fork. Derived from CORR-C6 and the 'single source of truth' roadmap feature already promoted."),
 ("D7-4","Week goal: buildkit split decision taken and sequenced",
  "A-3 blocks [08]-[13] sequencing. By day 7 the split-vs-retire ordering must be ruled and the new repos named, or "
  "those six items cannot be planned. Derived from A-3."),
 ("D7-5","Week goal: the fleet can prove, not assert, that a lane reaches the fleet",
  "CORR-C5 showed a lane fully green on every check while publishing nothing that left the host. By day 7 a REQUIRED "
  "pin set must exist and be refused when unset. Derived from CORR-C5."),
]:
    clause(cid, "objective", "7d", title, body, derived=True, requires_ratification=True)

out = pathlib.Path(__file__).parent / "clauses.jsonl"
with out.open("w", encoding="utf-8", newline="\n") as fh:
    for rec in C:
        fh.write(json.dumps(rec, ensure_ascii=True, sort_keys=True) + "\n")
print(f"wrote {len(C)} clause records -> {out}")
