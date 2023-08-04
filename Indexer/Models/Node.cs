namespace Indexer.Models;

public class Node
{
    public string PathSegment;
    public Dictionary<string, Node> Children;
    public List<LeafNode> Leaves;

    public Node() {}

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
            Leaves = new List<LeafNode>();
        }

        var leafIdx = Leaves.FindIndex(l => l.Value == value);

        if (leafIdx == -1)
        {
            var matches = new HashSet<int> { matchIdx };
            Leaves.Add(new LeafNode(value, matches));
        }
        else
        {
            Leaves[leafIdx].Matches.Add(matchIdx);
        }
    }

}
