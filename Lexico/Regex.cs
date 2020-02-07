using System.Text.RegularExpressions;
using System;

namespace Lexico
{
    public class RegexAttribute : TerminalAttribute
    {
        public RegexAttribute(string pattern) {
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        }

        public string Pattern { get; }
    }

    internal class RegexParser : IParser
    {
        // TODO: Case-insensitive etc.?
        public RegexParser(RegexAttribute attr) {
            regex = new Regex($"^{attr.Pattern}", RegexOptions.Compiled);
        }
        private readonly Regex regex;
        public bool Matches(ref IContext context, ref object? value)
        {
            var str = context.Text;
            var match = regex.Match(str, context.Position, str.Length - context.Position);
            if (match.Success) {
                value = match.Value;
                context = context.Advance(match.Value.Length);
                return true;
            }
            return false;
        }

        public override string ToString() => regex.ToString();
    }
}