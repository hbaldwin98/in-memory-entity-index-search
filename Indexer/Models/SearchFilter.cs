
namespace Indexer.Models;

public class SearchFilter
{
    public string Field { get; set; }
    public List<string> Values { get; set; }
    public string NestedPrefix { get; set; }

    public SearchFilter()
    {
    }

    public SearchFilter(string field, List<string> values, string nestedPrefix = null)
    {
        Field = field;
        Values = values;
        NestedPrefix = nestedPrefix;
    }
}
