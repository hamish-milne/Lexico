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
        
        public ParserFlags PrefixFlags { get; set; }
        public ParserFlags SuffixFlags { get; set; }

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config) => 
            new SurroundParser(new LiteralParser(Prefix, config, PrefixFlags), child(null), new LiteralParser(Suffix, config, SuffixFlags), config, ParserFlags);
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
            var s = new OptionalParser(new WhitespaceParser(config, ParserFlags), config, ParserFlags);
            return new SurroundParser(s, child(null), s, config, ParserFlags);
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
        
        public ParserFlags PrefixFlags { get; set; }

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
            => new SurroundParser(new LiteralParser(Value, config, PrefixFlags), child(null), null, config, ParserFlags);
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
        
        public ParserFlags SuffixFlags { get; set; }

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
            => new SurroundParser(null, child(null), new LiteralParser(Value, config, SuffixFlags), config, ParserFlags);
    }

    internal class SurroundParser : ParserBase
    {
        public SurroundParser(IParser? prefix, IParser inner, IParser? suffix, IConfig config, ParserFlags flags)  : base(config, flags)
        {
            this._prefix = prefix;
            this._inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this._suffix = suffix;
        }

        private readonly IParser _inner;
        private readonly IParser? _prefix, _suffix;

        public override Type OutputType => _inner.OutputType;

        public override void Compile(ICompileContext context)
        {
            if (_prefix != null) {
                context.Child(_prefix, "(Prefix)", null, null, context.Failure);
            }
            context.Child(_inner, null, context.Result, null, context.Failure);
            if (_suffix != null) {
                context.Child(_suffix, "(Suffix)", null, null, context.Failure);
            }
            context.Succeed();
        }

        public override string ToString() => $"({_prefix} {_inner} {_suffix})";
    }
}