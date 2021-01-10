using System.Collections.Generic;
using Xunit;

namespace Lexico.Test
{
    public class SimpleTests
    {
        class LiteralTestObj
        {
            [Literal("foo")] Unnamed _;
            [Literal("bar")] Unnamed __;
        }

        [Fact]
        public void LiteralTest()
        {
            Assert.True(Lexico.TryParse<LiteralTestObj>("foobar", out var outObj));
            Assert.NotNull(outObj);
            Assert.False(Lexico.TryParse<LiteralTestObj>("fooba", out var _));
        }

        class RepeatTestObj
        {
            [Repeat, Literal("abc")] public List<string> Items { get; } = new List<string>();
        }

        [Fact]
        public void RepeatTest()
        {
            Assert.True(Lexico.TryParse<RepeatTestObj>("abcabcabc", out var outObj));
            Assert.Equal(3, outObj.Items.Count);
        }

        class ChoiceTestObj
        {
            [Term] public List<Choice> Items { get; } = new List<Choice>();
        }

        abstract class Choice {
        }

        class Choice1 : Choice {
            [Literal("abc")] Unnamed _;
        }

        class Choice2 : Choice {
            [Literal("def")] Unnamed _;
        }

        [Fact]
        private void ChoiceTest()
        {
            Assert.True(Lexico.TryParse<ChoiceTestObj>("abcdef", out var outObj, new ConsoleTrace()));
            Assert.Equal(2, outObj.Items.Count);
            Assert.IsType<Choice1>(outObj.Items[0]);
            Assert.IsType<Choice2>(outObj.Items[1]);
        }

        class RecursiveTestObj {
            [Literal("[")] Unnamed _;
            [Optional] RecursiveTestObj next;
            [Literal("]")] Unnamed __;

            public int Count() {
                return next == null ? 0 : (next.Count() + 1);
            }
        }

        [Fact]
        private void RecursiveTest()
        {
            Assert.True(Lexico.TryParse<RecursiveTestObj>("[[[[[[[[[[[[[[[[[[[[[[[[[]]]]]]]]]]]]]]]]]]]]]]]]]", out var outObj));
            Assert.Equal(25, outObj.Count());
        }

        
    }
}