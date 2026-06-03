// Minimal cell tags (extend later as needed).
namespace GlpRuntime.Runtime;

/// <summary>Discriminator tag for the Cell hierarchy (extensible — not sealed).</summary>
public enum CellTag { writer, reader }

/// <summary>Minimal cell marker contract. Non-sealed: the hierarchy is open to future cell kinds.</summary>
public interface ICell
{
    CellTag Tag { get; }
}

/// <summary>Writable endpoint cell (WR). Paired to a ReaderId (RO) in the heap model.
/// For now, we carry only identifiers; actual heap/value comes later.</summary>
public class WriterCell : ICell
{
    public CellTag Tag { get; } = CellTag.writer;
    public long WriterId { get; }   // WriterId
    public long ReaderId { get; }   // ReaderId (pair)
    public bool Abandoned { get; set; } = false;

    public WriterCell(long writerId, long readerId)
    {
        WriterId = writerId;
        ReaderId = readerId;
    }
}

/// <summary>Read-only endpoint cell (RO). Owns a suspension-queue identity.</summary>
public class ReaderCell : ICell
{
    public CellTag Tag { get; } = CellTag.reader;
    public long ReaderId { get; }   // ReaderId

    public ReaderCell(long readerId)
    {
        ReaderId = readerId;
    }
}
