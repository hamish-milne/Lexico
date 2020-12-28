using System;
using System.Reflection;

namespace Lexico
{
    public class LookAheadAttribute : TermAttribute
    {
        public override int Priority => 100;

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
        {
            return new LookAheadParser(child(null));
        }
    }

    public class LookAheadParser : IParser
    {
        public LookAheadParser(IParser inner) => this.inner = inner;
        private readonly IParser inner;
        public Type OutputType => typeof(void);

        public void Compile(ICompileContext context)
        {
            var savePoint = context.Save();
            context.Child(inner, null, context.Result, savePoint, context.Failure);
            context.Restore(savePoint);
            context.Succeed();
            context.Release(savePoint);
        }
    }
}