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
        public bool Matches(ref Buffer buffer, ref object value, ITrace trace)
        {
            var pos = buffer.Position;
            if (child.Matches(ref buffer, ref value, trace)) {
                return true;
            } else {
                buffer.Position = pos;
                value = null;
                return true;
            }
        }

        public override string ToString() => $"{child}?";
    }
}