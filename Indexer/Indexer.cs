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

    public class Indexer<T> : IIndexer<T> where T : Base
    {
        private readonly Dictionary<string, Dictionary<string, HashSet<T>>> _index = new Dictionary<string, Dictionary<string, HashSet<T>>>();
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
        #endregion
        #region Searching
        public IEnumerable<T> Search(List<ComplexSearch> complexSearches)
        {
            HashSet<T> matchingEntities = new HashSet<T>();

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
            if (filters == null)
            {
                return GetHashSet();
            }

            // Calculate the number of matching entities for each filter without retrieving the entities
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
                if (entities != null && entities.Count == 0)
                {
                    break;
                }
            }

            return entities ?? GetHashSet();
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
                    foreach (var item in matches)
                    {
                        currentEntities.Add(item);
                    }
                }
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
        #endregion
    }
}
