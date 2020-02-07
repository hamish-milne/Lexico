using System;
using static System.AttributeTargets;

namespace Lexico
{
    [AttributeUsage(Field | Class, AllowMultiple = false)]
    public class EOFAfterAttribute : TermAttribute { }

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