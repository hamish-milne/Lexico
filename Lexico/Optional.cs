using System.Reflection;
using System;
using static System.Linq.Expressions.Expression;

namespace Lexico
{
    /// <summary>
    /// Indicates that this member does not need to be matched. If the parsing fails, the member's value is unchanged
    /// and the cursor is reset to its last position before continuing onward.
    /// Applied by default to Nullable`1 types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class OptionalAttribute : TermAttribute
    {
        public override int Priority => 100;
        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
        {
            IParser c;
            if (member is Type t && t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                c = child(t.GetGenericArguments()[0]);
            } else {
                c = child(null);
            }
            if (c is OptionalParser o) {
                return o;
            }
            return new OptionalParser(c);
        }

        public override bool AddDefault(MemberInfo member)
        {
            // TODO: Also check for NullableAttribute(2) on members
            if (member is Type t && t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                return true;
            }
            return false;
        }
    }

    internal class OptionalParser : IParser
    {
        public OptionalParser(IParser child) {
            Child = child;
        }
        public readonly IParser Child;

        public Type OutputType => Child.OutputType; // TODO: Nullable<T> here?

        public void Compile(ICompileContext context)
        {
            var savePoint = context.Save();
            var skip = context.Success == null ? Label() : null;
            context.Child(Child, null, context.Result, context.Success ?? skip, savePoint);
            context.Restore(savePoint);
            if (skip != null) {
                context.Append(Label(skip));
            }
            context.Succeed();
        }

        public override string ToString() => $"{Child}?";
    }
}