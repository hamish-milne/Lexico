using System;
using System.Text;

namespace Lexico
{
    public struct StringSegment
    {
        public StringSegment(string str, int start, int length)
        {
            String = str;
            Start = start;
            Length = length;
        }

        public string String { get; }
        public int Start { get; }
        public int Length { get; }

        public override string ToString() => String?.Substring(Start, Length) ?? "";
    }

    /// <summary>
    /// Allows logging the results of each parser step for debugging, error reporting etc.
    /// </summary>
    public interface ITrace
    {
        /// <summary>
        /// Indicates that a child parser is being tested
        /// </summary>
        /// <param name="parser">The parser</param>
        /// <param name="name">The parser's name (relative to its parent)</param>
        void Push(IParser parser, string? name);

        /// <summary>
        /// Records the result of the matched Push record
        /// </summary>
        /// <param name="parser">The parser</param>
        /// <param name="success">True if matching succeeded</param>
        /// <param name="value">The resultant value</param>
        /// <param name="text">The total matched text</param>
        void Pop(IParser parser, bool success, object? value, StringSegment text);
    }

    /// <summary>
    /// A Trace that writes directly and completely to the console (with colours)
    /// </summary>
    public sealed class ConsoleTrace : ITrace
    {
        int indent = 0;

        (IParser parser, string? name)? lastPush;

        public void Pop(IParser parser, bool success, object? value, StringSegment text)
        {
            var sb = new StringBuilder();
            if (lastPush.HasValue) {
                sb.Append(' ', 4 * indent);
                if (lastPush.Value.name != null) {
                    sb.Append(lastPush.Value.name).Append(" : ");
                }
                sb.Append(lastPush.Value.parser?.ToString() ?? "<UNKNOWN>").Append(' ');
                lastPush = null;
            } else {
                indent--;
                sb.Append(' ', 4 * indent).Append("} ");
            }

            if (success) {
                sb.Append("\u2714").Append(" = ").Append(value ?? "<null>");
                Console.ForegroundColor = ConsoleColor.Green;
            } else {
                sb.Append("\u2717 (got `").Append(text).Append("`)");
                Console.ForegroundColor = ConsoleColor.Red;
            }
            Console.WriteLine(sb);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public void Push(IParser parser, string? name)
        {
            if (lastPush.HasValue) {
                WritePush(lastPush.Value.parser, lastPush.Value.name);
            }
            lastPush = (parser, name);
        }

        private void WritePush(IParser parser, string? name)
        {
            var sb = new StringBuilder().Append(' ', 4 * indent);
            if (name != null) {
                sb.Append(name).Append(" : ");
            }
            sb.Append(parser?.ToString() ?? "<UNKNOWN>").Append(" {");
            indent++;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(sb);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
