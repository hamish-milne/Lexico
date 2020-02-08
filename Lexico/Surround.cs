using System;
using System.Reflection;

namespace Lexico
{
    public class SurroundByAttribute : TermAttribute
    {
        public override int Priority => 100;
        public SurroundByAttribute(Type surround) {
            Surround = surround ?? throw new ArgumentNullException(nameof(surround));
        }
        public Type Surround { get; }

        public override IParser Create(MemberInfo member, Func<IParser> child)
            => new SurroundParser(child(), this);
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

        public bool Matches(ref IContext context, ref object? value)
        {
            object? tmp = null;
            return surround.MatchChild("(Prefix)", ref context, ref tmp)
                && inner.MatchChild(null, ref context, ref value)
                && surround.MatchChild("(Suffix)", ref context, ref tmp);
        }

        public override string ToString() => $"|{inner}|";
    }
}