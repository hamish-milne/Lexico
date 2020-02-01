using System;

namespace Lexico
{
    public class Parser
    {
        public static T Parse<T>(string str)
        {
            var buf = new Buffer(str);
            object value = null;
            var trace = new Trace();
            if (ParserCache.GetParser(typeof(T)).Matches(ref buf, ref value, trace)) {
                Console.WriteLine(trace);
                return (T)value;
            } else {
                Console.WriteLine(trace);
                throw new Exception("Parse failed");
            }
        }
    }
}
