using System;
using System.Linq.Expressions;
using System.Reflection;
using static System.Linq.Expressions.Expression;

namespace Lexico
{
    /// <summary>
    /// Matches a new-line string (LF or CRLF). No output
    /// </summary>
    public class EOLAttribute : TermAttribute
    {
        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
            => new EOLParser(config, ParserFlags);
    }

    /// <summary>
    /// Matches a new-line string (LF or CRLF). No output
    /// </summary>
    [EOL] public struct EOL {}

    internal class EOLParser : ParserBase
    {
        public EOLParser(IConfig config, ParserFlags flags) : base(config, flags) {}

        public override Type OutputType => typeof(void);

        public override void Compile(ICompileContext context)
        {
            var breakTarget = Label();
            var fail = Goto(context.Failure);
            var succeed = Goto(breakTarget);
            context.Append(IfThen(GreaterThanOrEqual(context.Position, context.Length), succeed));
            context.Append(Switch(context.Peek(0), fail,
                SwitchCase(Block(AddAssign(context.Position, Constant(1)), succeed), Constant('\n')),
                SwitchCase(IfThenElse(And(
                        LessThan(Add(Constant(1), context.Position), context.Length),
                        Equal(context.Peek(1), Constant('\n'))
                    ),
                    Block(AddAssign(context.Position, Constant(2)), succeed),
                    fail), Constant('\r'))
            ));
            context.Append(Label(breakTarget));
            context.Succeed(Empty());
        }
    }
}