using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;

namespace Lexico
{

    internal class AlternativeParser : IParser
    {
        public AlternativeParser(Type baseType)
        {
            options = baseType.Assembly.GetTypes()
                .Where(t => (t.IsClass || t.IsValueType) && !t.IsAbstract && baseType.IsAssignableFrom(t))
                .Select(t => ParserCache.GetParser(t))
                .ToArray();
            if (options.Length == 0) {
                throw new ArgumentException($"{baseType} has no concrete options");
            }
        }

        // TODO: Pass this in to Matches as a 'context'; this isn't thread-safe atm.
        private static readonly Stack<IParser> currentSet = new Stack<IParser>();

        private readonly IParser[] options;
        public bool Matches(ref Buffer buffer, ref object value)
        {
            var prevValue = value;
            var prevPos = buffer.Position;
            foreach (var option in options)
            {
                if (currentSet.Count > 0 && currentSet.Peek() == option) {
                    continue;
                }
                buffer.Position = prevPos;
                value = prevValue;
                currentSet.Push(option);
                if (option.Matches(ref buffer, ref value)) {
                    currentSet.Pop();
                    return true;
                }
                currentSet.Pop();
            }
            return false;
        }
    }
}