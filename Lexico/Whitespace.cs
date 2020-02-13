using System.Text.RegularExpressions;
using System;
using System.Reflection;

namespace Lexico
{
    /// <summary>
    /// Matches any amount of whitespace (at least one character).
    /// New-lines do not count by default; see the MultiLine attribute
    /// </summary>
    public class WhitespaceAttribute : TerminalAttribute
    {
        public override IParser Create(MemberInfo member, IConfig config)
            => new WhitespaceParser(config);
    }

    /// <summary>
    /// Matches any amount of whitespace (at least one character).
    /// New-lines do not count by default; see the MultiLine attribute
    /// </summary>
    [Whitespace] public struct Whitespace {}

    internal class WhitespaceParser : IParser
    {
        public WhitespaceParser(IConfig config) {
            multiline = (config.Get<RegexOptions>(default) & RegexOptions.Multiline) != 0;
        }

        private readonly bool multiline;

        public bool Matches(ref IContext context, ref object? value)
        {
            int idx = 0;
            var c = context.Peek(idx);
            if (MatchChar(c)) {
                do {
                    idx++;
                    c = context.Peek(idx);
                } while (MatchChar(c));
                context = context.Advance(idx);
                return true;
            } else {
                return false;
            }
        }

        private bool MatchChar(char? c)
            => c.HasValue && (multiline || c != '\n') && Char.IsWhiteSpace(c.Value);

        public override string ToString() => "Whitespace";
    }
}