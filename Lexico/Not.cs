using System;
using System.Reflection;
using static System.Linq.Expressions.Expression;

namespace Lexico
{
    public class NotAttribute : TermAttribute
    {
        public override int Priority => 100;

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
        {
            return new NotParser(child(null));
        }
    }

    public class NotParser : IParser
    {
        public NotParser(IParser inner) => this.inner = inner;
        private readonly IParser inner;
        public Type OutputType => typeof(void);

        public void Compile(Context context)
        {
            var savePoint = context.Save();
            context.Child(inner, null, context.Result, context.Failure, savePoint.label);
            context.Restore(savePoint);
            context.Succeed();
        }
    }
}