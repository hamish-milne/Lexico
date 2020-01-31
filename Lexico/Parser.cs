using System;

namespace Lexico
{
    public class Parser
    {
        public static T Parse<T>(string str)
        {
            var buf = new Buffer(str);
            object value = null;
            if (ParserCache.GetParser(typeof(T)).Matches(ref buf, ref value)) {
                return (T)value;
            } else {
                throw new Exception("Parse failed");
            }
        }
    }
}
