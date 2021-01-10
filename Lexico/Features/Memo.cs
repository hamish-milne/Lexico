using System;
using System.Collections.Generic;

namespace Lexico
{
    class Memo : Feature
    {
        private class State
        {
            private readonly HashSet<(int, int)> cache = new HashSet<(int, int)>();

            public bool Check(int id, int pos) => cache.Contains((id, pos));

            public bool Remember(int id, int pos) => cache.Add((id, pos));
        }

        private GlobalVar? memoObj;
        private readonly List<IParser> parsersById = new List<IParser>();
        private readonly Stack<(Var, Label)> store = new Stack<(Var, Label)>();

        public Context Before(IParser parser, Context context)
        {
            var e = context.Emitter;
            if (memoObj == null) {
                memoObj = e.Global(null, typeof(State));
                e.Copy(e.GlobalRef(memoObj), e.Create(typeof(State)));
            }
            var id = parsersById.IndexOf(parser);
            if (id < 0) {
                id = parsersById.Count;
                parsersById.Add(parser);
            }
            var memoEnd = e.Label();
            store.Push((e.Const(id), memoEnd));
            e.Compare(
                e.Call(e.GlobalRef(memoObj), nameof(State.Check), store.Peek().Item1, context.Position),
                CompareOp.Equal, e.Const(true), context.Failure);
            return new Context(context.Emitter, context.Result, context.Success, e.Label(), null, context.Features, context.CanWriteResult);
        }

        public void After(IParser parser, Context original, Context modified)
        {
            var e = original.Emitter;
            var startPos = original.GetFeature<StartPosition>().Get();
            var (id, memoEnd) = store.Pop();
            var skip = e.Label();
            e.Jump(skip);
            e.Mark(modified.Failure);
            e.Call(e.GlobalRef(memoObj), nameof(State.Remember), id, startPos);
            e.Jump(original.Failure);
            e.Mark(skip);
        }
    }
}