using System;
using System.Reflection;
using static System.AttributeTargets;

namespace Lexico
{
    [AttributeUsage(Class | Struct, AllowMultiple = false)]
    public class TopLevelAttribute : TermAttribute
    {
        public override int Priority => 110;
        public override IParser Create(MemberInfo member, Func<IParser> child)
        {
            if (member == typeof(EOF)) {
                return EOFParser.Instance;
            }
            return new SurroundParser(null, child(), EOFParser.Instance);
        }
    }

    [TopLevel] public struct EOF {}

    internal class EOFParser : IParser
    {
        public static EOFParser Instance { get; } = new EOFParser();
        private EOFParser() {}

        public bool Matches(ref IContext context, ref object? value)
            => !context.Peek(0).HasValue;

        public override string ToString() => "EOF";
    }
}