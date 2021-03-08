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
        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
        {
            if (member == typeof(EOF)) {
                return new EOFParser(config, ParserFlags);
            }
            return new SurroundParser(null, child(null), new EOFParser(config, ParserFlags), config, ParserFlags);
        }
    }

    /// <summary>
    /// Only matches the end-of-file (i.e. expects the Position to be past the end of the text). No output
    /// </summary>
    [TopLevel] public struct EOF {}

    internal class EOFParser : ParserBase
    {
        public EOFParser(IConfig config, ParserFlags flags) : base(config, flags) {}

        public override string ToString() => "EOF";

        public override Type OutputType => typeof(void);

        public override void Compile(ICompileContext context)
        {
            context.Append(IfThen(LessThan(context.Position, context.Length), Goto(context.Failure)));
            context.Succeed(Empty());
        }
    }

    public class SOFAttribute : TermAttribute
    {
        public override int Priority => 110;
        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
        {
            if (member == typeof(SOF)) {
                return new SOFParser(config, ParserFlags);
            }
            return new SurroundParser(null, child(null), new SOFParser(config, ParserFlags), config, ParserFlags);
        }
    }

    /// <summary>
    /// Only matches the start-of-file (i.e. expects the Position to be at 0). No output
    /// </summary>
    [SOF] public struct SOF {}

    internal class SOFParser : ParserBase
    {
        public SOFParser(IConfig config, ParserFlags flags) : base(config, flags) {}

        public override string ToString() => "SOF";

        public override Type OutputType => typeof(void);

        public override void Compile(ICompileContext context)
        {
            context.Append(IfThen(GreaterThan(context.Position, Constant(0)), Goto(context.Failure)));
            context.Succeed(Empty());
        }
    } 
}