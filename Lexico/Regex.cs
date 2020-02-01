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
            regex = new Regex(attr.Pattern, RegexOptions.Compiled);
        }
        private readonly Regex regex;
        public bool Matches(ref Buffer buffer, ref object value)
        {
            var match = regex.Match(buffer.String, buffer.Position);
            if (match.Success) {
                value = match.Value;
                buffer.Position += match.Value.Length;
                return true;
            }
            return false;
        }
    }
}