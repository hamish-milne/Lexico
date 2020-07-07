using System;
using System.Reflection;

namespace Lexico
{
    public class UserObjectAttribute : TerminalAttribute
    {
        public override IParser Create(MemberInfo member, IConfig config) => UserObjectWriter.Instance;
    }

    internal class UserObjectWriter : IParser
    {
        private UserObjectWriter() {}
        public static IParser Instance { get; } = new UserObjectWriter();
        public Type OutputType => typeof(object);

        public void Compile(ICompileContext context)
        {
            context.Succeed(context.UserObject);
        }
    }
}