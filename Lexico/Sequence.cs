using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using static System.Reflection.BindingFlags;

namespace Lexico
{
    /// <summary>
    /// Matches each Term member in a class/struct, in the order they were declared.
    /// Applied by default to non-abstract classes/structs.
    /// </summary>
    public class SequenceAttribute : TermAttribute
    {
        public override int Priority => 0;
        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
            => new SequenceParser(member.GetMemberType(), null, CheckZeroLength);

        public override bool AddDefault(MemberInfo member) => member is Type;

        public bool CheckZeroLength { get; set; }
    }

    internal class SequenceParser : IParser
    {
        public SequenceParser(Type type, IParser? separator, bool checkZeroLength)
        {
            this.CheckZeroLength = checkZeroLength;
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
        public bool CheckZeroLength { get; }
        private readonly (MemberInfo? member, IParser parser)[] members;
        private readonly IParser? separator;

        public Type OutputType => Type;

        public void Compile(Context context)
        {
            var e = context.Emitter;
            // Get the current value. If it's not the right type, make a new one.
            // If we're not saving the value, no need to do this
            if (context.Result != null && !e.TypeOf(context.Result).IsValueType && context.CanWriteResult)
            {
                var skip = e.Label();
                e.Compare(context.Result, CompareOp.NotEqual, e.Default(typeof(object)), skip);
                e.Copy(context.Result, e.Create(OutputType));
                e.Mark(skip);
            }
            bool first = true;
            foreach (var (member, parser) in members)
            {
                // If not the first item, add a Separator
                if (!first && separator != null) {
                    context.Child(separator, "(Separator)", null, null, context.Failure);
                }
                first = false;
                // Match the item and, if we're saving the value, write it back to the member in question
                if (context.Result != null && member != null) {
                    var mValue = e.Load(context.Result, member);
                    context.Child(parser, member.Name, mValue, null, context.Failure, IsWritable(member));
                    if (IsWritable(member)) {
                        e.Store(context.Result, member, mValue);
                    }
                } else {
                    context.Child(parser, null, null, null, context.Failure);
                }
            }
            if (CheckZeroLength) {
                e.Compare(context.GetFeature<StartPosition>().Get(), CompareOp.Equal, context.Position, context.Failure);
            }
            context.Succeed();
        }

        private static bool IsPrivate(MemberInfo member) => member switch
        {
            FieldInfo fi => fi.IsPrivate,
            PropertyInfo pi => (pi.SetMethod?.IsPrivate ?? true) && (pi.GetMethod?.IsPrivate ?? true), // if either accessor is not private, the property is not private
            _ => throw new ArgumentException($"{member} cannot determine member access level")
        };

        private static bool IsWritable(MemberInfo member) => member switch {
            FieldInfo f => !f.IsInitOnly,
            PropertyInfo p => p.CanWrite,
            _ => false
        };

        public override string ToString() => Type.Name;
    }
}