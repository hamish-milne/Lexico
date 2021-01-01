using System;

namespace Lexico
{
    class Trace : Feature
    {
        public Type[] DependsOn => new []{typeof(StartPosition)};
        readonly Var trace;

        public Context Before(IParser parser, Context context)
        {
            var e = context.Emitter;
            e.Call(trace, nameof(ITrace.Push), e.Const(parser), e.Const(context.Name ?? "", typeof(string)));
            return new Context(
                context.Emitter,
                context.Result ?? e.Var(null, parser.OutputType),
                context.Emitter.Label(),
                context.Emitter.Label(),
                context.Name,
                context.Features,
                context.CanWriteResult);
        }

        public void After(IParser parser, Context original, Context modified)
        {
            var e = original.Emitter;
            var p = e.Const(parser);
            var startPos = original.GetFeature<StartPosition>().Get();
            e.Mark(modified.Failure);
            e.Call(trace, nameof(ITrace.Pop), p, e.Const(true), modified.Result!,
                e.Create(typeof(StringSegment), e.Sequence, startPos, e.Difference(e.Position, startPos))
            );
            e.Jump(original.Failure);
            e.Mark(modified.Success!);
            e.Call(trace, nameof(ITrace.Pop), p, e.Const(false), modified.Result!,
                e.Create(typeof(StringSegment), e.Sequence, startPos, e.Difference(e.Position, startPos))
            );
            if (original.Success != null) {
                e.Jump(original.Success);
            }
        }
    }
}