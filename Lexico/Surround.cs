
using System;
using static System.AttributeTargets;

namespace Lexico
{
    [AttributeUsage(Field | Property | Class | Struct, AllowMultiple = true)]
    public class SurroundByAttribute : Attribute
    {
        public SurroundByAttribute(Type surround) {
            Surround = surround ?? throw new ArgumentNullException(nameof(surround));
        }
        public Type Surround { get; }
    }

    public class WhitespaceSurroundedAttribute : SurroundByAttribute
    {
        public WhitespaceSurroundedAttribute() : base(typeof(Whitespace?)) {}
    }

    internal class SurroundParser : IParser
    {
        public SurroundParser(IParser inner, SurroundByAttribute attribute)
        {
            surround = ParserCache.GetParser(attribute.Surround);
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        private readonly IParser inner;
        private readonly IParser surround;

        public bool Matches(ref Buffer buffer, ref object value, ITrace trace)
        {
            object tmp = null;
            return surround.Matches(ref buffer, ref tmp, trace)
                && inner.Matches(ref buffer, ref value, trace)
                && surround.Matches(ref buffer, ref tmp, trace);
        }
    }
}