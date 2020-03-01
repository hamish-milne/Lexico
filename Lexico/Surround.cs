using System;
using System.Reflection;

namespace Lexico
{
    /// <summary>
    /// Adds the given literals before and after the parser (ignoring any separators).
    /// Outputs the child parser's result.
    /// </summary>
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

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
            => new SurroundParser(new LiteralParser(Prefix), child(null), new LiteralParser(Suffix));
    }

    /// <summary>
    /// Surrounds the parser with optional whitespace (ignoring any separators).
    /// Outputs the child parser's result.
    /// </summary>
    public class WhitespaceSurroundedAttribute : TermAttribute
    {
        public override int Priority => 100;

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
        {
            var s = new OptionalParser(new WhitespaceParser(config));
            return new SurroundParser(s, child(null), s);
        }
    }

    /// <summary>
    /// Prefixes the parser with the given literal (ignoring any separators).
    /// Outputs the child parser's result.
    /// </summary>
    public class PrefixAttribute : TermAttribute
    {
        public PrefixAttribute(string value) => Value = value;
        public string Value { get; }
        public override int Priority => 80;

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
            => new SurroundParser(new LiteralParser(Value), child(null), null);
    }

    /// <summary>
    /// Appends the given literal to the parser (ignoring any separators).
    /// Outputs the child parser's result.
    /// </summary>
    public class SuffixAttribute : TermAttribute
    {
        public SuffixAttribute(string value) => Value = value;
        public string Value { get; }
        public override int Priority => 80;

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
            => new SurroundParser(null, child(null), new LiteralParser(Value));
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

        public Type OutputType => inner.OutputType;

        public void Compile(ICompileContext context)
        {
            if (prefix != null) {
                context.Child(prefix, "(Prefix)", null, null, context.Failure);
            }
            context.Child(inner, null, context.Result, null, context.Failure);
            if (suffix != null) {
                context.Child(suffix, "(Suffix)", null, null, context.Failure);
            }
            context.Succeed();
        }

        public override string ToString() => $"({prefix} {inner} {suffix})";
    }
}