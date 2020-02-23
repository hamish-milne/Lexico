using System.Reflection;
using System;
using static System.Reflection.BindingFlags;
using static System.AttributeTargets;
using static System.Linq.Expressions.Expression;

namespace Lexico
{
    /// <summary>
    /// Base class for Terminals - the left nodes of the parse tree
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | Class | Struct, AllowMultiple = false)]
    public abstract class TerminalAttribute : TermAttribute
    {
        public override int Priority => 30;
        public abstract IParser Create(MemberInfo member, IConfig config);

        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config)
        {
            return Create(member, config);
        }
    }

    /// <summary>
    /// Matches a string literal exactly. Outputs the matched text
    /// </summary>
    public class LiteralAttribute : TerminalAttribute
    {
        public LiteralAttribute(string value) {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
        public string Value { get; }

        public override IParser Create(MemberInfo member, IConfig config) {
            return new LiteralParser(Value);
        }
    }

    /// <summary>
    /// Matches a string literal read from the value of a named String Property. This allows the literal's value
    /// to be different for derived classes. Outputs the matched text
    /// </summary>
    public class IndirectLiteralAttribute : TerminalAttribute
    {
        public IndirectLiteralAttribute(string property) {
            Property = property ?? throw new ArgumentNullException(nameof(property));
        }
        public string Property { get; }

        public override IParser Create(MemberInfo member, IConfig config) {
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

        public Type OutputType => typeof(string);

        public void Compile(ICompileContext context)
        {
            for (int i = 0; i < literal.Length; i++) {
                var c = Constant(literal[i]);
                context.Append(IfThen(NotEqual(c, context.Peek(i)), Goto(context.Failure)));
            }
            context.Advance(literal.Length);
            context.Succeed(Constant(literal));
        }

        public override string ToString() => $"`{literal}`";
    }
}