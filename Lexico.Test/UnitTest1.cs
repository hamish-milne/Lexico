using System.IO;
using Xunit;

namespace Lexico.Test
{
    public class UnitTest1
    {
        [Fact]
        public void CalculatorTest()
        {
            var expr = Lexico.Parse<Calculator.Expression>("5-(3/2)^(2+1)");
            Assert.Equal(1.625f, expr.Value);
        }

        [Fact]
        public void JsonTest()
        {
            var expr = Lexico.Parse<Json.JsonDocument>(@"
            {
                ""foo"": [1, 2, 3.0],
                5: ""bar"",
                [6.1]: {""baz"": ""bat""}
            }
            ");
        }
    }
}
