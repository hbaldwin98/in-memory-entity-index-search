using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Bogus;
using Indexer;
using Indexer.Models;
using Database = Indexer.Database;

public class Program
{
    public static void Main(string[] args)
    {
        //BenchmarkRunner.Run<IndexingBenchmark>();
        BenchmarkRunner.Run<SearchingBenchmark>();
    }
}

[MemoryDiagnoser]
public class IndexingBenchmark
{
    [Params(100, 1000, 10000)]
    public int NumberOfEntities;

    private List<BaseEntity> _entities;

    private IEnumerable<BaseEntity> CreateEntities(int v)
    {
        var faker = new Faker();

        for (var i = 0; i < v; i++)
        {
            yield return new BaseEntity
            {
                Attributes = new Dictionary<string, string>
                {
                    { faker.Music.Random.String(), faker.Music.Random.String() },
                    { faker.Music.Random.String(), faker.Music.Random.String() },
                    { faker.Music.Random.String(), faker.Music.Random.String() },
                    { faker.Music.Random.String(), faker.Music.Random.String() }
                },
                Meta = new Dictionary<string, List<Meta>>
                {
                    { faker.Music.Random.String(), new List<Meta> { new Meta { { faker.Rant.Random.String(), faker.Rant.Random.String() } } } },
                    { faker.Music.Random.String(), new List<Meta> { new Meta { { faker.Rant.Random.String(), faker.Rant.Random.String() } } } },
                    { faker.Music.Random.String(), new List<Meta> { new Meta { { faker.Rant.Random.String(), faker.Rant.Random.String() } } } },
                    { faker.Music.Random.String(), new List<Meta> { new Meta { { faker.Rant.Random.String(), faker.Rant.Random.String() } } } }
                }
            };
        }
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _entities = CreateEntities(NumberOfEntities).ToList();
    }

    [Benchmark]
    public void IndexEntities()
    {
        var database = new Database();
        database.Index(_entities);
    }
}

[MemoryDiagnoser]
public class SearchingBenchmark
{
    [Params(100, 1000, 10000)]
    public int NumberOfEntities;

    private Database _database;
    private Faker faker;


    [GlobalSetup]
    public void GlobalSetup()
    {
        var entities = new List<BaseEntity>();
        faker = new Faker();

        for (var i = 0; i < NumberOfEntities; i++)
        {
            entities.Add(new BaseEntity
            {
                Attributes = new Dictionary<string, string>
                {
                    { faker.Music.Random.String(), faker.Music.Random.String() },
                    { faker.Music.Random.String(), faker.Music.Random.String() },
                    { faker.Music.Random.String(), faker.Music.Random.String() },
                    { faker.Music.Random.String(), faker.Music.Random.String() }
                },
                Meta = new Dictionary<string, List<Meta>>
                {
                    { faker.Music.Random.String(), new List<Meta> { new Meta { { faker.Rant.Random.String(), faker.Rant.Random.String() } } } },
                    { faker.Music.Random.String(), new List<Meta> { new Meta { { faker.Rant.Random.String(), faker.Rant.Random.String() } } } },
                    { faker.Music.Random.String(), new List<Meta> { new Meta { { faker.Rant.Random.String(), faker.Rant.Random.String() } } } },
                    { faker.Music.Random.String(), new List<Meta> { new Meta { { faker.Rant.Random.String(), faker.Rant.Random.String() } } } }
                }
            });
        }

        _database = new Database();
        _database.Index(entities);
    }

    [Benchmark]
    public void Search()
    {
        var search = new ComplexSearch
        {
            OneOf = new List<SearchFilter>
            {
                new SearchFilter($"attributes.{faker.Music.Random.String()}", new List<string> { faker.Music.Random.String() }),
                new SearchFilter($"meta.{faker.Music.Random.String()}", new List<string> { faker.Music.Random.String() })
            },
            NotOneOf = new List<SearchFilter>
            {
               new SearchFilter("foo", new List<string> { "bar" })
            }
        };

        _database.Search<BaseEntity>(new List<ComplexSearch> { search });
    }
}
