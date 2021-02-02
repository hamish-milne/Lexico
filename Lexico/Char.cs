using System;
using System.Reflection;

namespace Lexico
{
    public class CharAttribute : TermAttribute
    {
        public override int Priority => 1;

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config) => CharParser.Instance;

        public override bool AddDefault(MemberInfo member) => member == typeof(char);
    }

    public class CharParser : IParser
    {
        private CharParser() { }

        public static CharParser Instance { get; } = new CharParser();

        public Type OutputType => typeof(char);

        public void Compile(Context context)
        {
            context.PopCachedResult();
            context.RequireSymbols(1);
            if (context.HasResult()) {
                context.Peek(0);
            }
            context.Advance(1);
            context.Succeed();
        }
    }
}