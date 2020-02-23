using System.Reflection;
using System.Text.RegularExpressions;
using System;
using static System.Linq.Expressions.Expression;

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

        public Type OutputType => typeof(String);

        public void Compile(ICompileContext context)
        {
            context.Append(IfThen(GreaterThanOrEqual(context.Position, context.Length), Goto(context.Failure)));
            var match = context.Cache(Call(
                Constant(regex),
                nameof(Regex.Match), Type.EmptyTypes,
                context.String,
                context.Position,
                Subtract(context.Length, context.Position)
            ));
            context.Append(IfThen(Not(PropertyOrField(match, nameof(Match.Success))), Goto(context.Failure)));
            context.Append(AddAssign(context.Position, PropertyOrField(match, nameof(Match.Length))));
            context.Succeed(PropertyOrField(match, nameof(Match.Value)));
        }

        public override string ToString() => regex.ToString();
    }
}