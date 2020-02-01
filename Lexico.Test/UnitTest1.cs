using System;
using Xunit;

namespace Lexico.Test
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var expr = Parser.Parse<Calculator.Expression>("5+ (2 +1)");
            Assert.Equal(8.0, expr.Value);
        }

        [Fact]
        public void Test2()
        {
            var expr = Parser.Parse<Json.JsonDocument>(@"
            {
                ""foo"": [1, 2, 3.0],
                5: ""bar"",
                [6.1]: {""baz"": ""bat""}
            }
            ");
        }
    }
}
