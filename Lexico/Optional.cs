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
            return new OptionalParser(c, config, ParserFlags);
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

    internal class OptionalParser : ParserBase
    {
        public OptionalParser(IParser child, IConfig config,  ParserFlags flags) : base(config, flags) {
            Child = child;
        }
        public readonly IParser Child;

        public override Type OutputType => Child.OutputType; // TODO: Nullable<T> here?

        public override void Compile(Context context)
        {
            var e = context.Emitter;
            var savePoint = context.Save();
            var skip = context.Success == null ? e.Label() : null;
            context.Child(Child, null, context.Result, context.Success ?? skip, savePoint.label);
            context.Restore(savePoint);
            if (context.CanWriteResult && context.Result != null) {
                e.Copy(context.Result, e.Default(e.TypeOf(context.Result)));
            }
            if (skip != null) {
                e.Mark(skip);
            }
            context.Succeed();
        }

        public override string ToString() => $"{Child}?";
    }
}