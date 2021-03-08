using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Regex = System.Text.RegularExpressions.Regex;

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

        public override string ToString() => String?.Substring(Start, Length) ?? string.Empty;
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

    public abstract class DeveloperTrace : ITrace
    {
        int currentIndent = 0;
        private int IndentCount => Math.Max(0, SpacesPerIndent * currentIndent - (OutputBraces ? 0 : 2)); // -2 for leading outliner characters

        (IParser parser, string? name)? lastPush;
        readonly Stack<string> parserKindStack = new Stack<string>();

        protected abstract void WriteLine(bool isPop, bool success, string str);
        
        public bool OutputBraces { get; set; }
        public int SpacesPerIndent { get; set; } = 2;

        public void Pop(IParser parser, bool success, object? value, StringSegment text)
        {
            var sb = new StringBuilder();

            if (OutputBraces)
            {
                if (lastPush.HasValue)
                {
                    sb.Append(' ', IndentCount);
                    if (lastPush.Value.name != null)
                    {
                        sb.Append(lastPush.Value.name).Append(" : ");
                    }

                    sb.Append((lastPush.Value.parser?.ToString() ?? "<UNKNOWN>").Replace("\n", "\\n").Replace("\r", "\\r")).Append(' ');
                    lastPush = null;
                }
                else
                {
                    currentIndent--;
                    sb.Append(' ', IndentCount).Append("} ");
                }

                if (success)
                {
                    sb.Append("\u2714").Append(" = ").Append(value ?? "<null>");
                }
                else
                {
                    if (text.String != null && text.Length == 0 && text.Start < text.String.Length)
                    {
                        text = new StringSegment(text.String, text.Start, text.Length + 1);
                    }

                    sb.Append("\u2717 (got `").Append(text.ToString().Replace("\n", "\\n").Replace("\r", "\\r")).Append("`)");
                }
            }
            else
            {
                sb.Append(lastPush.HasValue ? "|" : "<")
                  .Append(success ? "\u2714 " : "\u2717 ");
                if (lastPush.HasValue)
                {
                    sb.Append(' ', IndentCount);
                    if (lastPush.Value.parser != null)
                    {
                        sb.Append(lastPush.Value.parser);
                    }

                    lastPush = null;
                }
                else
                {
                    currentIndent--;
                    sb.Append(' ', IndentCount).Append(parserKindStack.Pop());
                }

                sb.Append(success ? " \u2714 " : " \u2717 ");


                var result = text.ToString();

                void AppendResult(bool anyResult)
                {
                    if (anyResult)
                    {
                        sb.Append("`")
                          .Append(Regex.Replace(result, @"\r\n?|\n", @"\n"))
                          .Append("`");
                    }
                    else
                    {
                        sb.Append("<nothing>");
                    }
                }

                if (success) AppendResult(value != null);
                else AppendResult(text.Length > 0);
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
            if (!OutputBraces) {
                sb.Append(">  ");
            }
            sb.Append(' ', IndentCount);

            switch (OutputBraces)
            {
                case true:
                    if (name != null) {
                        sb.Append(name).Append(" : ");
                    }
                    sb.Append((parser?.ToString() ?? "<UNKNOWN>").Replace("\n", "\\n").Replace("\r", "\\r")).Append(" {");
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
    public sealed class ConsoleDeveloperTrace : DeveloperTrace
    {
        protected override void WriteLine(bool isPop, bool success, string str)
        {
            Console.ForegroundColor = isPop ? success ? ConsoleColor.Green : ConsoleColor.Red : ConsoleColor.Yellow;
            Console.WriteLine(str);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }

    public sealed class DelegateDeveloperTrace : DeveloperTrace
    {
        public DelegateDeveloperTrace(Action<string> writeLine) => _writeLineDelegate = writeLine;

        private readonly Action<string> _writeLineDelegate;
        protected override void WriteLine(bool isPop, bool success, string str) => _writeLineDelegate?.Invoke(str);
    }

    public class UserTrace : ITrace
    {
        private readonly Action<string> _onError;

        public UserTrace(Action<string> onError) => _onError = onError;

        private TraceInstance? _trace;
        
        public void Push(IParser parser, string? name)
        {
            _trace ??= new TraceInstance(this);
            _trace.Push(parser, name);
        }

        public void Pop(IParser parser, bool success, object? value, StringSegment text)
        {
            if (_trace == null) throw new InvalidOperationException("Push was not called before Pop");
            _trace.Pop(parser, success, value, text);
            if (_trace.HasFinishedTrace) _trace = null;
        }

        public bool ShowAlternativeErrorBranches { get; set; }
        public bool ShowLineAfterError { get; set; }
        public bool ShowParserStack { get; set; }

        private class TraceInstance : ITrace
        {
            private class ErrorBranch
            {
                public ErrorBranch(Error rootError) => Errors = new List<Error> {rootError};
                public IReadOnlyList<IParser> OuterParsers { get; set; } = Array.Empty<IParser>();
                public List<Error> Errors { get; }

                public IParser FirstTraceHeaderOrInnermostParser => Errors.FirstOrDefault(IsTraceHeader)?.Parser
                                               ?? OuterParsers.FirstOrDefault(p => p.Flags.HasFlag(ParserFlags.TraceHeader))
                                               ?? InnermostError.Parser;

                private static bool IsTraceHeader(Error e) => e.Parser.Flags.HasFlag(ParserFlags.TraceHeader);
                private static bool IsTraceHeader(IParser p) => p.Flags.HasFlag(ParserFlags.TraceHeader);

                public IEnumerable<IParser> TraceHeaders => Errors.Where(IsTraceHeader).Select(e => e.Parser).Concat(OuterParsers.Where(IsTraceHeader));
                
                public Error InnermostError => Errors.First();
                public Error OutermostError => Errors.Last();

                public int TotalParsedCharacters => Errors.First().ErrorSegment.Start;
                public int SuccessfullyParsedCharacters => TotalParsedCharacters - OutermostError.ErrorSegment.Start;
            }

            private class Error
            {
                public Error(IParser parser, StringSegment errorSegment)
                {
                    Parser = parser;
                    ErrorSegment = errorSegment;
                }

                public IParser Parser { get; }
                public StringSegment ErrorSegment { get; }
            }

            public TraceInstance(UserTrace userTrace) => _userTrace = userTrace;
            public bool HasFinishedTrace { get; private set; }

            private readonly UserTrace _userTrace;
            private readonly List<ErrorBranch> _errorsToLog = new List<ErrorBranch>();
            private readonly Stack<IParser> _parserStack = new Stack<IParser>();
            private ErrorBranch? _currentErrorBranch;
            private int _level;

            public void Push(IParser parser, string? name)
            {
                _level++;

                if (_currentErrorBranch != null)
                {
                    _currentErrorBranch.OuterParsers = _parserStack.ToList();
                    _currentErrorBranch = null;
                }
                
                _parserStack.Push(parser);
            }

            public void Pop(IParser parser, bool success, object? value, StringSegment text)
            {
                // We're not on an error branch if we're succeeding
                if (success)
                {
                    if (_currentErrorBranch != null)
                    {
                        _currentErrorBranch.OuterParsers = _parserStack.ToList();
                        _currentErrorBranch = null;
                    }
                }
                else
                {
                    if (_currentErrorBranch == null)
                    {
                        _currentErrorBranch = new ErrorBranch(new Error(parser, text));
                        _errorsToLog.Add(_currentErrorBranch);
                    }
                    else
                    {
                        _currentErrorBranch.Errors.Add(new Error(parser, text));
                    }
                }
                
                _level--;
                _parserStack.Pop();
                
                if (_level <= 0) // We finished tracing
                {
                    if (_currentErrorBranch != null)
                    {
                        _currentErrorBranch.OuterParsers = _parserStack.ToList();
                        _currentErrorBranch = null;
                    }
                    FinalizeTrace();
                }
            }

            private void FinalizeTrace()
            {
                if (_errorsToLog.Count <= 0) return;
                var errorBranchesToLog = _errorsToLog
                                         .GroupBy(branch => branch.TotalParsedCharacters)
                                         .OrderByDescending(grouping => grouping.Key)
                                         .ToArray();
                if (errorBranchesToLog.Length < 1) return; // Can happen if all branches are filtered out

                var builder = new StringBuilder();

                var indent = 0;
                void AppendLine(string? line = null)
                {
                    if (string.IsNullOrEmpty(line)) builder.AppendLine();
                    else builder.Append(' ', 4 * indent).AppendLine(line);
                }

                void AppendErrorWithCaret(ErrorBranch branch)
                {
                    var innermostError = branch.InnermostError;
                    var lines = innermostError.ErrorSegment.String.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
                    var errorIndex = innermostError.ErrorSegment.Start;
                    var errorLineNumber = innermostError.ErrorSegment.String
                                                        .Take(errorIndex)
                                                        .Count(c => c == '\n');
                    var (_, errorLineStartIndex) = innermostError.ErrorSegment.String
                                                                         .Select((ch, idx) => (ch, idx))
                                                                         .Take(errorIndex)
                                                                         .Reverse()
                                                                         .FirstOrDefault(tuple => tuple.ch == '\n');
                    var errorIndexOnLine = errorIndex - errorLineStartIndex;

                    void AppendLineWithNumber(int number) => AppendLine($"{$"{number}: ".PadLeft(4)}{lines[number]}");
                    
                    AppendLineWithNumber(errorLineNumber);
                    AppendLine("^".PadLeft(errorIndexOnLine + 5));
                    if (_userTrace.ShowLineAfterError && errorLineNumber < lines.Length - 1) AppendLineWithNumber(errorLineNumber + 1); // Append line after if the error isn't the last line in the source

                    if (!_userTrace.ShowParserStack) return;
                    AppendLine();
                    indent++;
                    foreach (var parser in branch.Errors.Select(err => err.Parser).Concat(branch.OuterParsers))
                    {
                        AppendLine(parser.ToString());
                    }

                    indent--;
                }

                void AppendGroup(IEnumerable<ErrorBranch> errorBranches)
                {
                    var asArray = errorBranches.ToArray();
                    if (asArray.Length > 1)
                    {
                        var firstBranch = asArray[0];
                        var innerErrorSegment = firstBranch.InnermostError.ErrorSegment;

                        var endOfString = innerErrorSegment.Start >= innerErrorSegment.String.Length;

                        if (!endOfString)
                        {
                            var errorStartCharcter = innerErrorSegment.String[innerErrorSegment.Start];
                            AppendLine($"Unexpected token '{errorStartCharcter}'");
                        }
                        else
                        {
                            AppendLine("Unexpected end of string");
                        }
                        
                        AppendErrorWithCaret(firstBranch);
                        AppendLine($"{asArray.Length} parsers were tried and failed here:");
                        indent++;
                        foreach (var errorBranch in asArray)
                        {
                            AppendLine($"Expected {errorBranch.InnermostError.Parser} while parsing {errorBranch.FirstTraceHeaderOrInnermostParser}");
                        }
                    }
                    else
                    {
                        indent++;
                        var errorBranch = asArray[0];
                        var innerErrorSegment = errorBranch.InnermostError.ErrorSegment;
                        var endOfString = innerErrorSegment.Start >= innerErrorSegment.String.Length;

                        if (!endOfString)
                        {
                            var errorStartCharcter = innerErrorSegment.String[innerErrorSegment.Start];
                            AppendLine($"Expected {errorBranch.InnermostError.Parser} but got '{errorStartCharcter}' while parsing {errorBranch.FirstTraceHeaderOrInnermostParser}");
                        }
                        else
                        {
                            AppendLine($"Expected {errorBranch.InnermostError.Parser} but string ended while parsing {errorBranch.FirstTraceHeaderOrInnermostParser}");
                        }
                        AppendErrorWithCaret(errorBranch);
                    }

                    indent--;
                    AppendLine();
                }

                AppendGroup(errorBranchesToLog[0]);

                if (_userTrace.ShowAlternativeErrorBranches && errorBranchesToLog.Length > 1)
                {
                    AppendLine();
                    AppendLine("=== Other parsers that were tried but failed sooner ===");
                    AppendLine();
                    indent++;
                    for (var groupIndex = 1; groupIndex < errorBranchesToLog.Length; groupIndex++)
                    {
                        AppendGroup(errorBranchesToLog[groupIndex]);
                    }

                    indent--;
                    AppendLine("=== End of parser alternatives ===");
                }

                _userTrace._onError(builder.ToString());
                HasFinishedTrace = true;
            }
        }
    }
}