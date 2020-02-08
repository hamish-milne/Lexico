using System.Reflection;
using System;
using static System.Reflection.BindingFlags;
using static System.AttributeTargets;

namespace Lexico
{
    [AttributeUsage(Property | Field | Class | Struct, AllowMultiple = false)]
    public abstract class TerminalAttribute : TermAttribute
    {
        public override int Priority => 30;
        public abstract IParser Create(MemberInfo member);

        public override IParser Create(MemberInfo member, Func<IParser> child)
        {
            return Create(member);
        }
    }

    public class LiteralAttribute : TerminalAttribute
    {
        public LiteralAttribute(string value) {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
        public string Value { get; }

        public override IParser Create(MemberInfo member) {
            return new LiteralParser(Value);
        }
    }

    public class IndirectLiteralAttribute : TerminalAttribute
    {
        public IndirectLiteralAttribute(string property) {
            Property = property ?? throw new ArgumentNullException(nameof(property));
        }
        public string Property { get; }

        public override IParser Create(MemberInfo member) {
            var prop = member.ReflectedType.GetProperty(Property, Instance | Public | NonPublic)
                ?? throw new ArgumentException($"Could not find `{Property}` on {member.ReflectedType}");
            return new LiteralParser((string)prop.GetValue(Activator.CreateInstance(member.ReflectedType, true)));
        }
    }

    internal class LiteralParser : IParser
    {
        public LiteralParser(string literal) {
            this.literal = literal;
        }
        private readonly string literal;
        public bool Matches(ref IContext context, ref object? value)
        {
            for (int i = 0; i < literal.Length; i++) {
                if (context.Peek(i) != literal[i]) {
                    return false;
                }
            }
            context = context.Advance(literal.Length);
            value = literal;
            return true;
        }

        public override string ToString() => $"`{literal}`";
    }
}