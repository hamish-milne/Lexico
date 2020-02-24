using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using static System.Reflection.BindingFlags;
using static System.Linq.Expressions.Expression;

namespace Lexico
{
    /// <summary>
    /// Matches each Term member in a class/struct, in the order they were declared.
    /// Applied by default to non-abstract classes/structs.
    /// </summary>
    public class SequenceAttribute : TermAttribute
    {
        public override int Priority => 0;
        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config)
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
                .Select(m => (
                    m.GetMemberType() == typeof(Unnamed) ? null : m,
                    ParserCache.GetParser(m)
                ))
                .ToArray();
        }

        public Type Type { get; }
        private readonly (MemberInfo? member, IParser parser)[] members;
        private readonly IParser? separator;

        public Type OutputType => Type;

        public void Compile(ICompileContext context)
        {
            // Get the current value. If it's not the right type, make a new one.
            // If we're not saving the value, no need to do this
            var obj = context.Result == null ? null :
                context.Cache(Condition(TypeIs(context.Result, Type), context.Result, New(Type)));
            bool first = true;
            foreach (var (member, parser) in members)
            {
                // If not the first item, add a Separator
                if (!first && separator != null) {
                    context.Child(separator, null, null, context.Failure);
                }
                first = false;
                // Match the item and, if we're saving the value, write it back to the member in question
                context.Child(parser,
                    member == null || obj == null ? null : MakeMemberAccess(obj, member),
                    null, context.Failure);
            }
        }

        private static bool IsPrivate(MemberInfo member) => member switch
        {
            FieldInfo fi => fi.IsPrivate,
            PropertyInfo pi => (pi.SetMethod?.IsPrivate ?? true) && (pi.GetMethod?.IsPrivate ?? true), // if either accessor is not private, the property is not private
            _ => throw new ArgumentException($"{member} cannot determine member access level")
        };

        public override string ToString() => Type.Name;
    }
}