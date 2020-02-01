using System.Linq;
using System;
using System.Reflection;
using static System.Reflection.BindingFlags;

namespace Lexico
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class IgnoreAttribute : Attribute
    {
    }

    public class WhitespaceSeparatedAttribute : SeparatedByAttribute
    {
        public WhitespaceSeparatedAttribute() : base(typeof(Whitespace?)) {}
    }

    internal class SequenceParser : IParser
    {
        public SequenceParser(Type type)
        {
            this.type = type ?? throw new ArgumentNullException(nameof(type));
            members = type.GetMembers(Instance | Public | NonPublic)
                .Where(m => m is FieldInfo)
                .Where(m => m.GetCustomAttribute<IgnoreAttribute>() == null)
                .Select(m => (MemberType(m) == typeof(Unnamed) ? null : m, ParserCache.GetParser(m)))
                .ToArray();
            var sep = type.GetCustomAttribute<SeparatedByAttribute>();
            if (sep != null) {
                separator = ParserCache.GetParser(sep.Separator);
            }
        }

        private readonly Type type;
        private readonly (MemberInfo? member, IParser parser)[] members;
        private readonly IParser? separator;

        public bool Matches(ref Buffer buffer, ref object value)
        {
            if (value == null) {
                value = Activator.CreateInstance(type);
            }
            bool first = true;
            foreach (var (member, parser) in members) {
                object tmp = null;
                if (first) {
                    first = false;
                } else if (separator?.Matches(ref buffer, ref tmp) == false) {
                    return false;
                }
                if (member == null) {
                    if (!parser.Matches(ref buffer, ref tmp)) {
                        return false;
                    }
                    continue;
                }
                var oldvalue = GetMember(value, member);
                var newvalue = oldvalue;
                if (parser.Matches(ref buffer, ref newvalue)) {
                    if (member != null && newvalue != oldvalue) {
                        SetMember(value, member, newvalue);
                    }
                } else {
                    return false;
                }
            }
            return true;
        }

        private static void SetMember(object obj, MemberInfo member, object value) {
            switch (member) {
                case FieldInfo field: field.SetValue(obj, value); break;
                case PropertyInfo property: property.SetValue(obj, value); break;
                default: throw new ArgumentException($"{member} cannot be set");
            }
        }

        private static object GetMember(object obj, MemberInfo member) {
            return member switch
            {
                FieldInfo field => field.GetValue(obj),
                PropertyInfo property => property.GetValue(obj),
                _ => throw new ArgumentException($"{member} cannot be got"),
            };
        }

        private static Type MemberType(MemberInfo member) {
            return member switch
            {
                FieldInfo field => field.FieldType,
                PropertyInfo property => property.PropertyType,
                _ => throw new ArgumentException(),
            };
        }
    }
}