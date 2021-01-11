using System;
using System.Collections.Generic;

namespace Lexico
{
    class StartPosition : Feature
    {
        private readonly Stack<Var> stored = new Stack<Var>();

        public Context Before(IParser parser, Context context, ref bool skipContent)
        {
            stored.Push(context.Emitter.Copy(context.Position));
            return context;
        }

        public void After(IParser parser, Context original, Context modified)
        {
            stored.Pop();
        }

        public Var Get() => stored.Peek();
    }
}