using System;
using System.Collections.Generic;

namespace Lexico
{
    class CheckILR : Feature
    {
        private readonly Stack<bool> checkStack = new Stack<bool>();
        private readonly List<IParser> targets = new List<IParser>();
        private GlobalVar? flags;
        private GlobalVar? ilrPos;

        public GlobalVar Pos => ilrPos ?? throw new InvalidOperationException();
        public GlobalVar Flags => flags ?? throw new InvalidOperationException();

        private static bool ShouldCheckILR(IParser parser) => parser is AlternativeParser || parser is OptionalParser;

        public Context Before(IParser parser, Context context, ref bool skipContent)
        {
            var e = context.Emitter;
            if (flags == null) {
                flags = e.Global(0, typeof(int));
            }
            if (ilrPos == null) {
                ilrPos = e.Global(0, e.TypeOf(context.Position));
            }
            if (checkStack.Count > 0 && checkStack.Peek()) {
                if (!targets.Contains(parser)) {
                    if (targets.Count >= 32) {
                        throw new Exception("Too many recursion targets - max of 32");
                    }
                    targets.Add(parser);
                }
                var id = targets.IndexOf(parser);

                
                var _flags = e.GlobalRef(flags);
                var _ilrPos = e.GlobalRef(ilrPos);
                
                var skip1 = e.Label();
                var skip2 = e.Label();
                e.Compare(context.Position, CompareOp.Greater, _ilrPos, skip1);
                e.CheckFlag(_flags, id, true, context.Failure);
                e.Jump(skip2);
                e.Mark(skip1);
                e.Set(_flags, 0);
                e.Copy(_ilrPos, context.Position);
                e.Mark(skip2);
                e.SetFlag(_flags, id, true);
            }
            checkStack.Push(Recursive.IsRecursive(parser) && ShouldCheckILR(parser));
            return context;
        }

        public void After(IParser parser, Context original, Context modified)
        {
            checkStack.Pop();
        }
    }
}