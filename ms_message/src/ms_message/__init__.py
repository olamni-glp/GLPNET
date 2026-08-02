"""ms_message — durable first-hop mesh messaging (feature 063 US2).

Signal-then-fetch on mailboxes/topics over any spec-025 link transport
(research R3): the originator journals to a WAL before acknowledging,
signals reachable targets, and the recipient fetches at its own pace from
a durable delivery position (exactly-once observation, research R7).

Authoritative contract: specs/063-wave-5-consolidated-captured-triad/
contracts/mesh-messaging-protocol.md (from the operator's intake brief
docs/roadmap-intake/durable-mesh-messaging-protocol.md).
"""

__version__ = "0.1.0"
