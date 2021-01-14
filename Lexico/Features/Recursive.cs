using System.Collections.Generic;
using System;

namespace Lexico
{
    class Recursive : Feature
    {
        private readonly Dictionary<IParser, Emitter> programs = new Dictionary<IParser, Emitter>();

        public static bool IsRecursive(IParser parser) => ParserCache.IsRecursive(parser);

        public Context Before(IParser parser, Context context, ref bool skipContent)
        {
            if (IsRecursive(parser)) {
                if (programs.ContainsKey(parser)) {
                    skipContent = true;
                    return context;
                } else {
                    var ctx = context.Emitter.MakeRecursive(parser.OutputType);
                    programs.Add(parser, ctx.Emitter);
                    return ctx;
                }
            } else {
                return context;
            }
        }

        public void After(IParser parser, Context original, Context modified)
        {
            if (IsRecursive(parser)) {
                original.Emitter.CallRecursive(programs[parser], original.Result, original.Success, original.Failure);
            }
        }

        public void CallFromPlaceholder(IParser parser, Context context)
        {
            context.Emitter.CallRecursive(programs[parser], context.Result, context.Success, context.Failure);
        }
    }
}