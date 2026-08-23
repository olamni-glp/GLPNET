<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature Specification: bk-onrestart per-host configurable auto-installable fleet resume

**Feature Branch**: `085-onrestart-fleet-resume`
**Created**: 2026-08-23
**Status**: Draft
**Input**: User description: "bk-onrestart per-host configurable auto-installable fleet resume"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The host describes itself; nobody edits a script (Priority: P1)

An engineer sets up post-reboot resume on a host by writing down what that host is — which
repo lanes live on it, and how its windows should be arranged — and nothing else. Adding a
repo, removing a repo, or changing the window arrangement is a change to that description,
never a change to a shared program. Two hosts that want different arrangements both get what
they want without either one being "the exception".

**Why this priority**: This is the whole point of the feature. Today the repo set is a fixed
list inside the launcher and the window arrangement is a flag whose default is the value
almost nobody wants, so every host reproduces the setup by hand and gets it wrong in its own
way. Until the host describes itself, every other improvement is still tribal knowledge.

**Independent Test**: Take a host with no prior setup, write only its description, run the
resume, and confirm every lane on that host comes back with the arrangement that host asked
for — with no flag supplied and no shared file edited. Delivers a reproducible resume on its
own, without any of the later stories.

**Acceptance Scenarios**:

1. **Given** a host whose description lists its repo lanes and declares a single-group
   arrangement, **When** the engineer runs the resume with no options, **Then** every listed
   lane is resumed and they appear in one group.
2. **Given** a host whose description declares a multi-group arrangement with named groupings,
   **When** the engineer runs the resume with no options, **Then** the lanes appear in exactly
   those groupings.
3. **Given** two hosts with different declared arrangements, **When** each runs the resume with
   no options, **Then** each gets its own declared arrangement, and neither needed to remember
   or supply a flag.
4. **Given** a host description that lists a repo whose path is not present on this host,
   **When** the resume runs, **Then** that lane is refused by name with the reason, and every
   other lane still resumes.
5. **Given** no host description exists yet, **When** the resume runs, **Then** it refuses with
   an explicit "this host has not been described" message and resumes nothing — it never falls
   back to a built-in list belonging to a different host.

---

### User Story 2 - A resume that is honest about what it actually started (Priority: P2)

After a resume, the engineer is told, per lane, what was requested and what is actually
running. A run in which windows opened but no sessions came up is reported as a failure at the
time it happens, not discovered later when the context is visibly gone.

**Why this priority**: This removes a known, measured, silent failure mode — a launch that
opens N tabs and starts zero sessions while cheerfully reporting success. It is second only to
self-description because a reproducible resume that lies about its outcome is still not
trustworthy, but it has no value until there is something to report on.

**Independent Test**: Force a launch that opens windows without starting sessions, run the
resume, and confirm the run reports failure and names every lane that did not come up.
Verifiable on its own against the existing launcher behaviour.

**Acceptance Scenarios**:

1. **Given** a resume of N lanes where all N come up, **When** the run finishes, **Then** the
   receipt records N requested and N started, and the run reports success.
2. **Given** a resume where windows open but no session starts, **When** the run finishes,
   **Then** the receipt records N requested and 0 started, names every missing lane, and the
   run reports failure.
3. **Given** a resume where some lanes were refused before launch (missing path, missing
   session history), **When** the run finishes, **Then** the receipt distinguishes *refused
   before launch* from *launched but not started*, and gives the reason for each refusal.
4. **Given** any completed resume, **When** the engineer looks for the outcome later, **Then**
   the receipt is retrievable after the terminal output is gone.

---

### User Story 3 - An absent share says "I cannot see it", never "it is empty" (Priority: P2)

When a resume runs while a required shared location is unavailable, every lane it starts is
told so explicitly, so no tool downstream reports an empty result that is really an invisible
one.

**Why this priority**: The resume is the only component that knows whether the shares were
ready, so it is the only place this can be established. Three separate incidents in one month
came from a tool silently substituting a stale local location and answering every query with
plausible wrong data. Equal in priority to the receipt: both convert a silent wrong answer
into a loud one.

