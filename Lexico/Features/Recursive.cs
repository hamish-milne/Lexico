using System.Collections.Generic;
using System;

namespace Lexico
{
    class Recursive : Feature
    {
        private readonly Dictionary<IParser, Emitter> programs = new Dictionary<IParser, Emitter>();

        public static bool IsRecursive(IParser parser) => ParserCache.IsRecursive(parser);

        public Context Before(IParser parser, Context context)
        {
            if (IsRecursive(parser) && !programs.ContainsKey(parser)) {
                var ctx = context.Emitter.MakeRecursive(parser.OutputType);
                programs.Add(parser, ctx.Emitter);
                return ctx;
            } else {
                return context;
            }
        }

        public void After(IParser parser, Context original, Context modified)
        {
            if (IsRecursive(parser)) {
                original.Emitter.CallRecursive(modified.Emitter, original.Result, original.Success, original.Failure);
            }
        }

        public void CallFromPlaceholder(IParser parser, Context context)
        {
            context.Emitter.CallRecursive(programs[parser], context.Result, context.Success, context.Failure);
        }
    }
}