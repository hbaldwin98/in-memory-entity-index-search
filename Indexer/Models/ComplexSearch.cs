namespace Indexer.Models;
public class ComplexSearch
{
    public List<SearchFilter> OneOf { get; set; }
    public List<SearchFilter> NotOneOf { get; set; }
}