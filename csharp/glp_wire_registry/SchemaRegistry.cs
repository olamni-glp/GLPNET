// Dual-DSL functor schema registry (feature 041-crdtmsg-mvp, T054).
//
// Contract C6 / research R11 (E9) — Constitution V (Claude-only LM):
//   Each messaging functor carries BOTH an authoring form (qmedit plaintext DSL) and the formally
//   registered form (CDDL). The qmedit↔CDDL translation is CLAUDE-AGENTIC — authored here by the Claude
//   agent (no external LLM / OpenAI / litellm on any LM path). Both forms are stored; CDDL is the
//   registered artifact. This registry is the store; the round-trip translation runs via the Agent
//   tool / MCP seam when a new functor is added.

namespace GlpRuntime.WireRegistry;

public sealed record SchemaForms(string Functor, string QmeditDsl, string Cddl);

public static class SchemaRegistry
{
    // qmedit authoring form for the crdt_message functor (human-authored surface).
    private const string CrdtMessageQmedit = """
        message crdt_message {
          schema_version: int
          payload_type:   int
          crdt_model:     enum(none, state_based, op_based)
          header {
            msg_id: str; from: str; to: str; seq: int
            policy { targets: [str]; waypoints: [str]; excludes: [str] }
            capability_slot?: bytes        // envelope-v2 additive-optional
          }
          sections: [ { type_number: int; value: bytes } ]   // TLV; unknown carried verbatim
        }
        """;

    // CDDL registered artifact — the Claude-agentic translation of the qmedit form above.
    private const string CrdtMessageCddl = """
        crdt-message = {
          schema_version: uint,
          payload_type:   uint,
          crdt_model:     crdt-model,
          header:         header,
          sections:       [* section],
        }
        crdt-model = &( none: 0, state_based: 1, op_based: 2 )
        header = {
          msg_id: tstr,
          from:   tstr,
          to:     tstr,
          seq:    uint,
          policy: policy,
          ? capability_slot: bstr,
        }
        policy  = { targets: [* tstr], waypoints: [* tstr], excludes: [* tstr] }
        section = { type_number: uint, value: bstr }
        """;

    private static readonly IReadOnlyDictionary<string, SchemaForms> Forms =
        new Dictionary<string, SchemaForms>
        {
            ["crdt_message"] = new SchemaForms("crdt_message", CrdtMessageQmedit, CrdtMessageCddl),
        };

    /// <summary>Both stored DSL forms for a functor; loud-fail on an unregistered functor.</summary>
    public static SchemaForms Get(string functor) =>
        Forms.TryGetValue(functor, out var f)
            ? f
            : throw new WireRegistryException($"no schema forms registered for functor '{functor}'");

    public static bool Has(string functor) => Forms.ContainsKey(functor);

    /// <summary>The registered CDDL artifact for a functor (the formal form).</summary>
    public static string Cddl(string functor) => Get(functor).Cddl;

    /// <summary>The qmedit authoring form for a functor (the human surface).</summary>
    public static string Qmedit(string functor) => Get(functor).QmeditDsl;
}
