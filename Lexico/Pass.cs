using System;
using System.Reflection;

namespace Lexico
{
    public class PassAttribute : TerminalAttribute
    {
        public override IParser Create(MemberInfo member, IConfig config)
        {
            return Pass.Instance;
        }
    }

    internal class Pass : IParser
    {
        public static IParser Instance { get; } = new Pass();
        public Type OutputType => typeof(void);

        public void Compile(Context context)
        {
            context.Succeed();
        }
    }
}