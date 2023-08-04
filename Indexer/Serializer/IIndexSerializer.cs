using Indexer.Models;
using System.Text.Json;

namespace Indexer.Serializer;

public interface IIndexSerializer<T> : IDisposable where T : IBaseEntity
{
    ReadOnlyMemory<byte> SerializeToMemory(T obj);
    JsonDocument SerializeToDocument(T obj);
}
