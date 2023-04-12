
namespace Indexer.Models;
public interface IBaseEntity
{
    string Id { get; set; }
}
public class BaseEntity : IBaseEntity
{
    public string Id { get; set; }
    public Dictionary<string, List<string>> Tag { get; set; }
    public Dictionary<string, List<Meta>> Meta { get; set; }
    public Dictionary<string, string> Attributes { get; set; }
}

public class Meta : Dictionary<string, string>
{
}