**Independent Test**: Run the resume with a required share disconnected and confirm the run
refuses to characterise anything as empty, states which shares are invisible, and passes that
state to the lanes it starts. Testable independently of the receipt and of self-description.

**Acceptance Scenarios**:

1. **Given** a host description that marks a shared location as required and that share is
   unavailable, **When** the resume runs, **Then** it states that the share cannot be seen,
   states the remedy, and does not describe any share-backed data as empty or absent.
2. **Given** the same conditions, **When** lanes are started anyway, **Then** each started lane
   is given the fact that the share was unavailable at start.
3. **Given** a required share that becomes available during the wait, **When** the wait ends,
   **Then** the resume proceeds normally with no warning.
4. **Given** an unavailable share, **When** the resume finishes, **Then** it never reports a
   finding about the state of another host's work.

---

### User Story 4 - Enrol and withdraw a lane from inside that lane (Priority: P3)

An engineer working inside a repo enrols it in that host's resume set, or withdraws it, with a
single action from where they already are. The host's description is updated for them; they do
not open it, find the right place, and match a format by hand.

**Why this priority**: Convenience and accuracy on top of a working self-describing host. It
removes the transcription errors that hand-editing a list invites, but the feature is already
useful without it.

**Independent Test**: From inside a repo not yet enrolled, enrol it; confirm it appears in the
host's description and is resumed on the next run. Withdraw it; confirm the reverse.

**Acceptance Scenarios**:

1. **Given** a repo not in the host's description, **When** the engineer enrols it from inside
   that repo, **Then** it appears in the description with the correct path and is resumed on
   the next run.
2. **Given** a repo already enrolled, **When** the engineer enrols it again, **Then** nothing
   changes and the engineer is told it was already enrolled.
3. **Given** an enrolled repo, **When** the engineer withdraws it from inside that repo,
   **Then** it is removed from the description and is not resumed on the next run.
4. **Given** a directory that is not a repo, **When** the engineer attempts to enrol it,
   **Then** the attempt is refused with the reason and the description is unchanged.

---

### User Story 5 - Automatic resume turned on or off in one repeatable action (Priority: P3)

An engineer turns automatic-resume-at-logon on for a host with one action, and off with one
action. Running either twice is safe and says so. A host that cannot grant the permission this
needs is told plainly rather than being left believing it is set up.

**Why this priority**: Automation is the convenience layer over a resume that already works
manually. It matters most because of its failure mode — an install that silently does nothing
is worse than no install — but that failure mode only exists once the rest works.

**Independent Test**: Turn automatic resume on, confirm it is registered; run the same action
again and confirm it reports no change; turn it off and confirm it is gone.

**Acceptance Scenarios**:

1. **Given** a host with no automatic resume, **When** the engineer turns it on, **Then** it is
   registered to fire at logon and the engineer is shown its registered state.
2. **Given** automatic resume already on, **When** the engineer turns it on again, **Then**
   nothing is changed and the engineer is told it was already on.
3. **Given** automatic resume on, **When** the engineer turns it off, **Then** it is removed,
   and turning it off again reports that it was already off.
4. **Given** a host where the engineer lacks permission to register an automatic trigger,
   **When** they turn it on, **Then** the attempt fails loudly with the reason and the host is
   not left reporting that automatic resume is on.
5. **Given** automatic resume is on, **When** the engineer asks to exercise it without
   rebooting, **Then** it runs the same path a logon would.

---

### Edge Cases

- A lane's repo path is present but it has no stored session history: the lane is refused by
  name with the reason and is **never** started as a blank session, because a blank session is
  indistinguishable from a successful resume until the missing context is noticed by hand.
- A lane's stored session history exists but is empty: treated the same as absent.
- A host description lists the same repo twice, or lists a path that has since moved: the
  duplicate or dead entry is reported and the rest of the run proceeds.
- A host description exists but cannot be read or understood: the run refuses entirely rather
  than resuming a partial or guessed set.
- The terminal the arrangement depends on is unavailable: the run refuses with the reason
  rather than resuming lanes into an arrangement nobody asked for.
- Two resumes are triggered close together (a manual run right after an automatic one): the
  second does not produce a duplicate set of lanes.
