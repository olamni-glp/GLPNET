# Ch 9 Sources — Social Networks

**PDF**: `GLP_ART.pdf`, book pp 81–96 (PDF pp 93–108).

## Sections (verified)
- 9.1 Direct Messaging — p 81 (channel primitives, DM protocol, play, execution trace)
- 9.2 Feeds and Followers — p 84 (feed structure, broadcast w/ ground guard, follower mgmt, feed server, play)
- 9.3 Groups — p 87 (manager-based + interlaced-stream; comparison)
- 9.4 Child-Safe Social Networking — p 92 (architecture, approval protocol, implementation, play)
- 9.5 Exercises — p 95 (OUT OF SCOPE)

## Code-block index — §9.1 Direct Messaging
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 9.1.1 | Channel ops as defined guards: `send/3`, `receive/3`, `new_channel/2` | p 81 | 3 unit clauses (re-introducing §3.2 channel ops) | channel-abstraction primitives |
| 9.1.2 | `dm_send/3`, `dm_receive/3` | p 82 | 2 unit clauses | DM-channel primitives |
| 9.1.3 | `bind_resp/2` | p 82 | 1 clause `Resp = Ch?` (explicit unification) | response-variable binding |
| 9.1.4 | `alice/3` + `alice_continue/3` | p 82 | 1 + 1 clauses; sends `dm_request(Resp)` then waits via `known(DMCh?)` | DM initiator |
| 9.1.5 | `bob/3` + `bob_continue/2` | p 83 | 1 + 1 clauses; allocates channel, binds via `bind_resp`, replies | DM responder |
| 9.1.6 | `verify/2` + `report/2` | p 83 | 2 unit clauses | result-checking |
| 9.1.7 | `play_dm/0` | p 83 | 1 clause wiring `alice/3`, `bob/3`, `verify` | DM play |

## Code-block index — §9.2 Feeds and Followers
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 9.2.1 | `make_feed/1` | p 84 | 1 unit clause `make_feed(feed([])).` | feed constructor |
| 9.2.2 | `extend_feed/3` | p 84 | 1 unit clause prepending a post | newest-first feed |
| 9.2.3 | `broadcast/3` | p 85 | 2 clauses (base + recursive guarded by `ground(Post?)`) | replication via ground guard |
| 9.2.4 | `get_feed/3` | p 85 | 2 clauses (key match + recurse) | feeds-list lookup |
| 9.2.5 | `serve_feed/3` | p 85 | 3 clauses (`post(Content)`, `subscribe(Name)`, base) | feed-server event loop |
| 9.2.6 | Feed-play actors `alice/3`, `bob_receive/2`, `carol_receive/2`, `verify/2`, `check/4`, `play_feed/0`, `play_continue/1` | p 86 | several clauses orchestrating subscribe→post→verify | feed play |

## Code-block index — §9.3 Manager-Based Groups
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 9.3.1 | `manager/3` + `manage/5` | p 88 | 1 entry + 4 manage clauses (`invite(Name)`, `post(From, Content)`, base, …) | manager event loop |
| 9.3.2 | `prepend_all/3` | p 88 | 2 clauses (base + recursive `ground(Msg?)` guard) | broadcast helper |
| 9.3.3 | Manager-play orchestration `get_member_stream/3`, `play_group_manager/0`, `play_continue/2`, `verify_group/3`, `check_feed/1`, `check_streams/2` | p 88–89 | several clauses | manager play |

## Code-block index — §9.3 Interlaced-Streams Groups (preview of Ch 10)
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 9.3.4 | `interlace/3` (interlace payloads with tips) | p 90 | 2 clauses | DAG-block production |
| 9.3.5 | `collect_tips/3` | p 90 | 3 clauses (`unknown(Bs?)`, `otherwise`, base) | tip detection via `unknown` guard |
| 9.3.6 | `member/4` + `tag_messages/3` | p 90 | 1 entry + 2 tag clauses | per-member interlaced producer |
| 9.3.7 | Interlaced-play orchestration `play_group_interlaced/0`, `verify_interlaced/3`, `check_has_blocks/1`, `report_success/0` | p 91 | several clauses | interlaced group play |

## Code-block index — §9.4 Child-Safe Social Networking (CSSN)
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 9.4.1 | `bind_resp/2` | p 93 | 1 unit clause `bind_resp(Resp?, Resp).` | response-variable binding (alt definition) |
| 9.4.2 | Requesting parent: `parent_smith/2`, `smith_wait/2`, `smith_done/2` | p 93 | 1 + 1 + 1 unit clauses | parent-side approval request |
| 9.4.3 | Approving parent: `parent_jones/2`, `jones_done/3` | p 93 | 1 + 1 unit clauses | parent-side approval grant |
| 9.4.4 | Play `play_child_safe/0`, `verify_child_safe/2`, `report_success` | p 94 | 1 + 1 + 1 clauses | CSSN play |

## Tables / Comparisons
- Manager-Based vs Interlaced groups comparison table — p 92.

## Tutorial mode
multi-actor-distillation. Per charter §1, one project subdirectory per use case under `ch09/`.

## Use cases (suggested grouping per charter)
1. **`ch09/direct-messaging/`** — §9.1 entire DM protocol (alice/bob/play_dm).
2. **`ch09/feeds-and-followers/`** — §9.2 feed server + subscribe/post/broadcast play.
3. **`ch09/manager-based-group/`** — §9.3 manager-based group (manager + manage + prepend_all + play_group_manager).
4. **`ch09/interlaced-stream-group/`** — §9.3 interlaced group (interlace, collect_tips, member, play_group_interlaced) — note this is the **preview** of Ch 10's protocol applied to group messaging.
5. **`ch09/child-safe-networking/`** — §9.4 CSSN (parent_smith, parent_jones, play_child_safe).
6. **`ch09/useful-techniques.glp`** — `bind_resp`, `get_feed`, `prepend_all`, `tag_messages` if shared across use cases.

## Companion appendix
**Social Networks Code** appendix in the book at p 153–157 (Direct Messaging Play, Feeds and Followers Play, Manager-Based Group Play, Interlaced Streams Group Play, Child-Safe Social Networking Play) — these are likely **complete, self-contained variants** of the same five plays presented in §9.1–9.4 and should be cross-referenced at extraction time.

## Companion repo references
- `programs/typed_book/cssn/direct_messaging/`, `feeds_followers/`, `manager_based_group/`, `interlaced_streams/`, `child_safe/` — typed CSSN modules (verify per-use-case).
- `programs/cssn_modules/` — production CSSN if present.
- `glp_multiagent/lib/main_cssg_mad_modules.dart` — Flutter template (clone per use case).
- `../charter.md`
