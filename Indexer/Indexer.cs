using Indexer.Models;
using System.Buffers;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Indexer;

public class Nested : Dictionary<string, string>
{

}
public interface IIndexer<T> : IDisposable where T : IBaseEntity
{
    void Index(T obj, string path = null);
    void Index(List<T> entities, string path = null);
    void Index(JsonElement jsonElement, string path, T obj);
    IEnumerable<T> Search(List<ComplexSearch> complexSearches);
    Dictionary<string, Dictionary<string, HashSet<T>>> GetIndex();
}

public interface IIndexSerializer<T> : IDisposable where T : IBaseEntity
{
    ReadOnlyMemory<byte> SerializeToMemory(T obj);
    JsonDocument SerializeToDocument(T obj);
}

public class JsonDocumentSerializer<T> : IIndexSerializer<T>, IDisposable where T : IBaseEntity
{
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private readonly ArrayBufferWriter<byte> _bufferWriter = new ArrayBufferWriter<byte>(4096);
    private readonly Utf8JsonWriter _jsonWriter;

    public JsonDocumentSerializer()
    {
        _jsonWriter = new Utf8JsonWriter(_bufferWriter);
    }

    public ReadOnlyMemory<byte> SerializeToMemory(T obj)
    {
        _bufferWriter.Clear();
        _jsonWriter.Reset(_bufferWriter);

        JsonSerializer.Serialize(_jsonWriter, obj, _jsonOptions);

        return _bufferWriter.WrittenMemory;
    }
    public void Dispose()
    {
        _jsonWriter?.Dispose();
    }

    public JsonDocument SerializeToDocument(T obj)
    {
        return JsonDocument.Parse(SerializeToMemory(obj));
    }
}

public class Indexer<T> : IIndexer<T> where T : IBaseEntity
{
    private readonly Dictionary<string, Dictionary<string, HashSet<T>>> _index = new Dictionary<string, Dictionary<string, HashSet<T>>>();
    private readonly Dictionary<string, Dictionary<Nested, HashSet<T>>> _nestedObjects = new Dictionary<string, Dictionary<Nested, HashSet<T>>>();
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
    public void Index(T obj, string path = null)
    {
        using (var document = _serializer.SerializeToDocument(obj))
        {
            Index(document.RootElement, path, obj);
        }
    }

    public void Index(List<T> entities, string path = null)
    {
        foreach (var entity in entities)
        {
            Index(entity, path);
        }
    }

    public void Index(JsonElement jsonElement, string path, T obj)
    {
        switch (jsonElement.ValueKind)
        {
            case JsonValueKind.Object:
                IndexObject(jsonElement, path, obj);
                break;
            case JsonValueKind.Array:
                IndexArray(jsonElement, path, obj);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                break;
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.False:
            case JsonValueKind.True:
                string term = jsonElement.ToString();

                if (!_index.TryGetValue(term, out var index))
                {
                    index = new Dictionary<string, HashSet<T>>();
                    _index[term] = index;
                }

                if (!index.TryGetValue(path, out var matches))
                {
                    matches = new HashSet<T>(new IdEqualityComparer<T>());
                    index[path] = matches;
                }
                matches.Add(obj);
                break;
        }
    }

    private void IndexObject(JsonElement jsonElement, string path, T obj)
    {
        var nestedObject = new Nested();
        foreach (JsonProperty property in jsonElement.EnumerateObject())
        {
            string propertyPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
            nestedObject[propertyPath] = property.Value.ToString();
            Index(property.Value, propertyPath, obj);
        }

        if (path != null)
        {
            if (!_nestedObjects.TryGetValue(path, out var nestedIndex))
            {
                nestedIndex = new Dictionary<Nested, HashSet<T>>(new NestedEqualityComparer());
                _nestedObjects[path] = nestedIndex;
            }

            if (!nestedIndex.TryGetValue(nestedObject, out var nestedMatches))
            {
                nestedMatches = new HashSet<T>(new IdEqualityComparer<T>());
                nestedIndex[nestedObject] = nestedMatches;
            }

            nestedMatches.Add(obj);
        }
    }

    private void IndexArray(JsonElement jsonElement, string path, T obj)
    {
        string itemPath = string.IsNullOrEmpty(path) ? "" : path;
        foreach (JsonElement item in jsonElement.EnumerateArray())
        {
            Index(item, itemPath, obj);
        }
    }
    #endregion
    #region Searching
    public IEnumerable<T> Search(List<ComplexSearch> complexSearches)
    {
        HashSet<T> matchingEntities = new HashSet<T>(new IdEqualityComparer<T>());

        foreach (var complexSearch in complexSearches)
        {
            var oneOfMatches = GetEntities(complexSearch.OneOf ?? Enumerable.Empty<SearchFilter>());
            var notOneOfMatches = GetEntities(complexSearch.NotOneOf ?? Enumerable.Empty<SearchFilter>());

            if (notOneOfMatches.Count > 0)
            {
                oneOfMatches.ExceptWith(notOneOfMatches);
            }

            matchingEntities.UnionWith(oneOfMatches);

            ReturnHashSet(oneOfMatches);
            ReturnHashSet(notOneOfMatches);
        }

        return matchingEntities;
    }

