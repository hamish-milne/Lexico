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
            var success = e.Label();
            context.RequireSymbols(1);
            e.Compare(e.Peek(0), CompareOp.Equal, '\n', success);
            e.Compare(e.Peek(0), CompareOp.NotEqual, '\r', context.Failure);
            context.Advance(1);
            e.Compare(e.GetSymbolsRemaining(), CompareOp.LessOrEqual, 0, success);
            e.Compare(e.Peek(0), CompareOp.NotEqual, '\n', success);
            context.Advance(1);
            e.Mark(success);
            context.Advance(1);
            context.Succeed();
        }
    }
}