using System;
using System.Reflection;
using static System.AttributeTargets;
using static System.Linq.Expressions.Expression;

namespace Lexico
{
    /// <summary>
    /// Indicates that this parser should match the entire string, allowing no left-over text
    /// </summary>
    [AttributeUsage(Class | Struct, AllowMultiple = false)]
    public class TopLevelAttribute : TermAttribute
    {
        public override int Priority => 110;
        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config)
        {
            if (member == typeof(EOF)) {
                return EOFParser.Instance;
            }
            return new SurroundParser(null, child(), EOFParser.Instance);
        }
    }

    /// <summary>
    /// Only matches the end-of-file (i.e. expects the Position to be past the end of the text). No output
    /// </summary>
    [TopLevel] public struct EOF {}

    internal class EOFParser : IParser
    {
        public static EOFParser Instance { get; } = new EOFParser();
        private EOFParser() {}

        public override string ToString() => "EOF";

        public Type OutputType => typeof(void);

        public void Compile(ICompileContext context)
        {
            context.Append(IfThen(GreaterThanOrEqual(context.Position, context.Length), Goto(context.Failure)));
            context.Succeed(Empty());
        }
    }
}