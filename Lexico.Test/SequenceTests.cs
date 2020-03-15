#pragma warning disable CS0169,CS0649,IDE0044,IDE0051
using System.Globalization;
using Xunit;

namespace Lexico.Test
{
    public class SequenceTests
    {
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
        public void OrderOfSequenceCorrect()
        {
            Assert.True(Lexico.TryParse(" 5 ", out NonGenericSequence _, new ConsoleTrace()));
        }

        [Fact]
        public void OrderOfGenericSequenceCorrect() => Lexico.Parse<GenericWrapper>(" 5 ", new ConsoleTrace());
    }
}