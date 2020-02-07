using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using static System.Reflection.BindingFlags;

namespace Lexico
{
    public class WhitespaceSeparatedAttribute : SeparatedByAttribute
    {
        public WhitespaceSeparatedAttribute() : base(typeof(Whitespace?)) {}
    }

    internal class SequenceParser : IParser
    {
        public SequenceParser(Type type)
        {
            this.type = type ?? throw new ArgumentNullException(nameof(type));
            var typeHierachy = new List<Type>();
            var current = type;
            while (current != null && current != typeof(object))
            {
                typeHierachy.Add(current);
                current = current.BaseType;
            }

            typeHierachy.Reverse();

            members = typeHierachy.SelectMany(t =>
                t.GetMembers(Instance | Public | NonPublic)
                    .Where(m => m is FieldInfo || m is PropertyInfo)
                    // Only include leaf type or non-inherited members
                    .Where(m => m.ReflectedType == type || IsPrivate(m))
                    .Where(m => m.GetCustomAttributes<TermAttribute>(true).Any())
                    .Select(m => MemberType(m) == typeof(Unnamed)
                        ? (null, ParserCache.GetParser(m))
                        : (m, ParserCache.GetParser(m))
                    )
            ).ToArray();
            if (members.Length == 0) {
                throw new ArgumentException($"Sequence {type} has no Terms");
            }
            var sep = type.GetCustomAttribute<SeparatedByAttribute>(true);
            if (sep != null) {
                separator = ParserCache.GetParser(sep.Separator);
            }
        }

        private readonly Type type;
        private readonly (MemberInfo? member, IParser parser)[] members;
        private readonly IParser? separator;

        public bool Matches(ref IContext context, ref object? value)
        {
            if (!type.IsInstanceOfType(value)) {
                value = Activator.CreateInstance(type);
            }
            bool first = true;
            foreach (var (member, parser) in members) {
                object? tmp = null;
                if (!first && !separator.MatchChild("(Separator)", ref context, ref tmp)) {
                    return false;
                }
                first = false;
                if (member == null) {
                    tmp = null;
                    if (!parser.MatchChild(null, ref context, ref tmp)) {
                        return false;
                    }
                } else {
                    var oldvalue = GetMember(value!, member);
                    var newvalue = oldvalue;
                    if (parser.MatchChild(member.Name, ref context, ref newvalue)) {
                        if (member != null && newvalue != oldvalue) {
                            SetMember(value!, member, newvalue);
                        }
                    } else {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IsPrivate(MemberInfo member) => member switch
        {
            FieldInfo fi => fi.IsPrivate,
            PropertyInfo pi => (pi.SetMethod?.IsPrivate ?? true) && (pi.GetMethod?.IsPrivate ?? true), // if either accessor is not private, the property is not private
            _ => throw new ArgumentException($"{member} cannot determine member access level")
        };

        private static void SetMember(object obj, MemberInfo member, object? value) {
            switch (member) {
                case FieldInfo field: field.SetValue(obj, value); break;
                case PropertyInfo property: property.SetValue(obj, value); break;
                default: throw new ArgumentException($"{member} cannot be set");
            }
        }

        private static object? GetMember(object obj, MemberInfo member) {
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

        public override string ToString() => type.Name;
    }
}