    private HashSet<T> GetEntities(IEnumerable<SearchFilter> filters)
    {
        if (filters == null || filters.Count() == 0)
        {
            return GetHashSet();
        }

        var nestedFilters = filters.Any(f => !string.IsNullOrEmpty(f.NestedPrefix)) ? filters : new List<SearchFilter>();
        if (nestedFilters.Any())
        {
            return GetEntitiesWithNestedFilters(nestedFilters.ToList(), nestedFilters.Where(f => !string.IsNullOrEmpty(f.NestedPrefix)).First().NestedPrefix);
        }

        // Calculate the number of matching entities for each non-nested filter without retrieving the entities
        var filterCounts = filters.ToDictionary(filter => filter, filter => GetEntityCountForFilter(filter));
        if (filterCounts.Any(c => c.Value == 0))
        {
            return GetHashSet();
        }
        // Sort filters by the number of matching entities to optimize intersections
        var sortedFilters = filters.OrderBy(filter => filterCounts[filter]).ToList();

        HashSet<T> entities = null;
        foreach (var filter in sortedFilters)
        {
            HashSet<T> currentEntities = GetEntitiesFromFilter(filter);
            if (currentEntities.Count == 0)
            {
                return currentEntities;
            }

            entities = ProcessEntities(entities, currentEntities);
            if (entities.Count == 0)
            {
                return entities;
            }
        }

        return entities ?? GetHashSet();
    }

    private HashSet<T> GetEntitiesWithNestedFilters(List<SearchFilter> nestedFilters, string nestedKey)
    {
        if (!_nestedObjects.TryGetValue(nestedKey, out var nestedIndex))
        {
            return GetHashSet();
        }

        var entities = GetHashSet();
        foreach (var kvp in nestedIndex)
        {
            var nestedObject = kvp.Key;
            var matches = kvp.Value;

            var allFiltersMatch = true;
            foreach (var filter in nestedFilters)
            {
                if (!nestedObject.TryGetValue(filter.Field, out var nestedValue) || !filter.Values.Contains(nestedValue))
                {
                    allFiltersMatch = false;
                    break;
                }
            }

            if (allFiltersMatch)
            {
                entities.UnionWith(matches);
            }
        }

        return entities;
    }

    private HashSet<T> ProcessEntities(HashSet<T> entities, HashSet<T> currentEntities)
    {
        if (entities == null)
        {
            entities = currentEntities;
        }
        else
        {
            entities.IntersectWith(currentEntities);
            ReturnHashSet(currentEntities);
        }
        return entities;
    }

    private int GetEntityCountForFilter(SearchFilter filter)
    {
        int count = 0;
        foreach (var value in filter.Values)
        {
            if (_index.TryGetValue(value, out var pathIndex) && pathIndex.TryGetValue(filter.Field, out var matches))
            {
                count += matches.Count;
            }
        }
        return count;
    }

    private HashSet<T> GetEntitiesFromFilter(SearchFilter filter)
    {
        HashSet<T> currentEntities = GetHashSet();
        foreach (var value in filter.Values)
        {
            if (_index.TryGetValue(value, out var pathIndex) && pathIndex.TryGetValue(filter.Field, out var matches))
            {
                currentEntities.UnionWith(matches);
            }
        }

        return currentEntities;
    }

    private readonly ConcurrentStack<HashSet<T>> _hashSetPool = new ConcurrentStack<HashSet<T>>();
    private HashSet<T> GetHashSet()
    {
        if (_hashSetPool.TryPop(out var hashSet))
        {
            hashSet.Clear();
            return hashSet;
        }
        return new HashSet<T>(new IdEqualityComparer<T>());
    }

    private void ReturnHashSet(HashSet<T> hashSet)
    {
        _hashSetPool.Push(hashSet);
    }

    public Dictionary<string, Dictionary<string, HashSet<T>>> GetIndex()
    {
        return _index;
    }
    #endregion
}

public class IdEqualityComparer<T> : IEqualityComparer<T> where T : IBaseEntity
{
    public bool Equals(T x, T y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return x.Id == y.Id;
    }

    public int GetHashCode(T obj)
    {
        return obj.Id?.GetHashCode() ?? obj.GetHashCode();
    }
}

public class NestedEqualityComparer : IEqualityComparer<Nested>
{
    public bool Equals(Nested x, Nested y)
    {
        if (x.Count != y.Count)
        {
            return false;
        }

        foreach (var key in x.Keys)
        {
            if (!y.TryGetValue(key, out var value) || !value.Equals(x[key]))
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(Nested obj)
    {
        unchecked
        {
            int hash = 17;
            foreach (var pair in obj)
            {
                hash = hash * 23 + pair.Key.GetHashCode();
                hash = hash * 23 + (pair.Value?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }
}