- A host is described but every one of its lanes is refused: the run reports failure, not an
  empty success.
- The engineer restricts a run to a subset of lanes and names one that does not exist on this
  host: the run refuses and lists the lanes it does know.
- A required share is unavailable *and* a lane is missing its session history: both are
  reported; neither masks the other.

## Requirements *(mandatory)*

### Functional Requirements

**Host self-description**

- **FR-001**: The system MUST resolve the set of repo lanes to resume for a host from a
  per-host description, and MUST NOT carry a built-in list of repos.
- **FR-002**: The system MUST resolve the window arrangement for a host from that same
  description, such that a resume invoked with no options produces the arrangement that host
  declared.
- **FR-003**: The description MUST be able to express both a single-group arrangement and a
  multi-group arrangement with named groupings of lanes.
- **FR-004**: The system MUST refuse to resume, with an explicit reason, on a host that has no
  description, rather than falling back to any default set of repos.
- **FR-005**: The system MUST refuse to resume, with an explicit reason, when a description
  exists but cannot be read or understood, rather than resuming a partial or inferred set.
- **FR-006**: The system MUST allow a run to be restricted to a named subset of the described
  lanes, and MUST refuse with the list of known lanes when a named lane is not described.

**Resuming in place**

- **FR-007**: The system MUST resume each lane so that it continues its most recent
  conversation mid-thread, and MUST NOT start a copy of it or a summarised form of it.
- **FR-008**: The system MUST verify, before launching a lane, that stored session history for
  that lane exists and is non-empty, and MUST refuse the lane by name with the reason when it
  does not.
- **FR-009**: The system MUST require each lane's repo path to be present, waiting a bounded
  time for it, and MUST refuse that lane by name when it does not appear.

**Started-versus-requested receipt**

- **FR-010**: The system MUST produce, for every run, a receipt recording for each described
  lane whether it was requested, refused before launch (with the reason), launched, and
  confirmed running.
- **FR-011**: The system MUST determine "confirmed running" by observing that a session for
  that lane is actually live, and MUST NOT infer it from having issued the launch.
- **FR-012**: The system MUST report a run in which any launched lane is not confirmed running
  as a failure, and MUST name every such lane.
- **FR-013**: The receipt MUST remain retrievable after the run's terminal output is gone.

**Shares and visibility**

- **FR-014**: The description MUST be able to mark a shared location as required-for-visibility
  or optional.
- **FR-015**: The system MUST wait a bounded time for shared locations and MUST NOT block a
  resume indefinitely on one, so that a host which is up still resumes when a host serving a
  share is down.
- **FR-016**: When a shared location is unavailable at the end of the wait, the system MUST
  state that the location cannot be seen, MUST state the remedy, and MUST NOT describe any
  data behind it as empty, absent, or unchanged.
- **FR-017**: The system MUST convey the unavailable-share state to every lane it starts, so
  that a lane cannot later mistake invisible data for absent data.
- **FR-018**: The system MUST NOT emit any finding about another host's work on the basis of a
  share it could not see.

**Enrolment**

- **FR-019**: An engineer MUST be able to enrol the repo they are currently in into this host's
  description in one action, with the path captured automatically.
- **FR-020**: An engineer MUST be able to withdraw the repo they are currently in from this
  host's description in one action.
- **FR-021**: Enrolling an already-enrolled repo, or withdrawing an unenrolled one, MUST change
  nothing and MUST say so.
- **FR-022**: The system MUST refuse to enrol a directory that is not a repo, with the reason.

**Automatic resume**

- **FR-023**: An engineer MUST be able to turn automatic resume-at-logon on, and off, for a
  host, in one action each.
- **FR-024**: Turning automatic resume on or off MUST be safe to repeat: a second identical
  action MUST change nothing and MUST report that it changed nothing.
- **FR-025**: When the host cannot grant the permission automatic resume requires, the system
  MUST fail loudly with the reason and MUST NOT leave the host reporting that automatic resume
  is on.
- **FR-026**: An engineer MUST be able to see whether automatic resume is currently on for a
  host, and when it last ran.
