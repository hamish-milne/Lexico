using System;
using System.Collections.Generic;

namespace Lexico
{
    class CheckILR : Feature
    {
        private readonly Stack<IParser> parsers = new Stack<IParser>();
        private readonly List<IParser> targets = new List<IParser>();
        private readonly Dictionary<IParser, Var> ilrPos = new Dictionary<IParser, Var>();
        private GlobalVar? flags;

        public Context Before(IParser parser, Context context)
        {
            var e = context.Emitter;
            if (flags == null) {
                flags = e.Global(0, typeof(int));
            }
            if (Recursive.IsRecursive(parser)) {
                ilrPos.Add(parser, e.Copy(context.Position));
            }
            if (parsers.Count > 0 && Recursive.IsRecursive(parsers.Peek())) {
                if (!targets.Contains(parser)) {
                    if (targets.Count >= 32) {
                        throw new Exception("Too many recursion targets - max of 32");
                    }
                    targets.Add(parser);
                }
                var id = targets.IndexOf(parser);
                
                var skip1 = e.Label();
                e.Compare(context.Position, CompareOp.Greater, ilrPos[parsers.Peek()], skip1);
                var _flags = e.GlobalRef(flags);
                e.CheckFlag(_flags, id, true, context.Failure);
                e.SetFlag(_flags, id, true);
                e.Mark(skip1);
            }
            parsers.Push(parser);
            return context;
        }

        public void After(IParser parser, Context original, Context modified)
        {
            parsers.Pop();
            var id = targets.IndexOf(parser);
            if (id >= 0 && parsers.Count > 0 && Recursive.IsRecursive(parsers.Peek())) {
                var _flags = original.Emitter.GlobalRef(flags!);
                original.Emitter.SetFlag(_flags, id, false);
            }
        }
    }
}