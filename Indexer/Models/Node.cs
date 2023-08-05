namespace Indexer.Models;

public class Node
{
    public string PathSegment;
    public Dictionary<string, Node> Children;
    public Dictionary<string, LeafNode> Leaves;

    public Node() { }

    public Node(string path)
    {
        PathSegment = path;
    }

    public void AddChild(Node child)
    {
        if (Children == null)
        {
            Children = new Dictionary<string, Node>();
        }

        Children[child.PathSegment] = child;
    }

    public Node GetChild(string path)
    {
        if (Children == null || !Children.TryGetValue(path, out var child))
        {
            return null;
        }

        return child;
    }

    public void AddMatch(string value, int matchIdx)
    {
        if (Leaves == null)
        {
            Leaves = new Dictionary<string, LeafNode>();
        }

        if (!Leaves.TryGetValue(value, out var leaf))
        {
            Leaves[value] = new LeafNode(value, new HashSet<int> { matchIdx });
        }
        else
        {
            leaf.Matches.Add(matchIdx);
        }
    }
}
