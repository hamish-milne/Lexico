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
            this.baseType = baseType;
            options = baseType.Assembly.GetTypes()
                .Where(t => (t.IsClass || t.IsValueType) && !t.IsAbstract && baseType.IsAssignableFrom(t))
                .Select(t => ParserCache.GetParser(t))
                .ToArray();
            if (options.Length == 0) {
                throw new ArgumentException($"{baseType} has no concrete options");
            }
        }

        private readonly Type baseType;
        private readonly IParser[] options;
        public bool Matches(ref Buffer buffer, ref object value, ITrace trace)
        {
            var prevValue = value;
            var prevPos = buffer.Position;
            foreach (var option in options)
            {
                if (trace.ILR.Count > 0 && trace.ILR.Peek() == option.GetInner()) {
                    continue;
                }
                buffer.Position = prevPos;
                value = prevValue;
                if (option.Matches(ref buffer, ref value, trace)) {
                    return true;
                }
            }
            return false;
        }

        public override string ToString() => $"Any {baseType.FullName}";
    }
}