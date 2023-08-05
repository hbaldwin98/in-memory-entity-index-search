using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Bogus;
using Indexer;
using Indexer.Models;

public class Program
{
    public static void Main(string[] args)
    {
        //BenchmarkRunner.Run<IndexerBenchmark>();
    }
}

[MemoryDiagnoser]
public class IndexerBenchmark
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
        var indexer = new Indexer<BaseEntity>();

        indexer.Index(_entities);
    }
}
