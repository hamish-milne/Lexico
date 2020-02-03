using System.Reflection;
using System;
using static System.Reflection.BindingFlags;

namespace Lexico
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public abstract class TerminalAttribute : Attribute
    {
    }

    public class LiteralAttribute : TerminalAttribute
    {
        public LiteralAttribute(string value) {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
        public string Value { get; }

        internal IParser Create() {
            return new LiteralParser(Value);
        }
    }

    public class IndirectLiteralAttribute : TerminalAttribute
    {
        public IndirectLiteralAttribute(string property) {
            Property = property ?? throw new ArgumentNullException(nameof(property));
        }
        public string Property { get; }

        internal IParser Create(MemberInfo member) {
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
        public bool Matches(ref Buffer buffer, ref object value, ITrace trace)
        {
            for (int i = 0; i < literal.Length; i++, buffer.Position++) {
                if (buffer.Peek(0) != literal[i]) {
                    return false;
                }
            }
            value = literal;
            return true;
        }

        public override string ToString() => $"`{literal}`";
    }
}