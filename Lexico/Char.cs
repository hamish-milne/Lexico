using System;
using static System.Linq.Expressions.Expression;
using System.Reflection;

namespace Lexico
{
    public class CharAttribute : TermAttribute
    {
        public override int Priority => 1;

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config) => CharParser.Instance;

        public override bool AddDefault(MemberInfo member) => member == typeof(char);
    }

    public class CharParser : IParser
    {
        private CharParser() { }

        public static CharParser Instance { get; } = new CharParser();

        public Type OutputType => typeof(char);

        public void Compile(ICompileContext context)
        {
            context.Append(IfThen(GreaterThanOrEqual(context.Position, context.Length), Goto(context.Failure)));
            context.Advance(1);
            context.Succeed(context.Peek(-1));
        }
    }
}