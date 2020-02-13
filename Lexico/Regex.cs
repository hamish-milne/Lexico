using System.Reflection;
using System.Text.RegularExpressions;
using System;

namespace Lexico
{
    /// <summary>
    /// Matches a regular expression, using the RegexOptions configuration. Outputs the matched text
    /// </summary>
    public class RegexAttribute : TerminalAttribute
    {
        public RegexAttribute(string pattern) {
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        }

        public string Pattern { get; }

        public override IParser Create(MemberInfo member, IConfig config)
            => new RegexParser(Pattern, config.Get(RegexOptions.Compiled));
    }

    internal class RegexParser : IParser
    {
        public RegexParser(string pattern, RegexOptions options) {
            regex = new Regex($"^{pattern}", options);
        }
        private readonly Regex regex;
        public bool Matches(ref IContext context, ref object? value)
        {
            var str = context.Text;
            if (context.Position >= str.Length) {
                return false;
            }
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