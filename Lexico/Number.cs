using System.Globalization;
using System.Text.RegularExpressions;

namespace Lexico
{
    internal abstract class NumberParser : IParser
    {
        protected NumberParser(string pattern) {
            regex = new Regex("^"+pattern, RegexOptions.Compiled);
        }
        protected abstract object Parse(string str);
        private readonly Regex regex;

        public bool Matches(ref Buffer buffer, ref object value, ITrace trace)
        {
            var match = regex.Match(buffer.String, buffer.Position, buffer.String.Length - buffer.Position);
            if (match.Success) {
                value = Parse(match.Value);
                buffer.Position += match.Value.Length;
                return true;
            }
            return false;
        }
    }
    internal class FloatParser : NumberParser
    {
        public static FloatParser Instance { get; } = new FloatParser();
        // TODO: NumberStyles, hex, negative, etc.
        private FloatParser() : base(@"(?>[\-\+]?[0-9]*\.)?[0-9]+(?>[eE][\-\+]?[0-9]+)?") {}

        protected override object Parse(string str) => float.Parse(str, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent);
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