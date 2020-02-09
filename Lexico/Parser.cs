using System;

namespace Lexico
{
    public class Parser
    {
        public static T Parse<T>(string str, ITrace trace)
        {
            if (TryParse<T>(str, out var output, trace)) {
                return output;
            } else {
                throw new FormatException($"Value could not be parsed as {typeof(T)}");
            }
        }

        public static bool TryParse<T>(string str, out T output, ITrace trace)
        {
            object? value = null;
            IContext context = Context.CreateRoot(str, trace);
            var result = ParserCache.GetParser(typeof(T), null).MatchChild(null, ref context, ref value);
            output = result ? (T)value! : default!;
            return result;
        }
    }
}
