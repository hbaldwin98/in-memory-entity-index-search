using Indexer.Models;

namespace Indexer;

public class Database 
{
    private readonly Dictionary<Type, IPageIndex<object>> _indices;

    public Database()
    {
        _indices = new Dictionary<Type, IPageIndex<object>>();
    }

    public void Index<T>(IEnumerable<T> entities) where T : class
    {
        if (!_indices.ContainsKey(typeof(T)))
        {
            var index = new PageIndex<object>();
            index.Index(entities, null);
            
            _indices.Add(typeof(T), index);
            
        }
        else
        {
            _indices[typeof(T)].Index(entities, null);
        }
    }

    public void Index<T>(object entity) where T : class
    {
        if (!_indices.ContainsKey(typeof(T)))
        {
            _indices.Add(typeof(T), new PageIndex<object>());
        }
        else
        {
            _indices[typeof(T)].Index(entity, null);
        }
    }

    public T Get<T>(int index) where T : class
    {
        if (!_indices.ContainsKey(typeof(T)))
        {
            return default;
        }
        else
        {
           return (T)_indices[typeof(T)].GetEntity(index);
        }
    }
}
