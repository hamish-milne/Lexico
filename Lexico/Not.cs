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

        public void Compile(ICompileContext context)
        {
            var success = context.Success ?? Label();
            context.Child(inner, null, null, context.Failure, success);
            if (success != context.Success) {
                context.Append(Label(success));
                context.Succeed();
            }
        }
    }
}