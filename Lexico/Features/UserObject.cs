using System;

namespace Lexico
{
    class UserObject : Feature
    {
        public void After(IParser parser, Context original, Context modified) {}

        public Context Before(IParser parser, Context context, ref bool skipContent)
        {
            if (Global == null) {
                Global = context.Emitter.Global(null, typeof(object));
            }
            return context;
        }

        public GlobalVar Global { get; private set; } = null!;
    }
}