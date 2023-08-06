using Indexer.Models;

namespace Indexer;

public enum DbStatus
{
    SUCCESS,
    FAILURE
}

public class DbResult<T>
{
    public T Result { get; set; }
    public DbStatus Status { get; set; }
    public string Message { get; set; }

    public DbResult(DbStatus status)
    {
        Status = status;
    }

    public DbResult(DbStatus status, string message)
    {
        Status = status;
        Message = message;
    }

    public DbResult(T result)
    {
        Result = result;
        Status = DbStatus.SUCCESS;
    }

    public DbResult(T result, DbStatus status)
    {
        Result = result;
        Status = status;
    }
}

public class Accessor
{
    public DateTime AccessTime { get; }
    public Dictionary<string, string> Metadata { get; set; }

    public Accessor()
    {
        AccessTime = DateTime.UtcNow;
        Metadata = new Dictionary<string, string>();
    }

    public Accessor(Dictionary<string, string> metadata)
    {
        AccessTime = DateTime.UtcNow;
        Metadata = metadata;
    }
}

public class Database
{
    private readonly Dictionary<Type, IPageIndex<object>> _indices;
    private List<Accessor> _accessors;
    private Dictionary<Type, DateTime> _lastAccessed;

    public Database()
    {
        _indices = new Dictionary<Type, IPageIndex<object>>();
        _lastAccessed = new Dictionary<Type, DateTime>();
        _accessors = new List<Accessor>();
    }

    private T WithContext<T>(Func<Accessor, T> databaseCall, Accessor accessor)
    {
        _accessors.Add(accessor);
        T result = databaseCall(accessor);
        _accessors.Remove(accessor);

        _lastAccessed[typeof(T)] = DateTime.UtcNow;
        return result;
    }

    private async Task<T> WithContextAsync<T>(Func<Accessor, Task<T>> databaseCall, Accessor accessor)
    {
        _accessors.Add(accessor);
        T result = await databaseCall(accessor);
        _accessors.Remove(accessor);

        _lastAccessed[typeof(T)] = DateTime.UtcNow;
        return result;
    }

    public DbResult<T> Index<T>(IEnumerable<T> entities) where T : class
    {
        return Index(entities, new Accessor());
    }

    public async Task<DbResult<T>> IndexAsync<T>(IEnumerable<T> entities) where T : class
    {
        return await IndexAsync(entities, new Accessor());
    }

    public DbResult<T> Index<T>(object entity) where T : class
    {
        return Index<T>(entity, new Accessor());
    }
    public async Task<DbResult<T>> IndexAsync<T>(object entity) where T : class
    {
        return await IndexAsync<T>(entity, new Accessor());
    }

    public DbResult<T> Get<T>(int index) where T : class
    {
        return Get<T>(index, new Accessor());
    }
    
    public async Task<DbResult<T>> GetAsync<T>(int index) where T : class
    {
        return await GetAsync<T>(index, new Accessor());
    }

    public DbResult<IEnumerable<T>> Search<T>(IEnumerable<ComplexSearch> searches)
    {
        return Search<T>(searches, new Accessor());
    }

    public async Task<DbResult<IEnumerable<T>>> SearchAsync<T>(IEnumerable<ComplexSearch> searches)
    {
        return await SearchAsync<T>(searches, new Accessor());
    }

    private DbResult<T> Index<T>(IEnumerable<T> entities, Accessor accessor) where T : class
    {
        return WithContext(a =>
        {
            if (!_indices.ContainsKey(typeof(T)))
            {
                var index = new PageIndex<object>();
                index.Index(entities);

                _indices.Add(typeof(T), index);
            }
            else
            {
                _indices[typeof(T)].Index(entities);
            }

            return new DbResult<T>(DbStatus.SUCCESS);
        }, accessor);
    }

    private async Task<DbResult<T>> IndexAsync<T>(IEnumerable<T> entities, Accessor accessor) where T : class
    {
        return await WithContextAsync(async a =>
        {
            if (!_indices.ContainsKey(typeof(T)))
            {
                var index = new PageIndex<object>();
                await index.IndexAsync(entities);

                _indices.Add(typeof(T), index);
            }
            else
            {
                var index = _indices[typeof(T)];
                await index.IndexAsync(entities);
            }

            return new DbResult<T>(DbStatus.SUCCESS);
        }, accessor);
    }

    private DbResult<T> Index<T>(object entity, Accessor accessor) where T : class
    {
        return WithContext(a =>
        {
            if (!_indices.ContainsKey(typeof(T)))
            {
                _indices.Add(typeof(T), new PageIndex<object>());
            }

            _indices[typeof(T)].Index(entity);

            return new DbResult<T>(DbStatus.SUCCESS);
        }, accessor);
    }

    private async Task<DbResult<T>> IndexAsync<T>(object entity, Accessor accessor) where T : class
    {
        return await WithContextAsync(async a =>
        {
            if (!_indices.ContainsKey(typeof(T)))
            {
                _indices.Add(typeof(T), new PageIndex<object>());
            }

            await _indices[typeof(T)].IndexAsync(entity);

            return new DbResult<T>(DbStatus.SUCCESS);
        }, accessor);
    }

    private DbResult<T> Get<T>(int index, Accessor accessor) where T : class
    {
        return WithContext(a =>
        {
            if (!_indices.ContainsKey(typeof(T)))
            {
                return new DbResult<T>(DbStatus.FAILURE, "Type does not have an index.");
            }
            else
            {
                return new DbResult<T>((T)_indices[typeof(T)].GetEntity(index));
            }
        }, accessor);
    }

    private async Task<DbResult<T>> GetAsync<T>(int index, Accessor accessor) where T : class
    {
        return await WithContextAsync(async a =>
        {
            if (!_indices.ContainsKey(typeof(T)))
            {
                return new DbResult<T>(DbStatus.FAILURE, "Type does not have an index.");
            }
            else
            {
                return new DbResult<T>((T)_indices[typeof(T)].GetEntity(index));
            }
        }, accessor);
    }

    private DbResult<IEnumerable<T>> Search<T>(IEnumerable<ComplexSearch> searches, Accessor accessor)
    {
        return WithContext(a =>
        {
            if (!_indices.ContainsKey(typeof(T)))
            {
                return new DbResult<IEnumerable<T>>(DbStatus.FAILURE, "Type does not have an index.");
            }
            else
            {
                return new DbResult<IEnumerable<T>>(_indices[typeof(T)].Search(searches).Select(e => (T)e));
            }
        }, accessor);
    }

    private async Task<DbResult<IEnumerable<T>>> SearchAsync<T>(IEnumerable<ComplexSearch> searches, Accessor accessor)
    {
        return await WithContextAsync(async a =>
        {
            if (!_indices.ContainsKey(typeof(T)))
            {
                return new DbResult<IEnumerable<T>>(DbStatus.FAILURE, "Type does not have an index.");
            }
            else
            {
                var result = await _indices[typeof(T)].SearchAsync(searches);
                return new DbResult<IEnumerable<T>>(result.Select(e => (T)e));
            }
        }, accessor);
    }
}
