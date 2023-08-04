namespace Indexer;

public struct LeafNode
{
    public HashSet<int> Matches;
    public string Value;

    public LeafNode(string value, HashSet<int> matches)
    {
        Matches = matches;
        Value = value;
    }
}
