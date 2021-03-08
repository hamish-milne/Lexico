using System;
using System.Reflection;

namespace Lexico
{
    public class PassAttribute : TerminalAttribute
    {
        public override IParser Create(MemberInfo member, IConfig config) => new Pass(config, ParserFlags);
    }

    internal class Pass : ParserBase
    {
        public Pass(IConfig config, ParserFlags flags) : base(config, flags) { }
        public override Type OutputType => typeof(void);

        public override void Compile(ICompileContext context) => context.Succeed();
    }
}