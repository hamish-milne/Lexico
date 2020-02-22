using System.Reflection;
using System;
using static System.AttributeTargets;

namespace Lexico
{
    /// <summary>
    /// Indicates that this member does not need to be matched. If the parsing fails, the member's value is unchanged
    /// and the cursor is reset to its last position before continuing onward.
    /// Applied by default to Nullable`1 types.
    /// </summary>
    [AttributeUsage(Property | Field, AllowMultiple = false)]
    public class OptionalAttribute : TermAttribute
    {
        public override int Priority => 100;
        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config)
        {
            IParser c;
            if (member is Type t && t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                c = ParserCache.GetParser(t.GetGenericArguments()[0]);
            } else {
                c = child();
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
        public bool Matches(ref IContext context, ref object? value)
        {
            child.MatchChild(null, ref context, ref value);
            return true;
        }

        public override string ToString() => $"{child}?";
    }
}