using System.Collections.Generic;
using Xunit;

namespace Lexico.Test
{
    public class RepeatTests
    {
        private struct Comma
        {
            [Literal(",")] private Unnamed _;
        }

        private class NonGenericRepeat
        {
            [Literal("(")] private Unnamed _open;
            [field: SeparatedBy(typeof(Comma))] public List<int> Values { get; private set; }
            [Literal(")")] private Unnamed _close;
        }

        [Fact]
        public void RepeatParser() => Assert.Equal(Lexico.Parse<NonGenericRepeat>("(1, 1, 3, 5, 7)").Values, new []{1, 1, 3, 5, 7});

        private class GenericRepeat<T>
        {
            [Literal("(")] private Unnamed _open;
            [field: SeparatedBy(typeof(Comma))] public List<T> Values { get; private set; }
            [Literal(")")] private Unnamed _close;
        }

        [Fact]
        public void GenericRepeatParser() => Assert.Equal(Lexico.Parse<GenericRepeat<int>>("(1, 1, 3, 5, 7)").Values, new []{1, 1, 3, 5, 7});
    }
}