using System.Globalization;
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
            [field:Term] public int Value { get; }
        }

        [Fact]
        public void CannotWriteGetOnlyProperty() => Assert.NotEqual(5, Lexico.Parse<GetOnlyProp>("5").Value);

        private class GetOnlyPropWithPrivateSet
        {
            [field:Term] public int Value { get; private set; }
        }

        [Fact]
        public void WritePrivateSetProperty() => Assert.Equal(5, Lexico.Parse<GetOnlyPropWithPrivateSet>("5").Value);

        private class ReadonlyField
        {
            [Term] public readonly int Value;
        }

        [Fact]
        public void CannotWriteReadonlyField() => Assert.NotEqual(5, Lexico.Parse<ReadonlyField>("5").Value);

        private class PrivateField
        {
            [Term] private int Value;
            public int Val => Value;
        }

        [Fact]
        public void WritePrivateField() => Assert.Equal(5, Lexico.Parse<PrivateField>("5").Val);

        private class ProtectedField
        {
            [Term] protected int Value;
            public int Val => Value;
        }

        [Fact]
        public void WriteProtectedField() => Assert.Equal(5, Lexico.Parse<ProtectedField>("5").Val);

        private class NonGenericSequence
        {
            [Whitespace] public int a;
            [Term, NumberStyle(NumberStyles.None)] public int b;
            [Whitespace] public int c;
        }

        private class GenericSequence<T>
        {
            [Whitespace] public Unnamed a;
            [Term, NumberStyle(NumberStyles.None)] public T b;
            [Whitespace] public Unnamed c;
        }

        private class GenericWrapper : GenericSequence<int> { }

        [Fact]
        public void OrderOfSequenceCorrect() => Lexico.Parse<NonGenericSequence>(" 5 ");

        [Fact]
        public void OrderOfGenericSequenceCorrect() => Lexico.Parse<GenericWrapper>(" 5 ");
    }
}
