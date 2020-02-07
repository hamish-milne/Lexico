using System;
using System.Text;

namespace Lexico
{
    public interface ITrace
    {
        void Push(IParser parser, string? name);
        void Pop(IParser parser, bool success, object? value, ReadOnlySpan<char> text);
    }

    public sealed class ConsoleTrace : ITrace
    {
        int indent = 0;

        (IParser parser, string? name)? lastPush;

        public void Pop(IParser parser, bool success, object? value, ReadOnlySpan<char> text)
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
            } else {
                sb.Append("\u2717 (got `").Append(text.ToArray()).Append("`)");
            }
            Console.WriteLine(sb);
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
            Console.WriteLine(sb);
        }
    }
}
