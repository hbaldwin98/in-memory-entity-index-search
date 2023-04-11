using Indexer;
using Indexer.Extensions;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
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

public class Base
{
    public string Id { get; set; }
    public Dictionary<string, List<string>> Tag { get; set; }
    public Dictionary<string, List<MetaObjectConfig>> Meta { get; set; }
    public Dictionary<string, string> Attributes { get; set; }
}
public class MetaObjectConfig : Dictionary<string, string>
{
}

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

public class IndexService
{
    private readonly Dictionary<Type, Dictionary<string, List<object>>> _index;

    public IndexService()
    {
        _index = new Dictionary<Type, Dictionary<string, List<object>>>();
    }

    public void Index<T>(T obj)
    {
        IndexObject(typeof(T), obj, "");
    }

    public IEnumerable<T> Search<T>(string propertyName, object value)
    {
        if (!_index.ContainsKey(typeof(T)))
        {
            return Enumerable.Empty<T>();
        }

        var objects = _index[typeof(T)];
        var matches = objects.Where(pair => pair.Key.EndsWith(propertyName))
                            .SelectMany(pair => pair.Value.Where(o => o.Equals(value)).Cast<T>());
        return matches;
    }

    private void IndexObject(Type type, object obj, string path)
    {
        if (obj == null)
        {
            return;
        }

        if (!_index.ContainsKey(type))
        {
            _index[type] = new Dictionary<string, List<object>>();
        }

        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (var property in properties)
        {
            if (property.Name == "Id" || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var propertyValue = property.GetValue(obj);
            if (propertyValue == null)
            {
                continue;
            }

            var key = $"{path}{property.Name}";
            if (IsCollectionType(propertyValue))
            {
                var collection = propertyValue as IEnumerable<object>;
                foreach (var item in collection)
                {
                    IndexObject(item.GetType(), item, $"{key}.");
                }
            }
            else if (IsDictionaryType(propertyValue))
            {
                var dictionary = propertyValue as IDictionary;
                foreach (var entryKey in dictionary.Keys)
                {
                    var entryValue = dictionary[entryKey];
                    var entryPath = $"{key}.{entryKey}";
                    if (IsCollectionType(entryValue))
                    {
                        var collection = entryValue as IEnumerable<object>;
                        foreach (var item in collection)
                        {
                            IndexObject(item.GetType(), item, $"{entryPath}.");
                        }
                    }
                    else if (IsDictionaryType(entryValue))
                    {
                        IndexObject(entryValue.GetType(), entryValue, entryPath + ".");
                    }
                    else
                    {
                        if (!_index[type].ContainsKey(entryPath))
                        {
                            _index[type][entryPath] = new List<object>();
                        }

                        _index[type][entryPath].Add(entryValue);
                    }
                }
            }
            else if (!property.PropertyType.IsPrimitive && property.PropertyType != typeof(string))
            {
                // Property is not a primitive type or string, so we index its properties recursively.
                IndexObject(property.PropertyType, propertyValue, $"{key}.");
            }
            else
            {
                if (!_index[type].ContainsKey(key))
                {
                    _index[type][key] = new List<object>();
                }

                if (IsCollectionType(propertyValue))
                {
                    _index[type][key].AddRange(propertyValue as IEnumerable<object>);
                }
                else
                {
                    _index[type][key].Add(propertyValue);
                }
            }
        }
    }

    private bool IsCollectionType(object obj)
    {
        return obj is IEnumerable<object> && !(obj is string);
    }

    private bool IsDictionaryType(object obj)
    {
        return obj is IDictionary;
    }
}



class Program
{
    static void Main(string[] args)
    {
        var testObject = new Base
        {
            Id = "1",
            Tag = new Dictionary<string, List<string>>
    {
        {"foo", new List<string>{"bar", "baz"}},
        {"qux", new List<string>{"corge"}}
    },
            Meta = new Dictionary<string, List<MetaObjectConfig>>
    {
        {
            "abc", new List<MetaObjectConfig>
            {
                new MetaObjectConfig
                {
                    {"prop1", "val1"},
                    {"prop2", "val2"}
                },
                new MetaObjectConfig
                {
                    {"prop1", "val3"},
                    {"prop2", "val4"}
                }
            }
        }
    },
            Attributes = new Dictionary<string, string>
    {
        {"attr1", "val1"},
        {"attr2", "val2"}
    }
        };

        var testObject2 = new Base
        {
            Id = "1",
            Tag = new Dictionary<string, List<string>>
    {
        {"foo", new List<string>{"bar", "bez"}},
        {"qux", new List<string>{"corge"}}
    },
            Meta = new Dictionary<string, List<MetaObjectConfig>>
    {
        {
            "abc", new List<MetaObjectConfig>
            {
                new MetaObjectConfig
                {
                    {"prop1", "val1"},
                    {"prop2", "val2"}
                },
                new MetaObjectConfig
                {
                    {"prop1", "val3"},
                    {"prop2", "val4"}
                }
            }
        }
    },
            Attributes = new Dictionary<string, string>
    {
        {"attr1", "val1"},
        {"attr2", "val2"}
    }
        };

        var indexService = new IndexService();
        indexService.Index(testObject);
        //var options = new JsonSerializerOptions
        //{
        //    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        //    WriteIndented = true
        //};
        //var jsonString = JsonSerializer.Serialize(testObject, options);
        //var jsonString2 = JsonSerializer.Serialize(testObject2, options);

        //jsonString = "[" + jsonString + "]";

        //jsonString2 = "[" + jsonString2 + "]";
        //var comparer = new JsonComparer();
        //comparer.CompareFiles(jsonString, jsonString2);


        //Console.WriteLine(comparer.ToString());

        //foreach (var dict in compare)
        //{
        //    foreach (var kvp in dict)
        //    {
        //        foreach (var kvp2 in kvp.Value)
        //        {
        //            Console.WriteLine($"{kvp.Key} - {kvp2.Key}: ({kvp2.Value.Item1} - {kvp2.Value.Item2})");
        //        }
        //    }
        //}

        //var indexer = new Indexer<TestObject>();

        //indexer.Index(testObject);

        //indexer.ExportToCsv("test.csv");
    }

    public class User : Base
    {
        public Facility Facility { get; set; }
    }

    public class Facility
    {
        public List<string> Tag { get; set; }
    }

}