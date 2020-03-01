using System.Reflection;
using System;
using static System.AttributeTargets;

namespace Lexico
{
    /// <summary>
    /// Applied to any Repeat or Sequence, inserts the given parser between each element
    /// </summary>
    [AttributeUsage(Field | Property | Class | Struct, AllowMultiple = false)]
    public class SeparatedByAttribute : TermAttribute
    {
        public SeparatedByAttribute(Type separator) {
            separatorType = separator ?? throw new ArgumentNullException(nameof(separator));
        }

        public SeparatedByAttribute(string separator) {
            separatorString = separator ?? throw new ArgumentNullException(nameof(separator));
        }

        private readonly Type? separatorType;
        private readonly string? separatorString;

        protected virtual IParser GetSeparator(IConfig config) => separatorType == null
            ? new LiteralParser(separatorString!)
            : ParserCache.GetParser(separatorType);

        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config)
        {
            var c = child();
            var sep = GetSeparator(config);
            return c switch {
                RepeatParser r => new RepeatParser(r.OutputType, r.Element, sep, r.Min, r.Max),
                SequenceParser s => new SequenceParser(s.Type, sep),
                _ => throw new ArgumentException($"Separator not valid on {c}")
            };
        }
    }

    /// <summary>
    /// Applied to any Repeat or Sequence, inserts optional whitespace between each element
    /// </summary>
    public class WhitespaceSeparatedAttribute : SeparatedByAttribute
    {
        public WhitespaceSeparatedAttribute() : base(typeof(Whitespace?)) {}

        // TODO: Not this. Maybe GetParserUncached that accepts IConfig?
        protected override IParser GetSeparator(IConfig config) =>
            new OptionalParser(new WhitespaceParser(config));
    }
}