using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Lexico
{
    public interface ITrace
    {
        void Push(IParser parser, string? name);
        void Pop(IParser parser, bool success, object? value, ReadOnlySpan<char> text);
    }

    public abstract class TextTrace : ITrace
    {
        int indent = 0;

        (IParser parser, string? name)? lastPush;
        readonly Stack<string> parserKindStack = new Stack<string>();

        protected abstract void WriteLine(string str);

        public bool Verbose { get; set; }
        public int IndentAmount { get; set; } = 2;

        public void Pop(IParser parser, bool success, object? value, ReadOnlySpan<char> text)
        {
            var sb = new StringBuilder();

            switch (Verbose)
            {
                case true:
                    if (lastPush.HasValue) {
                        sb.Append(' ', IndentAmount * indent);
                        if (lastPush.Value.name != null) {
                            sb.Append(lastPush.Value.name).Append(" : ");
                        }
                        sb.Append(lastPush.Value.parser?.ToString() ?? "<UNKNOWN>").Append(' ');
                        lastPush = null;
                    } else {
                        indent--;
                        sb.Append(' ', IndentAmount * indent).Append("} ");
                    }

                    if (success) {
                        sb.Append("\u2714").Append(" = ").Append(value ?? "<null>");
                    } else {
                        sb.Append("\u2717 (got `").Append(text.ToArray()).Append("`)");
                    }
                    break;
                case false:
                    sb.Append(lastPush.HasValue ? "|" : "<");
                    sb.Append(success ? "\u2714 " : "\u2717 ");

                    if (lastPush.HasValue) {
                        sb.Append(' ', IndentAmount * indent);
                        if (lastPush.Value.parser != null) {
                            sb.Append(lastPush.Value.parser);
                        }
                        lastPush = null;
                    }
                    else {
                        indent--;
                        sb.Append(' ', IndentAmount * indent).Append(parserKindStack.Pop());
                    }

                    sb.Append(success ? " \u2714 " : " \u2717 ");

                    if (success)
                    {
                        if (string.IsNullOrEmpty(value?.ToString())) {
                            sb.Append("`")
                              .Append(Regex.Replace(new string(text.ToArray()), @"\r\n?|\n", @"\n"))
                              .Append("`");
                        }
                        else {
                            sb.Append("<nothing>");
                        }
                    }
                    else {
                        if (text.Length > 0) {
                            sb.Append("`")
                              .Append(Regex.Replace(new string(text.ToArray()), @"\r\n?|\n", @"\n"))
                              .Append("`");
                        }
                        else {
                            sb.Append("<nothing>");
                        }
                    }

                    break;
            }

            WriteLine(sb.ToString());
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

            switch (Verbose)
            {
                case true:
                    sb.Append(' ', IndentAmount * indent);
                    if (name != null) {
                        sb.Append(name).Append(" : ");
                    }
                    sb.Append(parser?.ToString() ?? "<UNKNOWN>").Append(" {");
                    indent++;
                    break;
                case false:
                    var parserString = parser?.ToString() ?? "<UNKNOWN>";
                    parserKindStack.Push(parserString);
                    sb.Append(">  ").Append(' ', IndentAmount * indent).Append(parserString);
                    indent++;
                    break;
            }

            WriteLine(sb.ToString());
        }
    }

    public sealed class ConsoleTrace : TextTrace
    {
        protected override void WriteLine(string str) => Console.WriteLine(str);
    }

    public sealed class DelegateTextTrace : TextTrace
    {
        public DelegateTextTrace(Action<string> writeLine) => _writeLineDelegate = writeLine;

        private readonly Action<string> _writeLineDelegate;
        protected override void WriteLine(string str) => _writeLineDelegate?.Invoke(str);
    }
}
