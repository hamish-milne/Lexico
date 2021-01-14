using System;
using static System.Linq.Expressions.Expression;
using System.Reflection;

namespace Lexico
{
    public class CharAttribute : TermAttribute
    {
        public override int Priority => 1;

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config) => new CharParser(config, ParserFlags);

        public override bool AddDefault(MemberInfo member) => member == typeof(char);
    }

    public class CharParser : ParserBase
    {
        public CharParser(IConfig config, ParserFlags flags) : base(config, flags) { }

        public override Type OutputType => typeof(char);

        public override void Compile(ICompileContext context)
        {
            context.Append(IfThen(GreaterThanOrEqual(context.Position, context.Length), Goto(context.Failure)));
            context.Advance(1);
            context.Succeed(context.Peek(-1));
        }
    }
}