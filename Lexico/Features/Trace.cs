namespace Lexico
{
    class Trace : Feature
    {
        public GlobalVar TraceObj { get; private set; } = null!;

        public Context Before(IParser parser, Context context, ref bool skipContent)
        {
            // TODO: More robust system for ignoring in trace
            // if (parser is RecursiveParser) {
            //     return context;
            // }
            var e = context.Emitter;
            if (TraceObj == null) {
                TraceObj = e.Global(null, typeof(ITrace));
            }
            e.Call(e.GlobalRef(TraceObj), nameof(ITrace.Push), e.Const(parser),
                context.Name == null ? e.Default(typeof(string)) : e.Const(context.Name, typeof(string))
            );
            return new Context(
                context.Emitter,
                context.Result ?? (parser.OutputType == typeof(void) ? null : e.Var(null, parser.OutputType)),
                context.Emitter.Label(),
                context.Emitter.Label(),
                context.Name,
                context.Features, 
                context.CanWriteResult);
        }

        public void After(IParser parser, Context original, Context modified)
        {
            if (original == modified) {
                return;
            }
            var e = original.Emitter;
            var p = e.Const(parser);
            var startPos = original.GetFeature<StartPosition>().Get();
            e.Mark(modified.Failure);
            var trace = e.GlobalRef(TraceObj);
            e.Call(trace, nameof(ITrace.Pop), p, e.Const(false), modified.Result ?? e.Default(typeof(object)),
                e.Create(typeof(StringSegment), original.Sequence, startPos, e.Difference(original.Position, startPos))
            );
            e.Jump(original.Failure);
            e.Mark(modified.Success!);
            e.Call(trace, nameof(ITrace.Pop), p, e.Const(true), modified.Result ?? e.Default(typeof(object)),
                e.Create(typeof(StringSegment), original.Sequence, startPos, e.Difference(original.Position, startPos))
            );
            if (original.Success != null) {
                e.Jump(original.Success);
            }
        }
    }
}