using System.Linq;
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
        public bool Matches(ref IContext context, ref object? value)
        {
            foreach (var option in options)
            {
                if (option.MatchChild(null, ref context, ref value)) {
                    return true;
                }
            }
            return false;
        }

        public override string ToString() => $"Any {baseType.Name}";
    }
}