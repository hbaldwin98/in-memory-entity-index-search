using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Indexer
{
    public interface IIndexer<T> : IDisposable where T : Base
    {
        void Index(T obj, string path = null);
        void Index(List<T> entities, string path = null);
        void Index(JsonElement jsonElement, string path, T obj);
        IEnumerable<T> Search(List<ComplexSearch> complexSearches);
        Dictionary<string, Dictionary<string, HashSet<T>>> GetIndex();
    }

    public interface IIndexSerializer<T> : IDisposable where T : Base
    {
        ReadOnlyMemory<byte> SerializeToMemory(T obj);
        JsonDocument SerializeToDocument(T obj);

    }

    public class JsonDocumentSerializer<T> : IIndexSerializer<T>, IDisposable where T : Base
    {
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        private readonly ArrayBufferWriter<byte> _bufferWriter = new ArrayBufferWriter<byte>(16384);
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
    
    public class Indexer<T> : IIndexer<T> where T : Base
    {
        private readonly Dictionary<string, Dictionary<string, HashSet<T>>> _index = new Dictionary<string, Dictionary<string, HashSet<T>>>();
        private readonly IIndexSerializer<T> _serializer;

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

        public void Index(T obj, string path = null)
        {
            //using (var document = JsonDocument.Parse(_serializer.SerializeToMemory(obj)))
            //{
            //    Index(document.RootElement, path, obj);
            //}
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
                        matches = new HashSet<T>();
                        index[path] = matches;
                    }
                    matches.Add(obj);
                    break;
            }
        }

        private void IndexObject(JsonElement jsonElement, string path, T obj)
        {
            foreach (JsonProperty property in jsonElement.EnumerateObject())
            {
                string propertyPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
                Index(property.Value, propertyPath, obj);
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
        public IEnumerable<T> Search(List<ComplexSearch> complexSearches)
        {
            HashSet<T> matchingEntities = new HashSet<T>();
            var emptyFilters = Enumerable.Empty<SearchFilter>();

            foreach (var complexSearch in complexSearches)
            {
                var oneOfMatches = GetEntities(complexSearch.OneOf ?? emptyFilters);
                var notOneOfMatches = GetEntities(complexSearch.NotOneOf ?? emptyFilters);

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
            if (filters == null)
            {
                return GetHashSet();
            }

            HashSet<T> entities = null;
            foreach (var filter in filters)
            {
                HashSet<T> currentEntities = GetEntitiesFromFilter(filter);
                if (currentEntities == null)
                {
                    break;
                }

                entities = ProcessEntities(entities, currentEntities);
                if (entities != null && entities.Count == 0)
                {
                    break;
                }
            }

            entities ??= GetHashSet();
            return entities;
        }

        private HashSet<T> GetEntitiesFromFilter(SearchFilter filter)
        {
            HashSet<T> currentEntities = null;
            bool noMatches = true;
            foreach (var value in filter.Values)
            {
                if (_index.TryGetValue(value, out var pathIndex) && pathIndex.TryGetValue(filter.Field, out var matches))
                {
                    noMatches = false;
                    currentEntities ??= GetHashSet();
                    currentEntities.UnionWith(matches);
                }
            }

            if (noMatches)
            {
                ReturnHashSet(currentEntities);
                return GetHashSet();
            }

            return currentEntities;
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

        private readonly ConcurrentStack<HashSet<T>> _hashSetPool = new ConcurrentStack<HashSet<T>>();
        private HashSet<T> GetHashSet()
        {
            if (_hashSetPool.TryPop(out var hashSet))
            {
                hashSet.Clear();
                return hashSet;
            }
            return new HashSet<T>();
        }

        private void ReturnHashSet(HashSet<T> hashSet)
        {
            _hashSetPool.Push(hashSet);
        }

        public Dictionary<string, Dictionary<string, HashSet<T>>> GetIndex()
        {
            return _index;
        }
    }
    
    //public class Indexer<T> : IDisposable where T : Base
    //{
    //    private readonly Dictionary<string, Dictionary<string, HashSet<T>>> _index = new Dictionary<string, Dictionary<string, HashSet<T>>>();
    //    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    //    {
    //        IncludeFields = true,
    //        PropertyNameCaseInsensitive = true,
    //        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    //    };
    //    private readonly ArrayBufferWriter<byte> _bufferWriter = new ArrayBufferWriter<byte>();
    //    private readonly Utf8JsonWriter _jsonWriter;

    //    public Indexer()
    //    {
    //        _jsonWriter = new Utf8JsonWriter(_bufferWriter);
    //    }

    //    public Indexer(List<T> entities) : this()
    //    {
    //        Index(entities);
    //    }

    //    ~Indexer()
    //    {
    //        Dispose(false);
    //    }

    //    public void Dispose()
    //    {
    //        Dispose(true);
    //        GC.SuppressFinalize(this);
    //    }

    //    protected virtual void Dispose(bool disposing)
    //    {
    //        if (disposing)
    //        {
    //            _jsonWriter?.Dispose();
    //        }
    //    }

    //    public void Index(T obj, string path = null)
    //    {
    //        _bufferWriter.Clear();
    //        _jsonWriter.Reset(_bufferWriter);

    //        JsonSerializer.Serialize(_jsonWriter, obj, _jsonOptions);

    //        using (var document = JsonDocument.Parse(_bufferWriter.WrittenMemory))
    //        {
    //            Index(document.RootElement, path, obj);
    //        }
    //    }


    //    public void Index(List<T> entities, string path = null)
    //    {
    //        foreach (var entity in entities)
    //        {
    //            Index(entity, path);
    //        }
    //    }
    //    public void Index(JsonElement jsonElement, string path, T obj)
    //    {
    //        switch (jsonElement.ValueKind)
    //        {
    //            case JsonValueKind.Object:
    //                IndexObject(jsonElement, path, obj);
    //                break;
    //            case JsonValueKind.Array:
    //                IndexArray(jsonElement, path, obj);
    //                break;
    //            case JsonValueKind.Null:
    //            case JsonValueKind.Undefined:
    //                break;
    //            case JsonValueKind.String:
    //            case JsonValueKind.Number:
    //            case JsonValueKind.False:
    //            case JsonValueKind.True:
    //                string term = jsonElement.ToString();
    //                if (!_index.TryGetValue(term, out var index))
    //                {
    //                    index = new Dictionary<string, HashSet<T>>();
    //                    _index[term] = index;
    //                }
    //                if (!index.TryGetValue(path, out var matches))
    //                {
    //                    matches = new HashSet<T>();
    //                    index[path] = matches;
    //                }
    //                matches.Add(obj);
    //                break;
    //        }
    //    }

    //    private void IndexObject(JsonElement jsonElement, string path, T obj)
    //    {
    //        foreach (JsonProperty property in jsonElement.EnumerateObject())
    //        {
    //            string propertyPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
    //            Index(property.Value, propertyPath, obj);
    //        }
    //    }

    //    private void IndexArray(JsonElement jsonElement, string path, T obj)
    //    {
    //        string itemPath = string.IsNullOrEmpty(path) ? "" : path;
    //        foreach (JsonElement item in jsonElement.EnumerateArray())
    //        {
    //            Index(item, itemPath, obj);
    //        }
    //    }
    //    public IEnumerable<T> Search(List<ComplexSearch> complexSearches)
    //    {
    //        HashSet<T> matchingEntities = new HashSet<T>();
    //        var emptyFilters = Enumerable.Empty<SearchFilter>();

    //        foreach (var complexSearch in complexSearches)
    //        {
    //            var oneOfMatches = GetEntities(complexSearch.OneOf ?? emptyFilters);
    //            var notOneOfMatches = GetEntities(complexSearch.NotOneOf ?? emptyFilters);

    //            if (notOneOfMatches.Count > 0)
    //            {
    //                oneOfMatches.ExceptWith(notOneOfMatches);
    //            }

    //            matchingEntities.UnionWith(oneOfMatches);

    //            ReturnHashSet(oneOfMatches);
    //            ReturnHashSet(notOneOfMatches);
    //        }

    //        return matchingEntities;
    //    }

    //    private HashSet<T> GetEntities(IEnumerable<SearchFilter> filters)
    //    {
    //        if (filters == null)
    //        {
    //            return GetHashSet();
    //        }

    //        HashSet<T> entities = null;
    //        foreach (var filter in filters)
    //        {
    //            HashSet<T> currentEntities = GetEntitiesFromFilter(filter);
    //            if (currentEntities == null)
    //            {
    //                break;
    //            }

    //            entities = ProcessEntities(entities, currentEntities);
    //            if (entities != null && entities.Count == 0)
    //            {
    //                break;
    //            }
    //        }

    //        entities ??= GetHashSet();
    //        return entities;
    //    }

    //    private HashSet<T> GetEntitiesFromFilter(SearchFilter filter)
    //    {
    //        HashSet<T> currentEntities = null;
    //        bool noMatches = true;
    //        foreach (var value in filter.Values)
    //        {
    //            if (_index.TryGetValue(value, out var pathIndex) && pathIndex.TryGetValue(filter.Field, out var matches))
    //            {
    //                noMatches = false;
    //                currentEntities ??= GetHashSet();
    //                currentEntities.UnionWith(matches);
    //            }
    //        }

    //        if (noMatches)
    //        {
    //            ReturnHashSet(currentEntities);
    //            return GetHashSet();
    //        }

    //        return currentEntities;
    //    }

    //    private HashSet<T> ProcessEntities(HashSet<T> entities, HashSet<T> currentEntities)
    //    {
    //        if (entities == null)
    //        {
    //            entities = currentEntities;
    //        }
    //        else
    //        {
    //            entities.IntersectWith(currentEntities);
    //            ReturnHashSet(currentEntities);
    //        }
    //        return entities;
    //    }

    //    private readonly ConcurrentStack<HashSet<T>> _hashSetPool = new ConcurrentStack<HashSet<T>>();
    //    private HashSet<T> GetHashSet()
    //    {
    //        if (_hashSetPool.TryPop(out var hashSet))
    //        {
    //            hashSet.Clear();
    //            return hashSet;
    //        }
    //        return new HashSet<T>();
    //    }

    //    private void ReturnHashSet(HashSet<T> hashSet)
    //    {
    //        _hashSetPool.Push(hashSet);
    //    }

    //    public Dictionary<string, Dictionary<string, HashSet<T>>> GetIndex()
    //    {
    //        return _index;
    //    }
    //}
}