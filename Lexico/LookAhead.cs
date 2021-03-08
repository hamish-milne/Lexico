using System;
using System.Reflection;

namespace Lexico
{
    public class LookAheadAttribute : TermAttribute
    {
        public override int Priority => 100;

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config) => new LookAheadParser(child(null), config, ParserFlags);
    }

    public class LookAheadParser : ParserBase
    {
        public LookAheadParser(IParser inner, IConfig config, ParserFlags parserFlags) : base(config, parserFlags) => this.inner = inner;
        private readonly IParser inner;
        public override Type OutputType => typeof(void);

        public override void Compile(ICompileContext context)
        {
            var savePoint = context.Save();
            context.Child(inner, null, context.Result, savePoint, context.Failure);
            context.Restore(savePoint);
            context.Succeed();
            context.Release(savePoint);
        }
    }
}