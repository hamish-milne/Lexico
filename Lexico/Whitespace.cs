using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System;
using System.Reflection;
using static System.Linq.Expressions.Expression;

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

        public Type OutputType => typeof(void);

        public void Compile(ICompileContext context)
        {
            var isWhiteSpace = typeof(Char).GetMethod(nameof(Char.IsWhiteSpace), BindingFlags.Static | BindingFlags.Public);
            Expression test = Call(isWhiteSpace, context.Peek(0));
            if (!multiline) {
                test = And(NotEqual(context.Peek(0), Constant('\n')), test);
            }
            context.Append(IfThen(Not(test), Goto(context.Failure)));
            var loop = Label();
            context.Append(Label(loop));
            context.Advance(1);
            context.Append(IfThen(test, Goto(loop)));
            context.Succeed();
        }

        public override string ToString() => "Whitespace";
    }
}