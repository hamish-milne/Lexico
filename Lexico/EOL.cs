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

        public override void Compile(Context context)
        {
            var e = context.Emitter;
            var success = e.Label();
            context.RequireSymbols(1);
            e.Compare(context.Peek(0), CompareOp.Equal, '\n', success);
            e.Compare(context.Peek(0), CompareOp.NotEqual, '\r', context.Failure);
            context.Advance(1);
            e.Compare(context.GetSymbolsRemaining(), CompareOp.LessOrEqual, 0, success);
            e.Compare(context.Peek(0), CompareOp.NotEqual, '\n', success);
            context.Advance(1);
            e.Mark(success);
            context.Advance(1);
            context.Succeed();
        }
    }
}