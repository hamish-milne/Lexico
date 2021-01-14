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
            Assert.True(Lexico.TryParse<RepeatTestObj>("abcabcabc", out var outObj, new ConsoleTrace()));
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
        public void ChoiceTest()
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
                return next == null ? 1 : (next.Count() + 1);
            }
        }

        [Fact]
        public void RecursiveTest()
        {
            Assert.True(Lexico.TryParse<RecursiveTestObj>("[[[[[[[[[[[[[[[[[[[[[[[[[]]]]]]]]]]]]]]]]]]]]]]]]]", out var outObj));
            Assert.Equal(25, outObj.Count());
        }

        abstract class Expression {
            public abstract int Eval();
        }

        class Expression2 : Expression {
            [Term] public Expression lhs;
            [Literal("+")] Unnamed _;
            [Term] public Expression rhs;
            public override int Eval() => lhs.Eval() + rhs.Eval();
        }

        class Expression3 : Expression {
            [Term] public Expression lhs;
            [Literal("*")] Unnamed _;
            [Term] public Expression rhs;
            public override int Eval() => lhs.Eval() * rhs.Eval();
        }

        class Expression1 : Expression {
            [CharRange("09")] public char c;
            public override int Eval() => c - '0';
        }

        [Fact]
        public void ILRTest()
        {
            Assert.True(Lexico.TryParse<Expression>("1+2*3", out var outObj, new ConsoleTrace()));
            Assert.Equal(7, outObj.Eval());
        }

        class RegexTestObj {
            [Regex("[a-zA-Z0-9]*")] public string str;
        }

        [Fact]
        public void RegexTest()
        {
            Assert.True(Lexico.TryParse<RegexTestObj>("abc012DEF", out var outObj, new ConsoleTrace{Verbose = true}));
            Assert.Equal("abc012DEF", outObj.str);
        }

        [Fact]
        public void RegexTest2()
        {
            Assert.True(Lexico.TryParse<RegexImpl.Regex>("(?:(?:.))*", out var obj));
        }

        [MultiLine, TopLevel]
        class WhitespaceTestObj
        {
            [Whitespace] Unnamed _;
        }

        [Fact]
        public void WhitespaceTest()
        {
            Assert.True(Lexico.TryParse<WhitespaceTestObj>("  \n   \n \t   ", out var _, new ConsoleTrace{Verbose = true}));
        }

        
    }
}