#pragma warning disable CS0169,CS0649,IDE0044,IDE0051
using Xunit;

namespace Lexico.Test
{
    public class WriteTests
    {
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
    }
}