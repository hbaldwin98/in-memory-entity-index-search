
namespace Indexer.Models;

public class SearchFilter
{
    public string Field { get; set; }
    public List<string> Values { get; set; }
    public string NestedPrefix { get; set; }
}
