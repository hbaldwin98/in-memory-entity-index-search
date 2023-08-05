using Indexer.Models;
namespace Indexer.Tests.Models;

public class NestedObject
{
    public string NestedProperty1 { get; set; }
    public int NestedProperty2 { get; set; }
}

public class TestEntity : IBaseEntity
{
    public string Property1 { get; set; }
    public int Property2 { get; set; }
    public bool Property3 { get; set; }
    public List<string> Property4 { get; set; }
    public NestedObject Property5 { get; set; }
    public List<NestedObject> Property6 { get; set; }
    public string Id { get; set; }
}
