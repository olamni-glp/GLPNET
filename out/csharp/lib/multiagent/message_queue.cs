// Message Queue (M_p) for madGLP
//
// Manages outbound messages from an agent to other agents.
// Messages are queued per destination with FIFO ordering.
//
// Specification: /docs/ma/madGLP-spec.md Section 6.1

using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace GlpRuntime.Multiagent;

/// <summary>
/// Type of outbound message.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Assignment: (G := T, destination).
    /// G is a global name (_w(p,i) or _r(p,i)), T is the assigned term.
    /// </summary>
    Assignment,

    /// <summary>
    /// Agent message: structured term sent between agents.
    /// Used for friend-to-friend communication (msg/3 terms).
    /// </summary>
    AgentMessage,
}

/// <summary>
/// An outbound message to another agent.
/// </summary>
public class OutboundMessage
{
    /// <summary>Destination agent ID.</summary>
    public string Destination { get; }

    /// <summary>Type of message.</summary>
    public MessageType Type { get; }

    /// <summary>Serialized payload (opaque bytes).</summary>
    public List<byte> Payload { get; }

    /// <summary>
    /// Initializes a new <see cref="OutboundMessage"/>.
    /// </summary>
    /// <param name="destination">Destination agent ID.</param>
    /// <param name="type">Type of message.</param>
    /// <param name="payload">Serialized payload (opaque bytes).</param>
    public OutboundMessage(string destination, MessageType type, List<byte> payload)
    {
        Destination = destination;
        Type = type;
        Payload = payload;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"OutboundMessage(to={Destination}, type={Type}, {Payload.Count} bytes)";
}

/// <summary>
/// Message Queue (M_p) for managing outbound messages.
/// <para>
/// Maintains FIFO ordering per destination and ensures at-most-once delivery.
/// </para>
/// <list type="bullet">
///   <item><description>FIFO per destination: Messages to same agent delivered in order.</description></item>
///   <item><description>At-most-once: Each message delivered at most once.</description></item>
///   <item><description>Eventual delivery: All queued messages eventually delivered (assuming connectivity).</description></item>
/// </list>
/// </summary>
public class MessageQueue
{
    /// <summary>Queues indexed by destination agent ID.</summary>
    private readonly Dictionary<string, Queue<OutboundMessage>> _queuesByDestination = new();

    /// <summary>
    /// Add a message to the queue.
    /// <para>Messages are queued per destination with FIFO ordering.</para>
    /// </summary>
    /// <param name="message">The message to enqueue.</param>
    public void Add(OutboundMessage message)
    {
        if (!_queuesByDestination.TryGetValue(message.Destination, out var queue))
        {
            queue = new Queue<OutboundMessage>();
            _queuesByDestination[message.Destination] = queue;
        }
        queue.Enqueue(message);
    }

    /// <summary>
    /// Poll (remove and return) the next message for a destination.
    /// <para>Returns <see langword="null"/> if no messages are queued for this destination.</para>
    /// <para>Maintains FIFO ordering — oldest message is returned first.</para>
    /// </summary>
    /// <param name="destination">The destination agent ID.</param>
    /// <returns>The next message, or <see langword="null"/> if the queue is empty.</returns>
    public OutboundMessage? Poll(string destination)
    {
        if (!_queuesByDestination.TryGetValue(destination, out var queue) || queue.Count == 0)
        {
            return null;
        }

        var message = queue.Dequeue();

        // Clean up empty bucket
        if (queue.Count == 0)
        {
            _queuesByDestination.Remove(destination);
        }

        return message;
    }

    /// <summary>
    /// Peek at the next message for a destination without removing it.
    /// <para>Returns <see langword="null"/> if no messages are queued for this destination.</para>
    /// </summary>
    /// <param name="destination">The destination agent ID.</param>
    /// <returns>The next message, or <see langword="null"/> if the queue is empty.</returns>
    public OutboundMessage? Peek(string destination)
    {
        if (!_queuesByDestination.TryGetValue(destination, out var queue) || queue.Count == 0)
        {
            return null;
        }
        return queue.Peek();
    }

    /// <summary>
    /// Get the number of messages queued for a destination.
    /// </summary>
    /// <param name="destination">The destination agent ID.</param>
    /// <returns>The message count, or 0 if no bucket exists for the destination.</returns>
    public int CountFor(string destination) =>
        _queuesByDestination.TryGetValue(destination, out var queue) ? queue.Count : 0;

    /// <summary>
    /// Get all destinations that have queued messages.
    /// <para>Returns an eager snapshot; subsequent mutations are not reflected in the returned list.</para>
    /// </summary>
    public IReadOnlyList<string> Destinations =>
        _queuesByDestination.Keys.ToList();

    /// <summary>
    /// Check if the queue is empty (no messages for any destination).
    /// </summary>
    public bool IsEmpty => _queuesByDestination.Count == 0;

    /// <summary>
    /// Check if the queue has messages.
    /// </summary>
    public bool IsNotEmpty => _queuesByDestination.Count > 0;

    /// <summary>
    /// Total number of messages across all destinations.
    /// </summary>
    public int TotalLength => _queuesByDestination.Values.Sum(q => q.Count);

    /// <summary>
    /// Clear all messages for all destinations.
    /// </summary>
    public void Clear() => _queuesByDestination.Clear();

    /// <summary>
    /// Clear all messages for a specific destination.
    /// </summary>
    /// <param name="destination">The destination agent ID.</param>
    public void ClearFor(string destination) =>
        _queuesByDestination.Remove(destination);

    /// <summary>
    /// Get all messages for a destination without removing them.
    /// <para>Returns an empty read-only list if no messages are queued.</para>
    /// <para>The returned list is a snapshot; subsequent enqueues are not visible through it.</para>
    /// </summary>
    /// <param name="destination">The destination agent ID.</param>
    /// <returns>A read-only snapshot of all queued messages for the destination.</returns>
    public IReadOnlyList<OutboundMessage> PeekAll(string destination)
    {
        if (!_queuesByDestination.TryGetValue(destination, out var queue))
        {
            return Array.Empty<OutboundMessage>();
        }
        return new List<OutboundMessage>(queue).AsReadOnly();
    }

    /// <summary>
    /// Poll all messages for a destination.
    /// <para>Returns an empty list if no messages are queued.</para>
    /// <para>Removes all messages from the queue for the destination.</para>
    /// </summary>
    /// <param name="destination">The destination agent ID.</param>
    /// <returns>A new list containing all previously queued messages for the destination.</returns>
    public List<OutboundMessage> PollAll(string destination)
    {
        if (!_queuesByDestination.TryGetValue(destination, out var queue) || queue.Count == 0)
        {
            return new List<OutboundMessage>();
        }

        var messages = new List<OutboundMessage>(queue);
        _queuesByDestination.Remove(destination);
        return messages;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        if (IsEmpty)
        {
            return "MessageQueue: empty";
        }

        var buffer = new StringBuilder("MessageQueue:\n");
        foreach (var destination in Destinations)
        {
            var count = CountFor(destination);
            buffer.AppendLine($"  {destination}: {count} message(s)");
        }
        return buffer.ToString();
    }
}
