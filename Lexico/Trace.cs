using System;
using System.Text;
using System.Collections.Generic;

namespace Lexico
{
    internal interface ITrace
    {
        void Indent(string? name, IParser parser);
        void Result(bool success, Buffer buffer, int startedAt);
        Stack<IParser> ILR { get; }
    }

    internal class Trace : ITrace
    {
        private readonly List<(string? name, IParser? parser, bool? result, Buffer? buf, int startedAt)> log
            = new List<(string? name, IParser? parser, bool? result, Buffer? buf, int startedAt)>();

        public void Indent(string? name, IParser parser)
        {
            log.Add((name, parser, null, null, 0));
        }

        public Stack<IParser> ILR { get; } = new Stack<IParser>();

        public void Result(bool success, Buffer buffer, int startedAt)
        {
            if (log.Count > 0 && log[log.Count - 1].result == null) {
                var l = log[log.Count - 1];
                l.result = success;
                l.buf = buffer;
                l.startedAt = startedAt;
                log[log.Count - 1] = l;
            } else {
                log.Add((null, null, success, buffer, startedAt));
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            int indent = 0;
            foreach (var (name, parser, result, buf, startedAt) in log)
            {
                if (parser == null) {
                    indent--;
                    sb.Append(' ', indent*4).Append("} ").Append(result);
                } else {
                    sb.Append(' ', indent*4);
                    if (name != null) {
                        sb.Append(name).Append(" = ");
                    }
                    sb.Append(parser);
                    if (result.HasValue) {
                        sb.Append(": ").Append(result.Value);
                    } else {
                        sb.Append(" {");
                        indent++;
                    }
                }
                if (buf.HasValue) {
                    sb.Append(" | `");
                    if (result == true) {
                        sb.Append(buf.Value.String, startedAt, (buf.Value.Position - startedAt));
                    } else {
                        sb.Append(buf.Value.Peek(0)?.ToString() ?? "<EOF>");
                    }
                    sb.Append("`");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

    internal interface IUnaryParser
    {
        IParser Inner { get; }
    }

    internal static class ParserExtensions
    {
        public static IParser GetInner(this IParser parser)
        {
            while (parser is IUnaryParser up && up.Inner != null) {
                parser = up.Inner;
            }
            return parser;
        }
    }

    internal sealed class TraceWrapper : IParser, IUnaryParser
    {
        public TraceWrapper(IParser inner, string? name = null) {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Inner = Inner.GetInner();
            this.name = name;
        }
        public IParser Inner { get; }
        private readonly string? name;
        public bool Matches(ref Buffer buffer, ref object value, ITrace trace)
        {
            trace.Indent(name, Inner);
            int startedAt = buffer.Position;
            var result = Inner.Matches(ref buffer, ref value, trace);
            trace.Result(result, buffer, startedAt);
            return result;
        }

        public override string ToString() => Inner.ToString();
    }
}