using Indexer.Extensions;
using Indexer.Models;
using Indexer.Tests.Models;
using NUnit.Framework;

namespace Indexer.Tests.IndexerTests;

[TestFixture]
public class IndexTests : BaseTest
{
    public IndexTests() : base()
    {
    }

    [Test]
    public void Database_Test()
    {
        var database = new Database();

        database.Index(new TestEntity[] { new TestEntity { Id = "1" }, new TestEntity { Id = "2" } });
        database.Index(new BaseEntity[]
        {
            new BaseEntity
            {
                Id = "3"
            }
        });

        var result = database.Get<TestEntity>(1);
        var result2 = database.Get<BaseEntity>(0);

        Assert.AreEqual(result.Status, DbStatus.SUCCESS);
        Assert.AreEqual(result2.Status, DbStatus.SUCCESS);

        Assert.NotNull(result.Result);
        Assert.NotNull(result2.Result);
        Assert.AreEqual(result.Result.Id, "2");
        Assert.AreEqual(result2.Result.Id, "3");
        Assert.True(result.Result.GetType() == typeof(TestEntity));
        Assert.True(result2.Result.GetType() == typeof(BaseEntity));
    }

    [Test]
    public async Task Async_Database_Test()
    {
        var database = new Database();

        await database.IndexAsync(new TestEntity[] { new TestEntity { Id = "1" }, new TestEntity { Id = "2" } });
        await database.IndexAsync(new BaseEntity[]
        {
            new BaseEntity
            {
                Id = "3"
            }
        });

        var result = await database.GetAsync<TestEntity>(1);
        var result2 = await database.GetAsync<BaseEntity>(0);

        Assert.AreEqual(result.Status, DbStatus.SUCCESS);
        Assert.AreEqual(result2.Status, DbStatus.SUCCESS);

        Assert.NotNull(result.Result);
        Assert.NotNull(result2.Result);
        Assert.AreEqual(result.Result.Id, "2");
        Assert.AreEqual(result2.Result.Id, "3");
        Assert.True(result.Result.GetType() == typeof(TestEntity));
        Assert.True(result2.Result.GetType() == typeof(BaseEntity));
    }

    [Theory]
    public void Index_WithSingleEntity_ShouldIndexCorrectly()
    {
        // Arrange
        var indexer = new PageIndex<TestEntity>();
        var entity = new TestEntity
        {
            Id = "1",
            Property1 = "foo",
            Property2 = 42,
            Property3 = true,
            Property4 = new List<string> { "bar", "baz" },
            Property5 = new NestedObject { NestedProperty1 = "qux", NestedProperty2 = 13 },
            Property6 = new List<NestedObject> { new NestedObject { NestedProperty1 = "quux", NestedProperty2 = 17 } }
        };

        // Act
        indexer.Index(entity);

        // Assert
        Assert.AreEqual(1, indexer.GetMatches("foo", "property1").Count());
        Assert.AreEqual(1, indexer.GetMatches("42", "property2").Count());
        Assert.AreEqual(1, indexer.GetMatches("True", "property3").Count());
        Assert.AreEqual(1, indexer.GetMatches("bar", "property4").Count());
        Assert.AreEqual(1, indexer.GetMatches("baz", "property4").Count());
        Assert.AreEqual(1, indexer.GetMatches("qux", "property5.nestedProperty1").Count());
        Assert.AreEqual(1, indexer.GetMatches("13", "property5.nestedProperty2").Count());
        Assert.AreEqual(1, indexer.GetMatches("quux", "property6.nestedProperty1").Count());
        Assert.AreEqual(1, indexer.GetMatches("17", "property6.nestedProperty2").Count());
    }

    [Theory]
    public void Index_WithMultipleEntities_ShouldIndexCorrectly()
    {
        // Arrange
        var indexer = new PageIndex<TestEntity>();
        var entities = new List<TestEntity>
    {
        new TestEntity { Id = "1", Property1 = "foo" },
        new TestEntity { Id = "2", Property1 = "bar" },
        new TestEntity { Id = "3", Property1 = "baz" }
    };

        // Act
        indexer.Index(entities);

        // Assert
        Assert.AreEqual(1, indexer.GetMatches("foo", "property1").Count());
        Assert.AreEqual(1, indexer.GetMatches("bar", "property1").Count());
        Assert.AreEqual(1, indexer.GetMatches("baz", "property1").Count());
    }

