using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

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

    public sealed class NoTrace : ITrace
    {
        public void Push(IParser parser, string? name) { }

        public void Pop(IParser parser, bool success, object? value, StringSegment text) { }
    }

    public abstract class TextTrace : ITrace
    {
        int currentIndent = 0;
        private int IndentCount => Math.Max(0, SpacesPerIndent * currentIndent - (Verbose ? 0 : 2)); // -2 for leading outliner characters

        (IParser parser, string? name)? lastPush;
        readonly Stack<string> parserKindStack = new Stack<string>();

        protected abstract void WriteLine(bool isPop, bool success, string str);

        public bool Verbose { get; set; }
        public int SpacesPerIndent { get; set; } = 2;

        public void Pop(IParser parser, bool success, object? value, StringSegment text)
        {
            var sb = new StringBuilder();

            switch (Verbose)
            {
                case true:
                    if (lastPush.HasValue) {
                        sb.Append(' ', IndentCount);
                        if (lastPush.Value.name != null) {
                            sb.Append(lastPush.Value.name).Append(" : ");
                        }
                        sb.Append(lastPush.Value.parser?.ToString() ?? "<UNKNOWN>").Append(' ');
                        lastPush = null;
                    } else {
                        currentIndent--;
                        sb.Append(' ', IndentCount).Append("} ");
                    }

                    if (success) {
                        sb.Append("\u2714").Append(" = ").Append(value ?? "<null>");
                    } else {
                        if (text.String != null && text.Length == 0 && text.Start < text.String.Length) {
                            text = new StringSegment(text.String, text.Start, text.Length + 1);
                        }
                        sb.Append("\u2717 (got `").Append(text).Append("`)");
                    }
                    break;
                case false:
                    sb.Append(lastPush.HasValue ? "|" : "<")
                      .Append(success ? "\u2714 " : "\u2717 ");
                    if (lastPush.HasValue) {
                        sb.Append(' ', IndentCount);
                        if (lastPush.Value.parser != null) {
                            sb.Append(lastPush.Value.parser);
                        }
                        lastPush = null;
                    }
                    else {
                        currentIndent--;
                        sb.Append(' ', IndentCount).Append(parserKindStack.Pop());
                    }

                    sb.Append(success ? " \u2714 " : " \u2717 ");

                    
                    var result = text.ToString();
                    void AppendResult(bool anyResult)
                    {
                        if (anyResult) {
                            sb.Append("`")
                              .Append(Regex.Replace(result, @"\r\n?|\n", @"\n"))
                              .Append("`");
                        }
                        else {
                            sb.Append("<nothing>");
                        }
                    }

                    if (success) AppendResult(value != null);
                    else AppendResult(text.Length > 0);

                    break;
            }

            WriteLine(true, success, sb.ToString());
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
            var sb = new StringBuilder();
            if (!Verbose) {
                sb.Append(">  ");
            }
            sb.Append(' ', IndentCount);

            switch (Verbose)
            {
                case true:
                    if (name != null) {
                        sb.Append(name).Append(" : ");
                    }
                    sb.Append(parser?.ToString() ?? "<UNKNOWN>").Append(" {");
                    break;
                case false:
                    var parserString = parser?.ToString() ?? "<UNKNOWN>";
                    parserKindStack.Push(parserString);
                    sb.Append(parserString);
                    break;
            }

            currentIndent++;
            WriteLine(false, false, sb.ToString());
        }
    }

    /// <summary>
    /// A Trace that writes directly and completely to the console (with colours)
    /// </summary>
    public sealed class ConsoleTrace : TextTrace
    {
        protected override void WriteLine(bool isPop, bool success, string str)
        {
            Console.ForegroundColor = isPop ? success ? ConsoleColor.Green : ConsoleColor.Red : ConsoleColor.Yellow;
            Console.WriteLine(str);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }

    public sealed class DelegateTextTrace : TextTrace
    {
        public DelegateTextTrace(Action<string> writeLine) => _writeLineDelegate = writeLine;

        private readonly Action<string> _writeLineDelegate;
        protected override void WriteLine(bool isPop, bool success, string str) => _writeLineDelegate?.Invoke(str);
    }
}
