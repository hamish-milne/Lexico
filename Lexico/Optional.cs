using System;

namespace Lexico
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class OptionalAttribute : Attribute
    {
    }

    internal class OptionalParser : IParser
    {
        public OptionalParser(IParser child) {
            this.child = child;
        }
        private readonly IParser child;
        public bool Matches(ref Buffer buffer, ref object value)
        {
            var pos = buffer.Position;
            if (child.Matches(ref buffer, ref value)) {
                return true;
            } else {
                buffer.Position = pos;
                value = null;
                return false;
            }
        }
    }
}