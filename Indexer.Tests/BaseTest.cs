using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Indexer.Tests
{
    public class BaseTest
    {
        protected readonly ITestOutputHelper Output;

        public BaseTest(ITestOutputHelper output)
        {
            Output = output;
        }
    }
}
