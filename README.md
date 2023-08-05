# in-memory-entity-index-search

A basic in-memory implementation to index and search upon classes - "entities." Entities are converted into JSONDocument and inserted into a node based tree structure associated by each dot-delimited parameter. 

```
public class TestObject
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
}
```

This can be indexed into a format similar to the below where each entity's index is store in a hashmap for quick lookup:

```
{
    "pathSegment": "path1"
    "children": {
        "path2": {
            "pathSegment": "path2",
            "children": null,
            "leaves": {
                "leafValue": {
                    "value": "leafValue",
                    "matches": {
                        [entityIndex]
                    }
                }
            }
        }
    }
}
```

This has support for any number of nested objects and can be accessed in a "dot-notation" - firstlevel.nested. 
