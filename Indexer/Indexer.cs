using Indexer.Models;
using Indexer.Serializer;
using System.Text.Json;

namespace Indexer;

public class Indexer<T> : IIndexer<T> where T : IBaseEntity
{
    private Node _index = new Node();
    private Dictionary<T, int> _entityIndexMap = new Dictionary<T, int>();
    private List<T> _entities = new List<T>();
    private readonly IIndexSerializer<T> _serializer;

    #region Constructors
    public Indexer()
    {
        _serializer = new JsonDocumentSerializer<T>();
    }

    public Indexer(List<T> entities) : this()
    {
        Index(entities);
    }

    ~Indexer()
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
    public void Index(T obj, Node node = null)
    {
        using (var document = _serializer.SerializeToDocument(obj))
        {
            Index(document.RootElement, node, obj);
        }
    }

    public void Index(List<T> entities, Node node = null)
    {
        foreach (var entity in entities)
        {
            Index(entity, node);
        }
    }

    public void Index(JsonElement jsonElement, Node node, T obj)
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
                if (!_entityIndexMap.TryGetValue(obj, out var existingIdx))
                {
                    _entities.Add(obj);

                    node.AddMatch(jsonElement.ToString(), _entities.Count - 1);
                    _entityIndexMap[obj] = _entities.Count - 1;
                }
                else
                {
                    node.AddMatch(jsonElement.ToString(), existingIdx);
                }

                break;
        }
    }

    private void IndexObject(JsonElement jsonElement, Node node, T obj)
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

    private void IndexArray(JsonElement jsonElement, Node node, T obj)
    {
        foreach (JsonElement item in jsonElement.EnumerateArray())
        {
            Index(item, node, obj);
        }
    }
    #endregion

    #region Searching
    public IEnumerable<T> Search(List<ComplexSearch> complexSearches)
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

        foreach (var leafNode in currentNode.Leaves)
        {
            foreach (var value in filter.Values)
            {
                if (leafNode.Value.Equals(value))
                {
                    matches.AddRange(leafNode.Matches);
                }
            }
        }

        return matches.Distinct();
    }
    #endregion

    public Node GetIndex()
    {
        return _index;
    }

    public T GetEntity(int index)
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

public interface IIndexer<T> : IDisposable where T : IBaseEntity
{
    void Index(T obj, Node node);
    void Index(List<T> entities, Node node);
    void Index(JsonElement jsonElement, Node node, T obj);
    IEnumerable<T> Search(List<ComplexSearch> complexSearches);

    T GetEntity(int index);
    Node GetIndex();
}
