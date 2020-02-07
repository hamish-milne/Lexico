using System;

namespace Lexico
{
    public struct Whitespace {}

    internal class WhitespaceParser : IParser
    {
        private static readonly object ws = new Whitespace();
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
                if (value == null) {
                    value = ws;
                }
                context = context.Advance(idx);
                return true;
            } else {
                return false;
            }
        }

        public override string ToString() => "Whitespace";
    }
}