- **FR-027**: An engineer MUST be able to exercise the automatic path on demand without
  rebooting.

**Boundaries**

- **FR-028**: The system MUST NOT modify the contents of any repo it resumes, MUST NOT create
  commits, MUST NOT push, and MUST NOT invoke any pipeline stage. Its only effects are
  launching lanes, reading and writing the host's own description and receipts, and
  registering or removing the automatic trigger.
- **FR-029**: The system MUST operate on a single host, and MUST NOT write to another host's
  description or trigger a resume on another host.
  [NEEDS CLARIFICATION: is distributing a host description *to* peer hosts in scope for this
  feature, or does each host stay solely responsible for its own — noting the recorded open
  block on authority for fleet-binding one-way actions?]

### Key Entities

- **Host resume profile**: the description of one host — which lanes it resumes, how its
  windows are arranged, which shared locations it requires. Machine-scoped, one per host.
- **Lane entry**: one repo within a profile — its name, its path on this host, and the grouping
  it belongs to.
- **Arrangement**: how a host wants its lanes presented — one group, or several named groups
  with lanes assigned to each.
- **Share requirement**: a shared location a host depends on, marked required-for-visibility or
  optional.
- **Resume receipt**: the durable record of one run — per lane: requested, refused (with
  reason), launched, confirmed running; plus which shares were visible; plus the overall
  outcome.
- **Automatic trigger registration**: the host-level state of whether resume fires at logon,
  and when it last did.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An engineer sets up post-reboot resume on a host that has never had it by writing
  only that host's description — **zero** edits to any file shared with another host.
- **SC-002**: On every host, a resume invoked with **zero** options produces that host's
  declared arrangement; no engineer needs to remember or supply an option to get what their
  host wants.
- **SC-003**: **100%** of runs in which a lane is launched but does not come up are reported as
  failures naming that lane, at the time of the run. No such run reports success.
- **SC-004**: **100%** of runs produce a receipt, retrievable after the run, that accounts for
  every described lane.
- **SC-005**: **Zero** runs describe share-backed data as empty, absent, or unchanged while a
  required share is unavailable.
- **SC-006**: Enrolling or withdrawing a repo takes **one** action from inside that repo, and
  the next resume reflects it with no further step.
- **SC-007**: Turning automatic resume on and off is **one** action each, and repeating either
  produces **no** change and an explicit no-change report.
- **SC-008**: On a host that cannot register an automatic trigger, **100%** of attempts to turn
  it on fail visibly; **zero** leave the host reporting it is on.
- **SC-009**: A resume on a host whose peers are down still completes; the unavailable shares
  delay it by no more than the declared bounded wait.
- **SC-010**: Across a fleet-wide reboot, the proportion of lanes that come back resumed
  mid-thread — rather than blank, forked, or summarised — is **100%** of the lanes that had
  session history.

## Assumptions

- The host resume profile is machine-scoped and lives outside any repo, since it names many
  repos and must survive any one of them being deleted or re-cloned.
- A lane with no stored session history is refused, never started blank. Starting blank is
  indistinguishable from a successful resume until the missing context is noticed by hand,
  which is precisely the failure this feature exists to remove.
- Repo paths are local to the host and are required; shared network locations are optional to
  *reach* but their absence is never silent. This mirrors the measured fleet reality that a
  fleet-wide reboot takes down the host serving the shares.
- Resuming means continuing the existing conversation in place. Forking to a new session, or
  allowing the resumed session to be summarised at start, both count as failures to resume.
- The current fleet is Windows hosts with a tabbed terminal and a logon-triggered scheduler.
  The profile describes arrangement in terms of groups rather than any particular terminal's
  vocabulary, so a host with a different terminal changes only the mechanism.
- The existing launcher (`post-reboot-restart.ps1`) and its installer are the reference
  behaviour to generalise, not the deliverable. Their measured properties carry forward: path
  case is irrelevant to session-store lookup, and a launch must be verified by observing live
  sessions rather than by trusting the launcher's own success message.
- This feature inherits the advisory boundary of the existing `/bk-onrestart` skill: host-level
  only, never editing a repo, never invoking a pipeline stage.
