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
        int currentIndent = 0;
        private int IndentCount => Math.Max(0, SpacesPerIndent * currentIndent - 2); // -2 for leading outliner characters

        (IParser parser, string? name)? lastPush;
        readonly Stack<string> parserKindStack = new Stack<string>();

        protected abstract void WriteLine(bool isPop, bool success, string str);

        public bool Verbose { get; set; }
        public int SpacesPerIndent { get; set; } = 2;

        public void Pop(IParser parser, bool success, object? value, ReadOnlySpan<char> text)
        {
            var sb = new StringBuilder()
                     .Append(lastPush.HasValue ? "|" : "<")
                     .Append(success ? "\u2714 " : "\u2717 ");

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
                        sb.Append("\u2717 (got `").Append(text.ToArray()).Append("`)");
                    }
                    break;
                case false:
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

                    
                    var result = new string(text.ToArray());
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
            var sb = new StringBuilder().Append(">  ").Append(' ', IndentCount);

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
