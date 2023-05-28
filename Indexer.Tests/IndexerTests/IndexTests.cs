using Indexer.Extensions;
using Indexer.Tests.Models;
using Xunit.Abstractions;

namespace Indexer.Tests.IndexerTests;

public class IndexTests : BaseTest
{
    public IndexTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ExportToCsv_WritesExpectedCsvFile()
    {
        // Arrange
        var indexer = new Indexer<TestEntity>();
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
        var entity2 = new TestEntity
        {
            Id = "2",
            Property1 = "foo",
            Property2 = 42,
            Property3 = true,
            Property4 = new List<string> { "bar", "baz" },
            Property5 = new NestedObject { NestedProperty1 = "qux", NestedProperty2 = 13 },
            Property6 = new List<NestedObject> { new NestedObject { NestedProperty1 = "quux", NestedProperty2 = 17 } }
        };
        indexer.Index(entity);
        indexer.Index(entity2);

        // Act
        indexer.ExportToCsv("test.csv");

        // Assert
        string[] lines = File.ReadAllLines("test.csv");
        Assert.Equal(3, lines.Length); // Header row + data row
        Assert.Equal("id,property1,property2,property3,property4,property5.nestedProperty1,property5.nestedProperty2,property6.nestedProperty1,property6.nestedProperty2", lines[0]);
        Assert.Equal("1,foo,42,True,bar;baz,qux,13,quux,17", lines[1]);
        Assert.Equal("2,foo,42,True,bar;baz,qux,13,quux,17", lines[2]);

    }

    [Fact]
    public void Index_WithSingleEntity_ShouldIndexCorrectly()
    {
        // Arrange
        var indexer = new Indexer<TestEntity>();
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
        Assert.Equal(1, indexer.GetMatches("foo", "property1").Count());
        Assert.Equal(1, indexer.GetMatches("42", "property2").Count());
        Assert.Equal(1, indexer.GetMatches("True", "property3").Count());
        Assert.Equal(1, indexer.GetMatches("bar", "property4").Count());
        Assert.Equal(1, indexer.GetMatches("baz", "property4").Count());
        Assert.Equal(1, indexer.GetMatches("qux", "property5.nestedProperty1").Count());
        Assert.Equal(1, indexer.GetMatches("13", "property5.nestedProperty2").Count());
        Assert.Equal(1, indexer.GetMatches("quux", "property6.nestedProperty1").Count());
        Assert.Equal(1, indexer.GetMatches("17", "property6.nestedProperty2").Count());
    }

    [Fact]
    public void Index_WithMultipleEntities_ShouldIndexCorrectly()
    {
        // Arrange
        var indexer = new Indexer<TestEntity>();
        var entities = new List<TestEntity>
    {
        new TestEntity { Id = "1", Property1 = "foo" },
        new TestEntity { Id = "2", Property1 = "bar" },
        new TestEntity { Id = "3", Property1 = "baz" }
    };

        // Act
        indexer.Index(entities);

        // Assert
        Assert.Equal(1, indexer.GetMatches("foo", "property1").Count());
        Assert.Equal(1, indexer.GetMatches("bar", "property1").Count());
        Assert.Equal(1, indexer.GetMatches("baz", "property1").Count());
    }

    [Fact]
    public void Index_WithNestedObjects_ShouldIndexCorrectly()
    {
        // Arrange
        var indexer = new Indexer<TestEntity>();
        var entity = new TestEntity
        {
            Id = "1",
            Property5 = new NestedObject { NestedProperty1 = "foo", NestedProperty2 = 42 },
            Property6 = new List<NestedObject> { new NestedObject { NestedProperty1 = "bar", NestedProperty2 = 13 } }
        };

        // Act
        indexer.Index(entity);

        // Assert
        Assert.Equal(1, indexer.GetMatches("foo", "property5.nestedProperty1").Count());
        Assert.Equal(1, indexer.GetMatches("42", "property5.nestedProperty2").Count());
        Assert.Equal(1, indexer.GetMatches("bar", "property6.nestedProperty1").Count());
        Assert.Equal(1, indexer.GetMatches("13", "property6.nestedProperty2").Count());
    }

    [Fact]
    public void GetMatches_WithExistingValue_ShouldReturnMatchingObjects()
    {
        // Arrange
        var indexer = new Indexer<TestEntity>();
        var entity1 = new TestEntity { Id = "1", Property1 = "foo", Property2 = 42 };
        var entity2 = new TestEntity { Id = "2", Property1 = "bar", Property2 = 42 };
        indexer.Index(new List<TestEntity> { entity1, entity2 });

        // Act
        var matches = indexer.GetMatches("42", "property2");

        // Assert
        Assert.Equal(2, matches.Count());
        Assert.Contains(entity1, matches);
        Assert.Contains(entity2, matches);
    }

    [Fact]
    public void GetMatches_WithNonExistingValue_ShouldReturnEmptyEnumerable()
    {
        // Arrange
        var indexer = new Indexer<TestEntity>();
        var entity1 = new TestEntity { Id = "1", Property1 = "foo", Property2 = 42 };
        indexer.Index(entity1);

        // Act
        var matches = indexer.GetMatches("bar", "property1");

        // Assert
        Assert.Empty(matches);
    }

    [Fact]
    public void GetMatches_WithNestedObjects_ShouldReturnMatchingObjects()
    {
        // Arrange
        var indexer = new Indexer<TestEntity>();
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
        Assert.Equal(1, matches1.Count());
        Assert.Contains(entity1, matches1);

        Assert.Equal(2, matches2.Count());
        Assert.Contains(entity1, matches2);
        Assert.Contains(entity2, matches2);
    }

    [Fact]
    public void GetMatches_WithInvalidPath_ShouldReturnEmptyEnumerable()
    {
        // Arrange
        var indexer = new Indexer<TestEntity>();
        var entity1 = new TestEntity { Id = "1", Property1 = "foo" };
        indexer.Index(entity1);

        // Act
        var matches = indexer.GetMatches("foo", "invalid.path");

        // Assert
        Assert.Empty(matches);
    }

    [Fact]
    public void Indexer_CanDispose()
    {
        // Arrange
        var indexer = new Indexer<TestEntity>();

        // Act
        indexer.Dispose();
    }
}