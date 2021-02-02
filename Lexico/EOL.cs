using System;
using System.Reflection;

namespace Lexico
{
    /// <summary>
    /// Matches a new-line string (LF or CRLF). No output
    /// </summary>
    public class EOLAttribute : TermAttribute
    {
        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
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

        public void Compile(Context context)
        {
            var e = context.Emitter;
            var skip = e.Label();
            context.Peek(0);
            e.Const('\r');
            e.Jump(CMP.NotEqual, skip);
            context.Advance(1);
            e.Mark(skip);
            context.RequireSymbols(1);
            context.Peek(0);
            e.Const('\n');
            e.Jump(CMP.NotEqual, context.Failure);
            context.Advance(1);
            context.Succeed();
        }
    }
}