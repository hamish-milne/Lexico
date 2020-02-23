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
        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config)
            => EOLParser.Instance;
    }

    /// <summary>
    /// Matches a new-line string (LF or CRLF). No output
    /// </summary>
    [EOL] public struct EOL {}

    internal class EOLParser : IParser
    {
        private EOLParser() {}

        public static EOLParser Instance { get; } = new EOLParser();

        public Type OutputType => typeof(void);

        public void Compile(ICompileContext context)
        {
            var breakTarget = Label();
            var fail = Goto(context.Failure);
            var succeed = Goto(breakTarget);
            context.Append(Switch(context.Peek(0), fail,
                SwitchCase(Block(AddAssign(context.Position, Constant(1)), succeed), Constant('\n')),
                SwitchCase(IfThenElse(Equal(context.Peek(1), Constant('\n')),
                    Block(AddAssign(context.Position, Constant(2)), succeed),
                    fail), Constant('\r'))
            ));
            context.Append(Label(breakTarget));
            context.Succeed(Empty());
        }
    }
}