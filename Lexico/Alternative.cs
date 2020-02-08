using System.Linq;
using System;
using System.Reflection;

namespace Lexico
{
    public class AlternativeAttribute : TermAttribute
    {
        public override int Priority => 10;
        public override IParser Create(MemberInfo member, Func<IParser> child)
        {
            return new AlternativeParser(member.GetMemberType());
        }

        public override bool AddDefault(MemberInfo member)
            => member is Type t && (t.IsInterface || t.IsAbstract);
    }

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