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

        public bool Matches(ref IContext context, ref object? value) =>
            child.MatchChild(null, ref context, ref value)
            && !context.Peek(0).HasValue;

        public override string ToString() => $"{child}>EOF";
    }
}