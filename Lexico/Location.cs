using System;
using System.Reflection;

namespace Lexico
{
    public class LocationAttribute : TerminalAttribute
    {
        public override IParser Create(MemberInfo member, IConfig config) => LocationWriter.Instance;
    }

    internal class LocationWriter : IParser
    {
        private LocationWriter() {}
        public static IParser Instance { get; } = new LocationWriter();
        public Type OutputType => typeof(int);

        public void Compile(Context context)
        {
            context.Succeed(context.Position);
        }
    }
}