    [Theory]
    public void Index_WithNestedObjects_ShouldIndexCorrectly()
    {
        // Arrange
        var indexer = new PageIndex<TestEntity>();
        var entity = new TestEntity
        {
            Id = "1",
            Property5 = new NestedObject { NestedProperty1 = "foo", NestedProperty2 = 42 },
            Property6 = new List<NestedObject> { new NestedObject { NestedProperty1 = "bar", NestedProperty2 = 13 } }
        };

        // Act
        indexer.Index(entity);

        // Assert
        Assert.AreEqual(1, indexer.GetMatches("foo", "property5.nestedProperty1").Count());
        Assert.AreEqual(1, indexer.GetMatches("42", "property5.nestedProperty2").Count());
        Assert.AreEqual(1, indexer.GetMatches("bar", "property6.nestedProperty1").Count());
        Assert.AreEqual(1, indexer.GetMatches("13", "property6.nestedProperty2").Count());
    }

    [Theory]
    public void GetMatches_WithExistingValue_ShouldReturnMatchingObjects()
    {
        // Arrange
        var indexer = new PageIndex<TestEntity>();
        var entity1 = new TestEntity { Id = "1", Property1 = "foo", Property2 = 42 };
        var entity2 = new TestEntity { Id = "2", Property1 = "bar", Property2 = 42 };
        indexer.Index(new List<TestEntity> { entity1, entity2 });

        // Act
        var matches = indexer.GetMatches("42", "property2");

        // Assert
        Assert.AreEqual(2, matches.Count());
        Assert.Contains(entity1, matches.ToList());
        Assert.Contains(entity2, matches.ToList());
    }

    [Theory]
    public void GetMatches_WithNonExistingValue_ShouldReturnEmptyEnumerable()
    {
        // Arrange
        var indexer = new PageIndex<TestEntity>();
        var entity1 = new TestEntity { Id = "1", Property1 = "foo", Property2 = 42 };
        indexer.Index(entity1);

        // Act
        var matches = indexer.GetMatches("bar", "property1");

        // Assert
        Assert.IsEmpty(matches);
    }

    [Theory]
    public void GetMatches_WithNestedObjects_ShouldReturnMatchingObjects()
    {
        // Arrange
        var indexer = new PageIndex<TestEntity>();
        var entity1 = new TestEntity
        {
            Id = "1",
            Property5 = new NestedObject { NestedProperty1 = "foo", NestedProperty2 = 42 },
            Property6 = new List<NestedObject> { new NestedObject { NestedProperty1 = "bar", NestedProperty2 = 13 } }
        };
        var entity2 = new TestEntity
        {
            Id = "2",
            Property5 = new NestedObject { NestedProperty1 = "baz", NestedProperty2 = 42 },
            Property6 = new List<NestedObject> { new NestedObject { NestedProperty1 = "qux", NestedProperty2 = 13 } }
        };
        indexer.Index(new List<TestEntity> { entity1, entity2 });

        // Act
        var matches1 = indexer.GetMatches("foo", "property5.nestedProperty1");
        var matches2 = indexer.GetMatches("13", "property6.nestedProperty2");

        // Assert
        Assert.AreEqual(1, matches1.Count());
        Assert.Contains(entity1, matches1.ToList());

        Assert.AreEqual(2, matches2.Count());
        Assert.Contains(entity1, matches2.ToList());
        Assert.Contains(entity2, matches2.ToList());
    }

    [Theory]
    public void GetMatches_WithInvalidPath_ShouldReturnEmptyEnumerable()
    {
        // Arrange
        var indexer = new PageIndex<TestEntity>();
        var entity1 = new TestEntity { Id = "1", Property1 = "foo" };
        indexer.Index(entity1);

        // Act
        var matches = indexer.GetMatches("foo", "invalid.path");

        // Assert
        Assert.IsEmpty(matches);
    }

    [Theory]
    public void Indexer_CanDispose()
    {
        // Arrange
        var indexer = new PageIndex<TestEntity>();

        // Act
        indexer.Dispose();
    }
}
