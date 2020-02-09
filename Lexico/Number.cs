using System;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Lexico
{
    public class NumberAttribute : TerminalAttribute
    {
        public override IParser Create(MemberInfo member, IConfig config)
        {
            return member.GetMemberType() switch
            {
                var t when t == typeof(float) => FloatParser.Instance,
                var t when t == typeof(int) => IntParser.Instance,
                _ => throw new ArgumentException($"Unknown number type for {member}"),
            };
        }

        public override bool AddDefault(MemberInfo member)
            => member is Type t && t.IsPrimitive;
    }

    internal abstract class NumberParser : IParser
    {
        protected NumberParser(string pattern) {
            regex = new Regex("^"+pattern, RegexOptions.Compiled);
        }
        protected abstract object Parse(string str);
        private readonly Regex regex;

        public bool Matches(ref IContext context, ref object? value)
        {
            var str = context.Text;
            var match = regex.Match(str, context.Position, str.Length - context.Position);
            if (match.Success) {
                value = Parse(match.Value);
                context = context.Advance(match.Value.Length);
                return true;
            }
            return false;
        }
    }
    internal class FloatParser : NumberParser
    {
        public static FloatParser Instance { get; } = new FloatParser();
        // TODO: NumberStyles, hex, negative, etc.
        private FloatParser() : base(@"[\-\+]?[0-9]*\.?[0-9]+(?>[eE][\-\+]?[0-9]+)?") {}

        protected override object Parse(string str) => float.Parse(str, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign);
        public override string ToString() => "float";
    }

    internal class IntParser : NumberParser
    {
        public static IntParser Instance { get; } = new IntParser();
        private IntParser() : base(@"[0-9]+") {}
        protected override object Parse(string str) => int.Parse(str, NumberStyles.None);
        public override string ToString() => "integer";
    }

    // TODO: other primitives? Make this automatically?
}