using System;
using System.Reflection;

namespace Lexico
{
    public class UserObjectAttribute : TerminalAttribute
    {
        public override IParser Create(MemberInfo member, IConfig config) => new UserObjectWriter(config, ParserFlags);
    }

    internal class UserObjectWriter : ParserBase
    {
        public UserObjectWriter(IConfig config, ParserFlags flags) : base(config, flags) {}
        public override Type OutputType => typeof(object);

        public override void Compile(ICompileContext context) => context.Succeed(context.UserObject);
    }
}