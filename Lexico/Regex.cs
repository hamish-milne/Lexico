using System.Reflection;
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
            => RegexImpl.Regex.Parse(Pattern, config, ParserFlags);
    }
}