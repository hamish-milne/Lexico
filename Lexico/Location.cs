using System;
using System.Reflection;

namespace Lexico
{
    public class LocationAttribute : TerminalAttribute
    {
        public override IParser Create(MemberInfo member, IConfig config) => new LocationWriter(config, ParserFlags);
    }

    internal class LocationWriter : ParserBase
    {
        public LocationWriter(IConfig config, ParserFlags flags) : base(config, flags) {}
        public override Type OutputType => typeof(int);
        public override void Compile(ICompileContext context) => context.Succeed(context.Position);
    }
}
