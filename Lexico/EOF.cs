using System;
using System.Reflection;

namespace Lexico
{
    public class EOFAfterAttribute : TermAttribute
    {
        public override int Priority => 90;
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

        public bool Matches(ref IContext context, ref object? value) =>
            child.MatchChild(null, ref context, ref value)
            && !context.Peek(0).HasValue;

        public override string ToString() => $"{child}>EOF";
    }
}