using System;
using System.Reflection;

namespace Lexico
{
    public class SurroundByAttribute : TermAttribute
    {
        public override int Priority => 100;
        public SurroundByAttribute(string surround) {
            Prefix = Suffix = surround ?? throw new ArgumentNullException(nameof(surround));
        }
        public SurroundByAttribute(string prefix, string suffix) {
            Prefix = prefix ?? throw new ArgumentNullException(nameof(Prefix));
            Suffix = suffix ?? throw new ArgumentNullException(nameof(Suffix));
        }
        public string Prefix { get; }
        public string Suffix { get; }

        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config)
            => new SurroundParser(new LiteralParser(Prefix), child(), new LiteralParser(Suffix));
    }

    public class WhitespaceSurroundedAttribute : TermAttribute
    {
        public override int Priority => 100;

        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config)
        {
            var s = ParserCache.GetParser(typeof(Whitespace?), config);
            return new SurroundParser(s, child(), s);
        }
    }

    public class PrefixAttribute : TermAttribute
    {
        public PrefixAttribute(string value) => Value = value;
        public string Value { get; }
        public override int Priority => 80;

        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config)
            => new SurroundParser(new LiteralParser(Value), child(), null);
    }

    public class SuffixAttribute : TermAttribute
    {
        public SuffixAttribute(string value) => Value = value;
        public string Value { get; }
        public override int Priority => 80;

        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config)
            => new SurroundParser(null, child(), new LiteralParser(Value));
    }

    internal class SurroundParser : IParser
    {
        public SurroundParser(IParser? prefix, IParser inner, IParser? suffix)
        {
            this.prefix = prefix;
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.suffix = suffix;
        }

        private readonly IParser inner;
        private readonly IParser? prefix, suffix;

        public bool Matches(ref IContext context, ref object? value)
        {
            object? tmp = null;
            return prefix.MatchChild("(Prefix)", ref context, ref tmp)
                && inner.MatchChild(null, ref context, ref value)
                && suffix.MatchChild("(Suffix)", ref context, ref tmp);
        }

        public override string ToString() => $"|{inner}|";
    }
}