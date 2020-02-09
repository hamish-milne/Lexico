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

    public class SequenceAttribute : TermAttribute
    {
        public override int Priority => 0;
        public override IParser Create(MemberInfo member, Func<IParser> child)
            => new SequenceParser(member.GetMemberType(), null);

        public override bool AddDefault(MemberInfo member) => member is Type;
    }

    internal class SequenceParser : IParser
    {
        public SequenceParser(Type type, IParser? separator)
        {
            this.Type = type ?? throw new ArgumentNullException(nameof(type));
            this.separator = separator;
            var typeHierachy = new List<Type>();
            var current = type;
            while (current != null && current != typeof(object))
            {
                typeHierachy.Add(current);
                current = current.BaseType;
            }

            typeHierachy.Reverse();

            var rawMembers = typeHierachy.SelectMany(t =>
                t.GetMembers(Instance | Public | NonPublic)
                    .Where(m => m is FieldInfo || m is PropertyInfo)
                    .Where(m => m.GetCustomAttributes<TermAttribute>(true).Any())
            );
            var members = new List<MemberInfo>();
            // Combine virtual/override members into one list
            foreach (var m in rawMembers)
            {
                if (!IsPrivate(m)) {
                    int i;
                    for (i = 0; i < members.Count; i++) {
                        if (!IsPrivate(members[i]) && members[i].Name == m.Name) {
                            members[i] = m;
                            break;
                        }
                    }
                    if (i < members.Count) {
                        continue;
                    }
                }
                members.Add(m);
            }
            if (members.Count == 0) {
                throw new ArgumentException($"Sequence {type} has no Terms");
            }
            this.members = members
                .Select(m => MemberType(m) == typeof(Unnamed)
                    ? (null, ParserCache.GetParser(m))
                    : (m, ParserCache.GetParser(m))
                ).ToArray();
        }

        public Type Type { get; }
        private readonly (MemberInfo? member, IParser parser)[] members;
        private readonly IParser? separator;

        public bool Matches(ref IContext context, ref object? value)
        {
            if (!Type.IsInstanceOfType(value)) {
                value = Activator.CreateInstance(Type);
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

        public override string ToString() => Type.Name;
    }
}