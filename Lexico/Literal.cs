using System;
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
    }

    internal class LiteralParser : IParser
    {
        public LiteralParser(LiteralAttribute attribute) {
            literal = attribute.Value;
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