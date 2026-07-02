// Additive, non-behavioral: expose glp_il_codec internals (notably the internal
// ConstantCodec) to the feature-038 result-codec test assembly for the V5 oracle
// cross-check (T032, FR-007 — 029 is the C# byte oracle only). No production surface
// changes; ConstantCodec stays internal to every non-test consumer.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GlpResultCodec.Tests")]
