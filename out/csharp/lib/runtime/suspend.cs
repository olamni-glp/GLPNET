namespace GlpRuntime.Runtime;

public class SuspensionNote
{
    public ReaderId ReaderId { get; }
    public Hanger Hanger { get; }

    public SuspensionNote(ReaderId readerId, Hanger hanger)
    {
        ReaderId = readerId;
        Hanger   = hanger;
    }
}
