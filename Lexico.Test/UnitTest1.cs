using System.IO;
using Xunit;

namespace Lexico.Test
{
    public class UnitTest1
    {
        [Fact]
        public void CalculatorTest()
        {
            var expr = Lexico.Parse<Calculator.Expression>("5-(3/2)^(2+1)", new ConsoleTrace{Verbose = true});
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

        private class GetOnlyProp
        {
            [field:Term] public float Value { get; }
        }

        [Fact]
        public void WriteGetOnlyProperty() => Lexico.Parse<GetOnlyProp>("5");

        private class GetOnlyPropWithPrivateSet
        {
            [field:Term] public float Value { get; private set; }
        }

        [Fact]
        public void WritePrivateSetProperty() => Lexico.Parse<GetOnlyPropWithPrivateSet>("5");

        private class ReadonlyField
        {
            [Term] public readonly float Value;
        }

        [Fact]
        public void WriteReadonlyField() => Lexico.Parse<ReadonlyField>("5");
    }
}
