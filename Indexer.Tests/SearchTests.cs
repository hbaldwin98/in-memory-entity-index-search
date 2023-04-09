namespace Indexer.Tests
{
    public class SearchTests
    {
        public class NestedObject
        {
            public string NestedProperty1 { get; set; }
            public int NestedProperty2 { get; set; }
        }

        public class TestBase : Base
        {
            public string Property1 { get; set; }
            public int Property2 { get; set; }
            public bool Property3 { get; set; }
            public List<string> Property4 { get; set; }
            public NestedObject Property5 { get; set; }
            public List<NestedObject> Property6 { get; set; }
        }

        [Fact]
        public void IndexingAndSearchingSingleObject()
        {
            var indexer = new Indexer<TestBase>();

            var testObj = new TestBase
            {
                Property1 = "test",
                Property2 = 42,
                Property3 = true,
                Property4 = new List<string> { "item1", "item2" }
            };

            indexer.Index(testObj);

            var complexSearch = new ComplexSearch
            {
                OneOf = new List<SearchFilter>
            {
                new SearchFilter { Field = "property1", Values = new List<string> { "test" } },
                new SearchFilter { Field = "property2", Values = new List<string> { "42" } }
            }
            };

            var result = indexer.Search(new List<ComplexSearch> { complexSearch });

            Assert.Single(result);
            Assert.Contains(testObj, result.ToList());
        }

        [Fact]
        public void IndexingAndSearchingMultipleObjects()
        {
            var indexer = new Indexer<TestBase>();

            var testObj1 = new TestBase
            {
                Property1 = "test",
                Property2 = 42,
                Property3 = true,
                Property4 = new List<string> { "item1", "item2" }
            };

            var testObj2 = new TestBase
            {
                Property1 = "test2",
                Property2 = 42,
                Property3 = false,
                Property4 = new List<string> { "item1", "item2", "item3" }
            };

            indexer.Index(new List<TestBase> { testObj1, testObj2 });

            var complexSearch = new ComplexSearch
            {
                OneOf = new List<SearchFilter>
            {
                new SearchFilter { Field = "property1", Values = new List<string> { "test" } },
                new SearchFilter { Field = "property2", Values = new List<string> { "42" } }
            },
                NotOneOf = new List<SearchFilter>
            {
                new SearchFilter { Field = "property3", Values = new List<string> { "False" } }
            }
            };

            var result = indexer.Search(new List<ComplexSearch> { complexSearch });

            Assert.Single(result);
            Assert.Contains(testObj1, result.ToList());
        }

        [Fact]
        public void SearchWithEmptyFiltersShouldReturnEmpty()
        {
            var indexer = new Indexer<TestBase>();

            var testObj1 = new TestBase
            {
                Property1 = "test",
                Property2 = 42,
                Property3 = true,
                Property4 = new List<string> { "item1", "item2" }
            };

            indexer.Index(testObj1);

            var complexSearch = new ComplexSearch
            {
                OneOf = new List<SearchFilter>(),
                NotOneOf = new List<SearchFilter>()
            };

            var result = indexer.Search(new List<ComplexSearch> { complexSearch });

            Assert.Empty(result);
        }

        [Fact]
        public void IndexingAndSearchingSingleComplexObject()
        {
            var indexer = new Indexer<TestBase>();

            var testObj = new TestBase
            {
                Property1 = "test",
                Property2 = 42,
                Property3 = true,
                Property4 = new List<string> { "item1", "item2" },
                Property5 = new NestedObject { NestedProperty1 = "nested", NestedProperty2 = 24 },
                Property6 = new List<NestedObject>
        {
            new NestedObject { NestedProperty1 = "nested1", NestedProperty2 = 10 },
            new NestedObject { NestedProperty1 = "nested2", NestedProperty2 = 20 }
        }
            };

            indexer.Index(testObj);

            var complexSearch = new ComplexSearch
            {
                OneOf = new List<SearchFilter>
        {
            new SearchFilter { Field = "property1", Values = new List<string> { "test" } },
            new SearchFilter { Field = "property2", Values = new List<string> { "42" } },
            new SearchFilter { Field = "property5.nestedProperty1", Values = new List<string> { "nested" } },
            new SearchFilter { Field = "property6.nestedProperty2", Values = new List<string> { "10" } }
        }
            };

            var result = indexer.Search(new List<ComplexSearch> { complexSearch });

            Assert.Single(result);
            Assert.Contains(testObj, result);
        }

        [Fact]
        public void Indexer_IndexesObject()
        {
            // Arrange
            var indexer = new Indexer<TestBase>();
            var testObject = new TestBase
            {
                Property1 = "Test",
                Property2 = 42,
                Property3 = true,
                Property4 = new List<string> { "one", "two", "three" },
                Property5 = new NestedObject { NestedProperty1 = "NestedTest", NestedProperty2 = 123 },
                Property6 = new List<NestedObject> { new NestedObject { NestedProperty1 = "NestedTest1", NestedProperty2 = 456 }, new NestedObject { NestedProperty1 = "NestedTest2", NestedProperty2 = 789 } }
            };

            // Act
            indexer.Index(testObject);

            // Assert
            var matches = indexer.Search(new List<ComplexSearch> { new ComplexSearch { OneOf = new List<SearchFilter> { new SearchFilter { Field = "property1", Values = new List<string> { "Test" } } } } });
            Assert.Single(matches);
            Assert.Equal(testObject, matches.First());
        }

        [Fact]
        public void Indexer_IndexesNestedObject()
        {
            // Arrange
            var indexer = new Indexer<TestBase>();
            var testObject = new TestBase
            {
                Property1 = "Test",
                Property5 = new NestedObject { NestedProperty1 = "NestedTest", NestedProperty2 = 123 }
            };

            // Act
            indexer.Index(testObject);

            // Assert
            var matches = indexer.Search(new List<ComplexSearch> { new ComplexSearch { OneOf = new List<SearchFilter> { new SearchFilter { Field = "property5.nestedProperty1", Values = new List<string> { "NestedTest" } } } } });
            Assert.Single(matches);
            Assert.Equal(testObject, matches.First());
        }

        [Fact]
        public void Indexer_IndexesNestedObjectList()
        {
            // Arrange
            var indexer = new Indexer<TestBase>();
            var testObject = new TestBase
            {
                Property1 = "Test",
                Property6 = new List<NestedObject> { new NestedObject { NestedProperty1 = "NestedTest1", NestedProperty2 = 456 }, new NestedObject { NestedProperty1 = "NestedTest2", NestedProperty2 = 789 } }
            };

            // Act
            indexer.Index(testObject);

            // Assert
            var matches = indexer.Search(new List<ComplexSearch> { new ComplexSearch { OneOf = new List<SearchFilter> { new SearchFilter { Field = "property6.nestedProperty1", Values = new List<string> { "NestedTest1" } } } } });
            Assert.Single(matches);
            Assert.Equal(testObject, matches.First());
        }

        [Fact]
        public void Indexer_SearchesForMultipleValues()
        {
            // Arrange
            var indexer = new Indexer<TestBase>();
            var testObject1 = new TestBase { Property1 = "Test1" };
            var testObject2 = new TestBase { Property1 = "Test2" };
            indexer.Index(new List<TestBase> { testObject1, testObject2 });

            // Act
            var matches = indexer.Search(new List<ComplexSearch> { new ComplexSearch { OneOf = new List<SearchFilter> { new SearchFilter { Field = "property1", Values = new List<string> { "Test1", "Test2" } } } } });

            // Assert
            Assert.Equal(2, matches.Count());
            Assert.Contains(testObject1, matches);
            Assert.Contains(testObject2, matches);
        }

        [Fact]
        public void Indexer_SearchesForMultipleFields()
        {
            // Arrange
            var indexer = new Indexer<TestBase>();
            var testObject = new TestBase
            {
                Property1 = "Test",
                Property2 = 42,
                Property5 = new NestedObject { NestedProperty1 = "NestedTest", NestedProperty2 = 123 }
            };
            indexer.Index(testObject);

            // Act
            var matches = indexer.Search(new List<ComplexSearch> { new ComplexSearch { OneOf = new List<SearchFilter> { new SearchFilter { Field = "property1", Values = new List<string> { "Test" } }, new SearchFilter { Field = "property5.nestedProperty1", Values = new List<string> { "NestedTest" } } } } });

            // Assert
            Assert.Single(matches);
            Assert.Equal(testObject, matches.First());
        }

        [Fact]
        public void Indexer_SearchesForNotOneOf()
        {
            // Arrange
            var indexer = new Indexer<TestBase>();
            var testObject1 = new TestBase { Property1 = "Test1" };
            var testObject2 = new TestBase { Property1 = "Test2" };
            indexer.Index(new List<TestBase> { testObject1, testObject2 });

            // Act
            var matches = indexer.Search(new List<ComplexSearch> { new ComplexSearch { OneOf = new List<SearchFilter> { new SearchFilter { Field = "property1", Values = new List<string> { "Test1" } } }, NotOneOf = new List<SearchFilter> { new SearchFilter { Field = "property1", Values = new List<string> { "Test2" } } } } });

            // Assert
            Assert.Single(matches);
            Assert.Contains(testObject1, matches);
            Assert.DoesNotContain(testObject2, matches);
        }

        [Fact]
        public void Indexer_SearchesForMultipleFieldsAndValues()
        {
            // Arrange
            var indexer = new Indexer<TestBase>();
            var testObject1 = new TestBase
            {
                Property1 = "Test1",
                Property2 = 42,
                Property5 = new NestedObject { NestedProperty1 = "NestedTest1", NestedProperty2 = 123 }
            };
            var testObject2 = new TestBase
            {
                Property1 = "Test2",
                Property2 = 24,
                Property5 = new NestedObject { NestedProperty1 = "NestedTest2", NestedProperty2 = 456 }
            };
            indexer.Index(new List<TestBase> { testObject1, testObject2 });

            // Act
            var matches = indexer.Search(new List<ComplexSearch> {
                    new ComplexSearch {
                        OneOf = new List<SearchFilter> {
                            new SearchFilter { Field = "property1", Values = new List<string> { "Test1" } },
                            new SearchFilter { Field = "property2", Values = new List<string> { "24" } },
                            new SearchFilter { Field = "property5.nestedProperty1", Values = new List<string> { "NestedTest2" } }
                        }
                    }
                });

            // Assert
            Assert.Empty(matches);
        }

        [Fact]
        public void Indexer_SearchesWithComplexSearch()
        {
            // Arrange
            var indexer = new Indexer<TestBase>();
            var testObject1 = new TestBase
            {
                Property1 = "Test1",
                Property2 = 42,
                Property3 = true,
                Property4 = new List<string> { "one", "two", "three" },
                Property5 = new NestedObject { NestedProperty1 = "NestedTest1", NestedProperty2 = 123 },
                Property6 = new List<NestedObject> { new NestedObject { NestedProperty1 = "NestedTest2", NestedProperty2 = 123 }, new NestedObject { NestedProperty1 = "NestedTest3", NestedProperty2 = 789 } }
            };
            var testObject2 = new TestBase
            {
                Property1 = "Test2",
                Property2 = 24,
                Property3 = false,
                Property4 = new List<string> { "four", "five", "six" },
                Property5 = new NestedObject { NestedProperty1 = "NestedTest2", NestedProperty2 = 567 },
                Property6 = new List<NestedObject> { new NestedObject { NestedProperty1 = "NestedTest3", NestedProperty2 = 789 }, new NestedObject { NestedProperty1 = "NestedTest4", NestedProperty2 = 101112 } }
            };
            indexer.Index(new List<TestBase> { testObject1, testObject2 });

            // Act
            var matches = indexer.Search(new List<ComplexSearch> {
                new ComplexSearch {
                    OneOf = new List<SearchFilter> {
                        new SearchFilter { Field = "property1", Values = new List<string> { "Test1", "Test2" } },
                        new SearchFilter { Field = "property2", Values = new List<string> { "24", "42" } },
                        new SearchFilter { Field = "property3", Values = new List<string> { "True" } },
                        new SearchFilter { Field = "property4", Values = new List<string> { "one", "four" } },
                        new SearchFilter { Field = "property5.nestedProperty1", Values = new List<string> { "NestedTest1", "NestedTest2" } },
                        new SearchFilter { Field = "property6.nestedProperty2", Values = new List<string> { "123", "101112" } }
                    },
                    NotOneOf = new List<SearchFilter> {
                        new SearchFilter { Field = "property4", Values = new List<string> { "four", "six" } },
                        new SearchFilter { Field = "property5.nestedProperty2", Values = new List<string> { "567" } }
                    }
                }
                });

            // Assert
            Assert.Single(matches);
            Assert.Equal(testObject1, matches.First());
        }

        [Fact]
        public void Indexer_SearchesForMultipleMatchesWithComplexSearch()
        {
            // Arrange
            var indexer = new Indexer<TestBase>();
            var testObject1 = new TestBase { Id = "1", Property1 = "Test1", Property2 = 1, Property3 = true };
            var testObject2 = new TestBase { Id = "2", Property1 = "Test2", Property2 = 2, Property3 = false };
            var testObject3 = new TestBase { Id = "3", Property1 = "Test3", Property2 = 3, Property3 = true };
            var testObject4 = new TestBase { Id = "4", Property1 = "Test4", Property2 = 4, Property3 = false };
            var testObject5 = new TestBase { Id = "5", Property1 = "Test5", Property2 = 5, Property3 = true };
            indexer.Index(new List<TestBase> { testObject1, testObject2, testObject3, testObject4, testObject5 });

            // Act
            var matches = indexer.Search(new List<ComplexSearch> {
                    new ComplexSearch {
                        OneOf = new List<SearchFilter> {
                            new SearchFilter { Field = "property1", Values = new List<string> { "Test1", "Test3" } },
                            new SearchFilter { Field = "property2", Values = new List<string> { "1", "2", "3" } },
                            new SearchFilter { Field = "property3", Values = new List<string> { "True" } }
                        },
                        NotOneOf = new List<SearchFilter> {
                            new SearchFilter { Field = "property1", Values = new List<string> { "Test5" } }
                        }
                    }
                });

            // Assert
            Assert.Equal(2, matches.Count());
            Assert.Contains(testObject1, matches);
            Assert.Contains(testObject3, matches);
        }

        [Fact]
        public void Indexer_SearchesForMultipleComplexFiltersWithOR()
        {
            // Arrange
            var indexer = new Indexer<TestBase>();
            var testObject1 = new TestBase
            {
                Property1 = "Test1",
                Property5 = new NestedObject { NestedProperty1 = "NestedTest1", NestedProperty2 = 456 },
                Property6 = new List<NestedObject> { new NestedObject { NestedProperty1 = "NestedTest1", NestedProperty2 = 123 } }
            };
            var testObject2 = new TestBase
            {
                Property1 = "Test2",
                Property5 = new NestedObject { NestedProperty1 = "NestedTest2", NestedProperty2 = 789 },
                Property6 = new List<NestedObject> { new NestedObject { NestedProperty1 = "NestedTest2", NestedProperty2 = 456 } }
            };
            indexer.Index(new List<TestBase> { testObject1, testObject2 });

            // Act
            var complexSearch1 = new ComplexSearch
            {
                OneOf = new List<SearchFilter>
            {
                new SearchFilter { Field = "property1", Values = new List<string> { "Test1" } }
            }
            };
            var complexSearch2 = new ComplexSearch
            {
                OneOf = new List<SearchFilter>
            {
                new SearchFilter { Field = "property5.nestedProperty1", Values = new List<string> { "NestedTest2" } }
            }
            };
            var matches = indexer.Search(new List<ComplexSearch> { complexSearch1, complexSearch2 });

            // Assert
            Assert.Equal(2, matches.Count());
            Assert.Contains(testObject1, matches);
            Assert.Contains(testObject2, matches);
        }
    }
}