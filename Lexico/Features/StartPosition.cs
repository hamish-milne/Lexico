using System;
using System.Collections.Generic;

namespace Lexico
{
    class StartPosition : Feature
    {
        private readonly Stack<Var> stored = new Stack<Var>();

        public Type[] DependsOn => Array.Empty<Type>();

        public Context Before(IParser parser, Context context)
        {
            stored.Push(context.Emitter.Copy(context.Emitter.Position));
            return context;
        }

        public void After(IParser parser, Context original, Context modified)
        {
            stored.Pop();
        }

        public Var Get() => stored.Peek();
    }
}