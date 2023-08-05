using Indexer.Models;
using System.Text.Json;

namespace Indexer.Serializer;

public interface IIndexSerializer : IDisposable
{
    ReadOnlyMemory<byte> SerializeToMemory(object obj);
    JsonDocument SerializeToDocument(object obj);
}
