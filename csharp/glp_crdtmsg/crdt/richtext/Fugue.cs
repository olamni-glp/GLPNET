// Fugue sequence CRDT — maximal non-interleaving (feature 041-crdtmsg-mvp, T032).
//
// Contract C11 / research R6: insert ops name left/right origins over stable element ids; ordering by
// the Fugue TREE yields maximal non-interleaving — concurrent typing never interleaves (SC-012).
//
// Representation: each element is a tree node with a stable id (its op's DVV dot), a parent id, and a
// side (Left/Right of the parent). In-order traversal = visit a node's Left children, then the node,
// then its Right children. Same-side siblings are ordered by dot (a total, replica-independent order).
//
// Placement rule (computed by the inserter, then RECORDED in the op so all replicas place identically):
// for a new element between visible neighbours L and R, exactly one of L,R is the other's tree ancestor
// (they are in-order adjacent). If L is an ancestor of R → the new node is R's LEFT child; else it is
// L's RIGHT child. Consequence: the 2nd..nth elements of a typed run attach to their own predecessor
// (which has no right child yet), so a whole run is one subtree — only run-STARTS are siblings, and
// sorting siblings by dot groups the runs. That is the no-interleaving guarantee.

using GlpRuntime.CrdtMsg.Crdt;

namespace GlpRuntime.CrdtMsg.Crdt.RichText;

public enum FugueSide { Left, Right }

public sealed class FugueNode
{
    public Dot Id { get; }
    public Dot? ParentId { get; }
    public FugueSide Side { get; }
    public string Value { get; }

    public FugueNode(Dot id, Dot? parentId, FugueSide side, string value)
    {
        Id = id;
        ParentId = parentId;
        Side = side;
        Value = value;
    }
}

public sealed class FugueTree
{
    private readonly Dictionary<Dot, FugueNode> _nodes = new();
    private readonly HashSet<Dot> _deleted = new(); // may arrive before the insert (out-of-order tolerant)

    /// <summary>Integrate an insert. Idempotent by element id (FR-015).</summary>
    public bool Integrate(Dot id, Dot? parentId, FugueSide side, string value)
    {
        if (_nodes.ContainsKey(id)) return false;
        _nodes[id] = new FugueNode(id, parentId, side, value);
        return true;
    }

    /// <summary>Tombstone an element by id (observed-remove: targets that specific dot).</summary>
    public bool Delete(Dot id) => _deleted.Add(id);

    public bool Contains(Dot id) => _nodes.ContainsKey(id);

    /// <summary>Compute (parentId, side) for a new element between two adjacent visible neighbours.</summary>
    public (Dot? parentId, FugueSide side) Place(Dot? leftId, Dot? rightId)
    {
        if (rightId is null) return (leftId, FugueSide.Right);   // append (or empty → root right child)
        if (leftId is null) return (rightId, FugueSide.Left);    // prepend before the first element
        return IsAncestor(leftId.Value, rightId.Value)
            ? (rightId, FugueSide.Left)
            : (leftId, FugueSide.Right);
    }

    private bool IsAncestor(Dot ancestor, Dot node)
    {
        Dot? cur = _nodes.TryGetValue(node, out var n) ? n.ParentId : null;
        while (cur is not null)
        {
            if (cur.Value.Equals(ancestor)) return true;
            cur = _nodes[cur.Value].ParentId;
        }
        return false;
    }

    /// <summary>Visible elements in sequence order (excludes tombstoned).</summary>
    public IReadOnlyList<FugueNode> Visible()
    {
        var childrenByParent = new Dictionary<Dot, List<FugueNode>>();
        var rootChildren = new List<FugueNode>();
        foreach (var n in _nodes.Values)
        {
            if (n.ParentId is null) rootChildren.Add(n);
            else (childrenByParent.TryGetValue(n.ParentId.Value, out var l) ? l
                    : childrenByParent[n.ParentId.Value] = new List<FugueNode>()).Add(n);
        }

        var outp = new List<FugueNode>();
        void Emit(FugueNode node)
        {
            if (childrenByParent.TryGetValue(node.Id, out var kids))
            {
                foreach (var c in Side(kids, FugueSide.Left)) Emit(c);
                if (!_deleted.Contains(node.Id)) outp.Add(node);
                foreach (var c in Side(kids, FugueSide.Right)) Emit(c);
            }
            else if (!_deleted.Contains(node.Id))
            {
                outp.Add(node);
            }
        }

        // root children: Left-side (prepends) before, Right-side after the (virtual) origin
        foreach (var c in Side(rootChildren, FugueSide.Left)) Emit(c);
        foreach (var c in Side(rootChildren, FugueSide.Right)) Emit(c);
        return outp;
    }

    private static IEnumerable<FugueNode> Side(List<FugueNode> kids, FugueSide side) =>
        kids.Where(k => k.Side == side).OrderBy(k => k.Id);

    /// <summary>The rendered text.</summary>
    public string Text() => string.Concat(Visible().Select(n => n.Value));
}
