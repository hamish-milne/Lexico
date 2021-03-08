using System;
using System.Reflection;

namespace Lexico
{
    public class NotAttribute : TermAttribute
    {
        public override int Priority => 100;

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config) => new NotParser(child(null), config, ParserFlags);
    }

    public class NotParser : ParserBase
    {
        public NotParser(IParser inner, IConfig config, ParserFlags flags) : base(config, flags) => this._inner = inner;
        private readonly IParser _inner;
        public override Type OutputType => typeof(void);

        public override void Compile(ICompileContext context)
        {
            var savePoint = context.Save();
            context.Child(_inner, null, context.Result, context.Failure, savePoint);
            context.Restore(savePoint);
            context.Succeed();
            context.Release(savePoint);
        }
    }
}