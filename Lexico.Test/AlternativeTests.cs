using System;
using Xunit;

namespace Lexico.Test
{
    public class AlternativeTests
    {
        private interface ITruthy {}

        private struct True : ITruthy { [Literal("true")] private Unnamed _; }

        private struct False : ITruthy { [Literal("false")] private Unnamed _; }
        private struct Maybe : ITruthy { [Literal("maybe")] private Unnamed _; }

        private class Truthy
        {
            [Alternative(typeof(True), typeof(False))] public ITruthy TrueOrFalse;
        }

        private class TruthyWithInvalidAlternative
        {
            [Alternative(typeof(True), typeof(False), typeof(object))] public ITruthy TrueOrFalse;
        }

        private abstract class IndirectTruthy
        {
            [IndirectAlternative(nameof(Options))] public ITruthy Indirect;
            public abstract Type[] Options { get; }
        }

        private class OnlyTrue : IndirectTruthy { public override Type[] Options { get; } = {typeof(True)}; }
        private class OnlyFalse : IndirectTruthy { public override Type[] Options { get; } = {typeof(False)}; }
        private class OnlyMaybe : IndirectTruthy { public override Type[] Options { get; } = {typeof(Maybe)}; }
        
        [Theory]
        [InlineData("true", true)]
        [InlineData("false", true)]
        [InlineData("maybe", false)]
        public void ExplicitAlternative(string expression, bool passes)
        {
            if (passes)
            {
                Assert.True(Lexico.TryParse(expression, out Truthy b, new ConsoleTrace()));
            }
            else
            {
                Assert.False(Lexico.TryParse(expression, out Truthy b, new ConsoleTrace()));
            }
        }

        [Fact]
        public void BadExplicitAlternativeType()
        {
            Assert.Throws<ArgumentException>(() => Lexico.Parse<TruthyWithInvalidAlternative>("true", new ConsoleTrace()));
        }
        
        [Theory]
        [
            InlineData(typeof(OnlyTrue), "true", true),
            InlineData(typeof(OnlyTrue), "false", false),
            InlineData(typeof(OnlyTrue), "maybe", false),
            InlineData(typeof(OnlyFalse), "true", false),
            InlineData(typeof(OnlyFalse), "false", true),
            InlineData(typeof(OnlyFalse), "maybe", false),
            InlineData(typeof(OnlyMaybe), "true", false),
            InlineData(typeof(OnlyMaybe), "false", false),
            InlineData(typeof(OnlyMaybe), "maybe", true),
        ]
        public void IndirectAlternative(Type parseType, string expression, bool passes)
        {
            if (passes)
            {
                Assert.True(Lexico.TryParse(expression, parseType, out _, new ConsoleTrace()));
            }
            else
            {
                Assert.False(Lexico.TryParse(expression, parseType, out _, new ConsoleTrace()));
            }
        }
    }
}