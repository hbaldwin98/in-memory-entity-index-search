using Indexer.Models;

namespace Indexer.Extensions;

public static class IndexerExtensions
{
    public static IEnumerable<T> GetMatches<T>(this IPageIndex<T> indexer, string value, string field)
        where T : IBaseEntity
    {
        if (indexer == null)
        {
            throw new ArgumentNullException(nameof(indexer));
        }

        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(value));
        }

        if (string.IsNullOrEmpty(field))
        {
            throw new ArgumentException("Field cannot be null or empty.", nameof(field));
        }

        var index = indexer.GetIndex();
        var splitField = field.Split('.');
        var currentNode = index;

        for (var i = 0; i < splitField.Length; i++)
        {
            var child = currentNode.GetChild(splitField[i]);
            if (child == null)
            {
                return new List<T>();
            }

            currentNode = child;
        }

        var matches = new List<T>();

        if (currentNode.Leaves.TryGetValue(value, out var leafNode))
        {
            matches.AddRange(leafNode.Matches.Select(m => (T)indexer.GetEntity(m)));
        }

        return matches;
    }
}
