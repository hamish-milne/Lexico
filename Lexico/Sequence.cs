using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using static System.Reflection.BindingFlags;
using static System.Linq.Expressions.Expression;
using System.Linq.Expressions;

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
            => new SequenceParser(member.GetMemberType(), null, config, ParserFlags);

        public override bool AddDefault(MemberInfo member) => member is Type;
    }

    internal class SequenceParser : ParserBase
    {
        public SequenceParser(Type type, IParser? separator, IConfig config, ParserFlags flags) : base(config, flags)
        {
            this.Type = type ?? throw new ArgumentNullException(nameof(type));
            this._separator = separator;
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
            this._members = members
                .Select(m => (
                    m.GetMemberType() == typeof(Unnamed) ? null : m,
                    ParserCache.GetParser(m)
                ))
                .ToArray();
        }

        public Type Type { get; }
        private readonly (MemberInfo? member, IParser parser)[] _members;
        private readonly IParser? _separator;

        public override Type OutputType => Type;

        public override void Compile(ICompileContext context)
        {
            // Get the current value. If it's not the right type, make a new one.
            // If we're not saving the value, no need to do this
            Expression? obj = null;
            if (context.Result != null)
            {
                if (context.Result.CanWrite()) {
                    obj = context.Cache(
                        Condition(TypeIs(context.Result, Type), Convert(context.Result, Type), New(Type))
                    );
                } else {
                    obj = context.Result;
                }
            }
            bool first = true;
            foreach (var (member, parser) in _members)
            {
                // If not the first item, add a Separator
                if (!first && _separator != null) {
                    context.Child(_separator, "(Separator)", null, null, context.Failure);
                }
                first = false;
                // Match the item and, if we're saving the value, write it back to the member in question
                context.Child(parser, member?.Name,
                    member == null || obj == null ? null : MakeMemberAccess(obj, member),
                    null, context.Failure);
            }
            context.Succeed(obj!);
        }

        private static bool IsPrivate(MemberInfo member) => member switch
        {
            FieldInfo fi => fi.IsPrivate,
            PropertyInfo pi => (pi.SetMethod?.IsPrivate ?? true) && (pi.GetMethod?.IsPrivate ?? true), // if either accessor is not private, the property is not private
            _ => throw new ArgumentException($"{member} cannot determine member access level")
        };

        public override string ToString() => Type.Name;
    }

    public static class ExpressionExtensions
    {
        public static bool CanWrite(this Expression expression)
        {
            return expression switch {
                ParameterExpression _ => true,
                MemberExpression m => m.Member switch {
                    FieldInfo f => !f.IsInitOnly,
                    PropertyInfo p => p.CanWrite,
                    _ => false
                },
                _ => false
            };
        }
    }
}