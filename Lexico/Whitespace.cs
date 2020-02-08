using System;
using System.Reflection;

namespace Lexico
{
    public class WhitespaceAttribute : TerminalAttribute
    {
        public override IParser Create(MemberInfo member) => WhitespaceParser.Instance;
    }

    [Whitespace] public struct Whitespace {}

    internal class WhitespaceParser : IParser
    {
        private WhitespaceParser() {}

        public static WhitespaceParser Instance { get; } = new WhitespaceParser();

        public bool Matches(ref IContext context, ref object? value)
        {
            int idx = 0;
            var c = context.Peek(idx);
            if (c.HasValue && Char.IsWhiteSpace(c.Value)) {
                do {
                    idx++;
                    c = context.Peek(idx);
                } while (c.HasValue && Char.IsWhiteSpace(c.Value));
                context = context.Advance(idx);
                return true;
            } else {
                return false;
            }
        }

        public override string ToString() => "Whitespace";
    }
}