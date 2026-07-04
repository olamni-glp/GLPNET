// Amulet capability slot — Amoeba 4-field shape (feature 041-crdtmsg-mvp, T038).
//
// Contract C15 / data-model §7: reserved token slot of the Amoeba 4-field shape
//   {Port 48b, ObjNum 24b, Rights 8b, Check ≥128b}. The Check field is WIDENED to ≥128 bits (E5), so the
//   legacy literal 16-byte Amoeba capability is rejected (a full slot is strictly larger than 16 bytes).
//   Rights-bit semantics are deferred (does not block the wire slot).

using GlpRuntime.CrdtMsg.Envelope;

namespace GlpRuntime.CrdtMsg.Cap;

public sealed record Amulet(ulong Port /*48b*/, uint ObjNum /*24b*/, byte Rights, byte[] Check)
{
    public const int MinCheckBytes = 16; // ≥128 bits

    public byte[] Encode()
    {
        if (Port > 0xFFFF_FFFF_FFFF) throw new CrdtMsgException("amulet Port exceeds 48 bits");
        if (ObjNum > 0x00FF_FFFF) throw new CrdtMsgException("amulet ObjNum exceeds 24 bits");
        if (Check.Length < MinCheckBytes)
            throw new CrdtMsgException($"amulet Check must be ≥{MinCheckBytes} bytes (≥128b); legacy 16-byte fidelity rejected");

        var buf = new byte[6 + 3 + 1 + Check.Length];
        for (int i = 0; i < 6; i++) buf[i] = (byte)(Port >> (8 * i));       // 48-bit LE
        for (int i = 0; i < 3; i++) buf[6 + i] = (byte)(ObjNum >> (8 * i)); // 24-bit LE
        buf[9] = Rights;
        Array.Copy(Check, 0, buf, 10, Check.Length);
        return buf;
    }

    public static Amulet Decode(byte[] bytes)
    {
        // A valid slot is strictly larger than the legacy 16-byte capability (10 header + ≥16 check).
        if (bytes.Length <= 16)
            throw new CrdtMsgException("amulet slot too small; legacy 16-byte Amoeba capability rejected (E5)");
        if (bytes.Length < 10 + MinCheckBytes)
            throw new CrdtMsgException("amulet slot truncated");

        ulong port = 0;
        for (int i = 0; i < 6; i++) port |= (ulong)bytes[i] << (8 * i);
        uint obj = 0;
        for (int i = 0; i < 3; i++) obj |= (uint)bytes[6 + i] << (8 * i);
        byte rights = bytes[9];
        byte[] check = bytes[10..];
        return new Amulet(port, obj, rights, check);
    }
}
