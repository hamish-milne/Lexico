using System;
using System.Reflection;

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

        public bool Matches(ref IContext context, ref object? value)
        {
            switch (context.Peek(0)) {
                case '\n':
                    context = context.Advance(1);
                    return true;
                case '\r':
                    if (context.Peek(1) == '\n') {
                        context = context.Advance(2);
                        return true;
                    }
                    break;
            }
            return false;
        }
    }
}