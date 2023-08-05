using Indexer.Models;
using Indexer.Serializer;
using System.Text.Json;

namespace Indexer;

public class PageIndex<T> : IPageIndex<T>
{
    private readonly Node _index;
    private readonly List<object> _entities;
    private readonly Dictionary<object, int> _entityIndexMap;
    private readonly IIndexSerializer _serializer;

    #region Constructors
    public PageIndex()
    {
        _serializer = new JsonDocumentSerializer();
        _index = new Node();
        _entityIndexMap = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        _entities = new List<object>();
    }

    public PageIndex(IEnumerable<object> entities) : this()
    {
        Index(entities);
    }

    ~PageIndex()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _serializer.Dispose();
        }
    }
    #endregion

    #region Indexing
    public void Index(object obj, Node node = null)
    {
        using (var document = _serializer.SerializeToDocument(obj))
        {
            Index(document.RootElement, node, obj);
        }
    }

    public void Index(IEnumerable<object> entities, Node node = null)
    {
        foreach (var entity in entities)
        {
            Index(entity, node);
        }
    }

    public void Index(JsonElement jsonElement, Node node, object obj)
    {
        switch (jsonElement.ValueKind)
        {
            case JsonValueKind.Object:
                IndexObject(jsonElement, node, obj);
                break;
            case JsonValueKind.Array:
                IndexArray(jsonElement, node, obj);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                break;
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.False:
            case JsonValueKind.True:
                int idx;
                if (!_entityIndexMap.TryGetValue(obj, out idx))
                {
                    _entities.Add(obj);
                    idx = _entities.Count - 1;

                    node.AddMatch(jsonElement.ToString(), idx);
                    _entityIndexMap[obj] = idx;
                }

                node.AddMatch(jsonElement.ToString(), idx);
                break;
        }
    }

    private void IndexObject(JsonElement jsonElement, Node node, object obj)
    {
        foreach (JsonProperty property in jsonElement.EnumerateObject())
        {
            Node currentNode = GetNextNode(property.Name, node);
            Index(property.Value, currentNode, obj);
        }
    }

    private Node GetNextNode(string propertyName, Node node)
    {
        if (node == null)
        {
            var existingChild = _index.GetChild(propertyName);
            if (existingChild != null)
            {
                return existingChild;
            }
            else
            {
                var newNode = new Node(propertyName);
                _index.AddChild(newNode);

                return newNode;
            }
        }
        else
        {
            var existingChild = node.GetChild(propertyName);
            if (existingChild != null)
            {
                return existingChild;
            }
            else
            {
                var newNode = new Node(propertyName);
                node.AddChild(newNode);
                return newNode;
            }
        }
    }

    private void IndexArray(JsonElement jsonElement, Node node, object obj)
    {
        foreach (JsonElement item in jsonElement.EnumerateArray())
        {
            Index(item, node, obj);
        }
    }
    #endregion

    #region Searching
    public IEnumerable<object> Search(List<ComplexSearch> complexSearches)
    {
        var results = new List<int>();
        foreach (var complexSearch in complexSearches)
        {
            var searchResults = Search(complexSearch);
            if (searchResults != null)
            {
                results.AddRange(searchResults);
            }
        }

        var distinctResults = results.Distinct();
        return distinctResults.Select(idx => _entities[idx]); ;
    }

    private IEnumerable<int> Search(ComplexSearch complexSearch)
    {
        IEnumerable<int> results = null;

        foreach (var searchFilter in complexSearch.OneOf ?? Enumerable.Empty<SearchFilter>())
        {
            var matches = GetFromFilter(searchFilter);
            if (matches.Count() == 0)
            {
                continue;
            }

            if (results == null)
            {
                results = matches;
                continue;
            }

            results = results.Intersect(matches);
        }

        foreach (var searchFilter in complexSearch.NotOneOf ?? Enumerable.Empty<SearchFilter>())
        {
            var matches = GetFromFilter(searchFilter);
            if (matches.Count() == 0)
            {
                continue;
            }

            results.Except(matches);
        }

        return results;
    }

    private IEnumerable<int> GetFromFilter(SearchFilter filter)
    {
        var delimitedPath = filter.Field.Split('.');
        var currentNode = _index;

        for (var i = 0; i < delimitedPath.Length; i++)
        {
            var child = currentNode.GetChild(delimitedPath[i]);
            if (child == null)
            {
                return new List<int>();
            }

            currentNode = child;
        }

        var matches = new List<int>();

        foreach (var value in filter.Values)
        {
            if (currentNode.Leaves.TryGetValue(value, out var leafNode))
            {
                matches.AddRange(leafNode.Matches);
            }
        }

        return matches.Distinct();
    }
    #endregion

    public Node GetIndex()
    {
        return _index;
    }

    public object GetEntity(int index)
    {
        if (index >= _entities.Count)
        {
            return default;
        }

        if (index == -1)
        {
            return default;
        }

        return _entities[index];
    }

}

public interface IPageIndex<T>
{
    void Index(object obj, Node node);
    void Index(IEnumerable<object> entities, Node node);
    void Index(JsonElement jsonElement, Node node, object obj);
    IEnumerable<object> Search(List<ComplexSearch> complexSearches);

    object GetEntity(int index);
    Node GetIndex();
}
