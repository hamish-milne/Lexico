using System;
using System.Reflection;

namespace Lexico
{
    public class EOFAfterAttribute : TermAttribute
    {
        public override int Priority => 1000;
        public override IParser Create(MemberInfo member, Func<IParser> child)
        {
            var c = child();
            if (c is EOFParser eof) {
                return eof;
            }
            return new EOFParser(c);
        }
    }

    internal class EOFParser : IParser
    {
        public EOFParser(IParser child) {
            this.child = child;
        }
        private readonly IParser child;

        private class NothingParser : IParser
        {
            public static NothingParser Instance { get; } = new NothingParser();
            
            private NothingParser(){}

            public bool Matches(ref IContext context, ref object? value) => !context.Peek(0).HasValue;

            public override string ToString() => "<nothing>";
        }

        public bool Matches(ref IContext context, ref object? value) =>
            child.MatchChild(null, ref context, ref value)
            && NothingParser.Instance.MatchChild("EOF", ref context, ref value);

        public override string ToString() => $"{child}>{NothingParser.Instance}";
    }
}