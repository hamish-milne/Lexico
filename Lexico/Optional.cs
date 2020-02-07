using System;

namespace Lexico
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class OptionalAttribute : TermAttribute
    {
    }

    internal class OptionalParser : IParser
    {
        public OptionalParser(IParser child) {
            this.child = child;
        }
        private readonly IParser child;
        public bool Matches(ref IContext context, ref object? value)
        {
            child.MatchChild(null, ref context, ref value);
            return true;
        }

        public override string ToString() => $"{child}?";
    }
}