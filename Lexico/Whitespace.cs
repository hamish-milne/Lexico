using System;

namespace Lexico
{
    public struct Whitespace {}

    internal class WhitespaceParser : IParser
    {
        private static readonly object ws = new Whitespace();
        public static WhitespaceParser Instance { get; } = new WhitespaceParser();

        public bool Matches(ref Buffer buffer, ref object value)
        {
            var c = buffer.Peek(0);
            if (c.HasValue && Char.IsWhiteSpace(c.Value)) {
                do {
                    buffer.Position++;
                    c = buffer.Peek(0);
                } while (c.HasValue && Char.IsWhiteSpace(c.Value));
                if (value == null) {
                    value = ws;
                }
                return true;
            } else {
                return false;
            }
        }
    }
}