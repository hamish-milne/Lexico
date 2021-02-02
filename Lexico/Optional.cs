using System.Reflection;
using System;

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
            this.child = child;
        }
        private readonly IParser child;

        public Type OutputType => child.OutputType; // TODO: Nullable<T> here?

        public void Compile(Context context)
        {
            var e = context.Emitter;
            var savePoint = context.Save();
            var skip = context.Success ?? e.Label();
            context.Child(child, null, context.Result, skip, savePoint.label);
            context.Restore(savePoint);
            context.PopCachedResult();
            if (context.HasResult()) {
                e.Null();
            }
            if (skip != context.Success) {
                e.Mark(skip);
            }
            context.Succeed();
        }

        public override string ToString() => $"{child}?";
    }
}