
using System;

namespace Lexico
{
    class String : Feature
    {
        public GlobalVar Length { get; private set; } = null!;
        public GlobalVar Sequence { get; private set; } = null!;
        public GlobalVar Position { get; private set; } = null!;

        public void After(IParser parser, Context original, Context modified)
        {
        }

        public Context Before(IParser parser, Context context)
        {
            if (Sequence == null) {
                var e = context.Emitter;
                Sequence = e.Global(null, typeof(string));
                Length = e.Global(0, typeof(int));
                Position = e.Global(0, typeof(int));
                e.Copy(e.GlobalRef(Length), e.Load(e.GlobalRef(Sequence), typeof(string).GetProperty(nameof(string.Length))));
            }
            return context;
        }
    }
}