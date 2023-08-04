using Indexer.Models;
using Indexer.Serializer;
using System.Buffers;
using System.Text.Json;

namespace Indexer;

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
