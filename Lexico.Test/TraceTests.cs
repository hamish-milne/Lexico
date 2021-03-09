using System;
using Xunit;

namespace Lexico.Test
{
    public class TraceTests
    {
        private class TestTrace<T> : ITrace
        {
            public bool TParserPushed { get; private set; }
            public bool TParserPopped { get; private set; }
            
            public void Push(IParser parser, string? name) => TParserPushed |= parser.OutputType == typeof(T);
            public void Pop(IParser parser, bool success, object? value, StringSegment text) => TParserPopped |= parser.OutputType == typeof(T);
        }

        private TestTrace<TParserToCheck> TestParse<TTopParser, TParserToCheck>(string parseString)
        {
            var rtn = new TestTrace<TParserToCheck>();
            var result = Lexico.Parse<TTopParser>(parseString, rtn);
            return rtn;
        }

        private class FullParser
        {
            [Term] private IgnoredInTrace Ignored;
            [Term] private NotIgnoredInTrace NotIgnored;
        }

        [Sequence(ParserFlags = ParserFlags.IgnoreInTrace)]
        private class IgnoredInTrace
        {
            [Literal("_")] private string _;
        }

        private class NotIgnoredInTrace
        {
            [Literal("_")] private string _;
        }
        
        [Fact]
        public void IgnoredParserNoPush() => Assert.False(TestParse<FullParser, IgnoredInTrace>("__").TParserPushed);
        [Fact]
        public void IgnoredParserNoPop() => Assert.False(TestParse<FullParser, IgnoredInTrace>("__").TParserPopped);
        
        
        [Fact]
        public void NonIgnoredParserPushed() => Assert.True(TestParse<FullParser, NotIgnoredInTrace>("__").TParserPushed);
        [Fact]
        public void NonIgnoredParserPop() => Assert.True(TestParse<FullParser, NotIgnoredInTrace>("__").TParserPopped);

        private class NestedNonIgnoredTopLevel
        {
            [Term] private IgnoredParser _;
        }
        
        [Sequence(ParserFlags = ParserFlags.IgnoreInTrace)]
        private class IgnoredParser
        {
            [Term] private NestedNotIgnoredParser _;
        }

        private class NestedNotIgnoredParser
        {
            [Literal("_")] private string _;
        }
        
        [Fact]
        public void NestedNonIgnoredParserWithinIgnoredPushed() => Assert.True(TestParse<NestedNonIgnoredTopLevel, NestedNotIgnoredParser>("_").TParserPushed);
        [Fact]
        public void NestedNonIgnoredParserWithinIgnoredPop() => Assert.True(TestParse<NestedNonIgnoredTopLevel, NestedNotIgnoredParser>("_").TParserPopped);
        
        private class InlineIgnoredInTrace
        {
            [Sequence(ParserFlags = ParserFlags.IgnoreInTrace)] private NotIgnoredInTrace _;
        }
        
        [Fact]
        public void InlineIgnoredParserNoPush() => Assert.False(TestParse<InlineIgnoredInTrace, NotIgnoredInTrace>("_").TParserPushed);
        [Fact]
        public void InlineIgnoredParserNoPop() => Assert.False(TestParse<InlineIgnoredInTrace, NotIgnoredInTrace>("_").TParserPopped);
        
        [Fact]
        public void EmptyUserTraceDoesntBlowUp()
        {
            var result = Lexico.TryParse("5", out int i, new UserTrace(Console.WriteLine));
        }
    }
}