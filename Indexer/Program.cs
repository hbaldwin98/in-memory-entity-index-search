using Indexer.Models;

namespace Indexer;

public class Program
{
    public static void Main(string[] args)
    {
        var indexService = new Indexer<BaseEntity>();

        var testObject = new BaseEntity
        {
            Id = "1",
            Tag = new Dictionary<string, List<string>>
            {
                {"foo", new List<string>{"bar", "baz"}},
                {"qux", new List<string>{"corge"}}
            },
            Meta = new Dictionary<string, List<Meta>>
            {
                {
                    "facility", new List<Meta>
                    {
                        new Meta
                        {
                            { "facility", "1234" },
                            { "region", "BURBON" },
                        },
                        new Meta
                        {
                            { "facility", "1234" },
                            { "region", "AMISH" },
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
        var testObject2 = new BaseEntity
        {
            Id = "2",
            Tag = new Dictionary<string, List<string>>
            {
                {"foo", new List<string>{"bar", "bez"}},
                {"qux", new List<string>{"corge"}}
            },
            Meta = new Dictionary<string, List<Meta>>
            {
                {
                    "facility", new List<Meta>
                    {
                        new Meta
                        {
                            { "facility", "1234" },
                            { "region", "BURBON" },
                        },
                        new Meta
                        {
                            { "facility", "1233" },
                            { "region", "BURBON" },
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

        indexService.Index(testObject);
        indexService.Index(testObject2);

        var searchResult = indexService.Search(new List<ComplexSearch>
        {
            new ComplexSearch
            {
                  OneOf = new List<SearchFilter>
                  {
                      new SearchFilter
                      {
                          Field = "meta.facility.facility",
                          Values = new List<string> { "1234"},
                          NestedPrefix = "meta.facility"
                      },
                    new SearchFilter
                      {
                          Field = "meta.facility.region",
                          Values = new List<string> { "AMISH"},
                          NestedPrefix = "meta.facility"
                      }
                  }

            }
        });

        Console.Read();
    }
}