using Indexer;
using Indexer.Extensions;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

//var newUser = new User
//{
//    Tag = new Dictionary<string, List<string>> { { "test", new List<string> { "test2" } } },
//    Facility = new Facility { Tag = new List<string> { "Poop" } }
//};

//var newUser2 = new User
//{
//    Id = 12345,
//    Tag = new Dictionary<string, List<string>> { { "test", new List<string> { "test", "test3" } } },
//    Meta = new Dictionary<string, List<MetaObjectConfig>> { { "imtFacility", new List<MetaObjectConfig> { new MetaObjectConfig { Tag = new Dictionary<string, List<string>> { { "Test", new List<string> { "Test", "Poop" } } } } } } }
//};

//var indexer = new Indexer<User>();

//indexer.Add(newUser);
//indexer.Add(newUser2);

//var results = indexer.Search(new List<SearchFilter> { new SearchFilter { Field = "meta.imtFacility.Test", Values = new List<string> { "Test", "Poop" } } });

//Console.ReadLine();


public class ComplexSearch
{
    public List<SearchFilter> OneOf { get; set; }
    public List<SearchFilter> NotOneOf { get; set; }
}

public class SearchFilter
{
    public string Field { get; set; }
    public List<string> Values { get; set; }
}
public class TestObject : Base
{
    public string Name { get; set; }
    public int Age { get; set; }
}
class Program
{
    static void Main(string[] args)
    {
        var testObject = new TestObject
        {
            Id = "12345",
            Name = "Test",
            Age = 123
        };

        var indexer = new Indexer<TestObject>();

        indexer.Index(testObject);

        indexer.ExportToCsv("test.csv");
    }
}

public class MetaObjectConfigConverter : JsonConverter<MetaObjectConfig>
{
    public override MetaObjectConfig Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, MetaObjectConfig value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Tag != null)
        {
            foreach (var tagEntry in value.Tag)
            {
                writer.WritePropertyName(tagEntry.Key);
                JsonSerializer.Serialize(writer, tagEntry.Value, options);
            }
        }
        writer.WriteEndObject();
    }
}

public class Base
{
    public string Id { get; set; }
    public Dictionary<string, List<string>> Tag { get; set; }
    public Dictionary<string, List<MetaObjectConfig>> Meta { get; set; }
    public Dictionary<string, string> Attributes { get; set; }
}
public class User : Base
{
    public Facility Facility { get; set; }
}

public class Facility
{
    public List<string> Tag { get; set; }
}
public class MetaObjectConfig : Dictionary<string, string>
{
    [JsonPropertyName("tag")]
    public Dictionary<string, List<string>> Tag { get; set; }
}

